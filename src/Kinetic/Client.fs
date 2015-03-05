// Copyright (c) 2015 Ignacio Corderi

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// author: Ignacio Corderi

namespace Kinetic

open System.Net.Sockets
open System.IO
open Kinetic.Proto
open Kinetic.Network
open Kinetic.Model
open Kinetic.Model.Builders
open Kinetic.Model.Parsers

type Promise<'T>()=

    let mutable value : 'T option = Option.None

    let signal = new System.Threading.ManualResetEvent(false)

    member x.HasValue with get () = value.IsSome 

    member x.Peek with get() = value

    member x.Set t =
        value <- Some t 
        signal.Set() |> ignore

    member x.Get () = 
       x.Wait()
       value.Value 

    member x.AsyncGet () = 
        async {
            do! x.AsyncWait()
            return value.Value 
        }

    member x.Wait () = 
        signal.WaitOne() |> ignore

    member x.AsyncWait () = 
        async {
            do! Async.AwaitWaitHandle signal |> Async.Ignore
        }

type internal ClientMessage = 
    | Operation of Kinetic.Proto.Command * Bytes * Promise<Result> * Parser
    | Signal of System.Threading.ManualResetEvent
 

type Authentication = 
    | Pin of Bytes
    | Hmac of int64 * Bytes
            

type Client(host:string, port:int) as this = 

    let log = new Log.Logger("Client")
    do log.Level <- Log.Level.Info

    let tcp = new TcpClient()
  
    let mutable connectionID = 0L

    let sequence = ref 0L

    let mutable running = false

    let addHeader (cmd: Kinetic.Proto.Command) =
        cmd.Header.ConnectionID <- this.ConnectionID
        cmd.Header.ClusterVersion <- this.ClusterVersion.Value
        cmd.Header.Sequence <- !sequence     
        sequence := !sequence + 1L // Add header is called from the sender loop, it's thread safe
        cmd

    let wrapAuthentication (cmd: Kinetic.Proto.Command) =
        use ms = new MemoryStream()
        ProtoBuf.Serializer.Serialize(ms, cmd)

        let msg = Kinetic.Proto.Message()
        msg.CommandBytes <- ms.ToArray()

        match this.Authentication with
        | Pin pin ->
            msg.AuthenticationType <- AuthenticationType.PINAUTH
            msg.PinAuthentication <- PinAuthentication(Pin=pin.Consume())    
        | Hmac (identity, secret) -> 
            msg.AuthenticationType <- AuthenticationType.HMACAUTH
            msg.HmacAuthentication <- HmacAuthentication(Identity=identity)
            msg.HmacAuthentication.Hmac <- calculateHmac (secret.Consume()) (ms.GetBuffer()) 0 (int ms.Length)
        
        msg.CommandBytes <- ms.ToArray()
        msg
           
    let queuedCommands = new FSharp.Control.BlockingQueueAgent<ClientMessage>(10) // TODO use constant as default

    let pendingReplies = new System.Collections.Concurrent.ConcurrentDictionary<int64,Promise<Result> * Parser>()

    let unsolicitedStatus = new Event<_>()

    let mutable config = Option.None

    let mutable limits = Option.None

    let sender = async {
                    while running do

                        if log.isEnabled Log.Level.Debug && pendingReplies.Count >= this.PendingLimit then
                            log.debug "Reached pending limit for %s:%i (Queued=%i, Pending=%i)" host port queuedCommands.Count pendingReplies.Count

                        while pendingReplies.Count > this.PendingLimit do
                            do! Async.Sleep 0

                        let! x = queuedCommands.AsyncGet() 
                        match x with
                        | Signal s -> s.Set() |> ignore
                        | Operation (cmd, value, promise, parser) ->
                            if log.isEnabled Log.Level.Debug then
                                log.debug "Transmitting Seq=%i on %s:%i (Queued=%i, Pending=%i)" cmd.Header.Sequence host port queuedCommands.Count pendingReplies.Count
                                                        
                            let msg = cmd |> addHeader |> wrapAuthentication

                            pendingReplies.TryAdd(cmd.Header.Sequence, (promise, parser)) |> ignore // TODO make sure it got added instead of ignoring it

                            do! AsyncSend msg (value.Consume()) tcp // TODO dont consume here!! let the network consume it                    
                    }

    let receiver = async {
                    while running do
                        let! (resp:Message, value) = AsyncReceive tcp

                        match resp.AuthenticationType with
                        | AuthenticationType.HMACAUTH -> 
                            match this.Authentication with
                            | Pin _ -> log.warn "Something is wrong, sent HMAC authentication and received PIN response."
                            | Hmac (identity, secret) when resp.HmacAuthentication.Identity = identity -> 
                                let hmac = calculateHmac (secret.Consume()) resp.CommandBytes 0 (int resp.CommandBytes.Length)
                                let allEqual = Array.forall2 (fun x y -> x = y)
                                if hmac <> resp.HmacAuthentication.Hmac then
                                    log.error "HMAC doesn't match" // TODO: Boom, invalid HMAC
                            | Hmac (identity, _) -> log.error "Identities don't match" // TODO: Boom, identities dont match 
                        | AuthenticationType.PINAUTH ->
                            match this.Authentication with
                            | Hmac _ -> log.warn "Something is wrong, sent PIN authentication and received HMAC response."
                            | Pin _ -> () // this is expected
                        | AuthenticationType.INVALID_AUTH_TYPE -> 
                            log.error "Ehh.. Invalid authentication status received."
                        | _ -> () // Nothing to check for the rest

                        let ms = new MemoryStream(resp.CommandBytes)
                        let cmd : Kinetic.Proto.Command = ProtoBuf.Serializer.Deserialize(ms)

                        if cmd.Header.ConnectionID <> this.ConnectionID then
                           // log.error "TODO: ConnectionId doesn't match, received %A expected %A. Throw exception." cmd.Header.ConnectionID this.ConnectionID 
                           ()

                        match resp.AuthenticationType with
                        | AuthenticationType.UNSOLICITEDSTATUS ->
                            unsolicitedStatus.Trigger cmd.Status
                        | _ ->
                            let promise, parser = pendingReplies.[cmd.Header.AckSequence]
                            pendingReplies.TryRemove(cmd.Header.AckSequence) |> ignore

                            if log.isEnabled Log.Level.Debug then
                                log.debug "Received Seq=%i on %s:%i (Queued=%i, Pending=%i)" cmd.Header.AckSequence host port queuedCommands.Count pendingReplies.Count
                                                                   
                            match cmd.Status.Code with
                            | StatusCode.SUCCESS -> promise.Set <| (Success <| parser (resp, cmd, value))
                            | _ -> promise.Set <| Error (RemoteException (cmd.Status)) 
                    } 

    let handshake() = 
        async {
            let! (resp:Message, value) = AsyncReceive tcp
            if resp.AuthenticationType <> AuthenticationType.UNSOLICITEDSTATUS then 
                failwith "Initial handshake response was not an UNSOLICITEDSTATUS." 
            
            let ms = new MemoryStream(resp.CommandBytes)
            let cmd : Kinetic.Proto.Command = ProtoBuf.Serializer.Deserialize(ms)

            if cmd.Status.Code <> StatusCode.SUCCESS then
                raise <| RemoteException (cmd.Status)

            connectionID <- cmd.Header.ConnectionID

            match this.ClusterVersion with
            | Some x when x <> cmd.Header.ClusterVersion -> 
                raise <| InvalidClusterVersion 
                    (sprintf "Cluster mismatch, client configured as %i but device expected %i." x cmd.Header.ClusterVersion
                    , cmd.Header.ClusterVersion)
            | Option.None -> this.ClusterVersion <- Some cmd.Header.ClusterVersion
            | _ -> () // all good

            config <- Some cmd.Body.GetLog.Configuration
            limits <- Some cmd.Body.GetLog.Limits
        }

    member val PendingLimit = 2 with get,set

    member this.QueueLimit 
        with get() = queuedCommands.Capacity 
        and  set v = queuedCommands.Capacity <- v

    member this.Configuration with get() = config.Value

    member this.Limits with get() = limits.Value
                
    [<CLIEvent>]
    member this.UnsolicitedStatus = unsolicitedStatus.Publish

    member this.QueueCount with get() = queuedCommands.Count
         
    member this.PendingCount with get() = pendingReplies.Count    

    member this.Outstanding with get() = this.QueueCount + this.PendingCount                                

    member val Host = host with get

    member val Port = port with get

    member val Authentication = Hmac (1L, String "asdfasdf") with get, set

    member this.ConnectionID with get() = connectionID

    member val ClusterVersion : Option<int64> = Option.None with get, set

    member this.Connect() = this.AsyncConnect() |> Async.RunSynchronously

    member this.AsyncConnect() =
        async {
            do! Async.FromBeginEnd(this.Host, this.Port,
                                           (fun (host:string, port:int, callback, state) -> tcp.BeginConnect(host, port, callback, state)),
                                           (fun iar -> 
                                                tcp.EndConnect(iar)
                                                ) )            
            tcp.Client.NoDelay <- true
            do! handshake()
            running <- true
            receiver |> Async.Start
            sender |> Async.Start
        }
                                  
    member this.Close() =
        running <- false
        // TODO when queue's depleted, close socket

    member this.Send command = this.AsyncSend command |> Async.RunSynchronously

    member this.SendAndReceive command = 
        let p : Promise<Result> = this.Send command
        p.Get()

    member this.AsyncSendAndReceive command =
        async{
            let! p = this.AsyncSend command
            return! p.AsyncGet()
        }            

    member this.AsyncSend (command: Command) =
        async {   
            let cmd = Command(Header=Header()) |> command.Build
                              
            if log.isEnabled Log.Level.Debug && queuedCommands.Count >= 10 then
                log.debug "Reached queue limit for %s:%i (Queued=%i, Pending=%i)" host port queuedCommands.Count pendingReplies.Count

            let p = Promise()
            do! queuedCommands.AsyncAdd(Operation (cmd, command.Value, p, command.Parser()))

            if log.isEnabled Log.Level.Debug then
                log.debug "Enqueued command (Queued=%i, Pending=%i)" queuedCommands.Count pendingReplies.Count
                   
            return p
        }
                
    member this.Wait () =
        let s = new System.Threading.ManualResetEvent(false)
        queuedCommands.Add <| Signal s
        s.WaitOne() |> ignore
        for (p,_) in pendingReplies.Values do
            p.Get() |> ignore
        
    /// Applies SendAndReceive to right hand side of operator
    static member (<+) (c : Client, rs) = c.SendAndReceive rs

    /// Applies SendAsync to right hand side of operator and calls GetAsync on promise
    static member (<<+) (c : Client, rs) = 
        async {
            let! p = c.AsyncSend rs
            return! p.AsyncGet()
        }

    /// Applies SendAsync to right hand side of operator
    static member (<<-) (c : Client, rs) = c.AsyncSend rs

    /// Applies SendAsync to right hand side of operator and discards result
    static member (<--) (c : Client, rs) = c.AsyncSend rs |> Async.Ignore

    override this.ToString() = 
        sprintf "%s:%i" this.Host this.Port


                             