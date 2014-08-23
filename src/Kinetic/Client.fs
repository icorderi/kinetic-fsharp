namespace Kinetic

open System.Net.Sockets
open System.IO
open Kinetic.Proto
open Kinetic.Network
open Kinetic.Model
open Kinetic.Model.Builders

type Promise<'T>()=

    let mutable value : 'T option = Option.None

    let signal = new System.Threading.ManualResetEvent(false)

    member x.HasValue with get () = value.IsSome 

    member x.Peek with get() = value

    member x.Set t =
        value <- Some t 
        signal.Set() |> ignore

    member x.Get () = 
       signal.WaitOne() |> ignore
       value.Value 

    member x.AsyncGet () = 
        async {
            do! Async.AwaitWaitHandle signal |> Async.Ignore
            return value.Value 
        }

type internal ClientMessage = 
    | Operation of Message*Bytes*Promise<Response>
    | Signal of System.Threading.ManualResetEvent
            
type Client(host:string, port:int) as this = 

    let log = new Log.Logger("Client")
    
    let tcp = new TcpClient()
  
    let mutable connectionID = 0L

    let sequence = ref 0L

    let mutable running = false

    let nextSequence () = System.Threading.Interlocked.Increment(sequence); // TODO move call to sender loop and get rid of lock

    let addHeader (message: Message) =
        message.Command <- Command()
        message.Command.Header <- Header()
        message.Command.Header.Identity <- this.Identity
        message.Command.Header.ConnectionID <- this.ConnectionID
        message.Command.Header.ClusterVersion <- this.ClusterVersion
        message.Command.Header.Sequence <- nextSequence()
        message

    let pendingLimit = 10 // TODO change to property

    let queuedCommands = new FSharp.Control.BlockingQueueAgent<ClientMessage>(10) // TODO make argument a property

    let pendingReplies = new System.Collections.Concurrent.ConcurrentDictionary<int64,Promise<Response>>()

    let sender = async {
                    while running do

                        if log.isEnabled Log.Level.Debug && pendingReplies.Count >= pendingLimit then
                            log.debug "Reached pending limit for %s:%i (Queued=%i, Pending=%i)" host port queuedCommands.Count pendingReplies.Count

                        while pendingReplies.Count > pendingLimit do
                            do! Async.Sleep 0

                        let! x = queuedCommands.AsyncGet() 
                        match x with
                        | Signal s -> s.Set() |> ignore
                        | Operation (msg, value, p) ->
                            if log.isEnabled Log.Level.Debug then
                                log.debug "Transmitting Seq=%i on %s:%i (Queued=%i, Pending=%i)" msg.Command.Header.Sequence host port queuedCommands.Count pendingReplies.Count

                            pendingReplies.TryAdd(msg.Command.Header.Sequence,p) |> ignore // TODO make sure it got added instead of ignoring it

                            // calculate hmac
                            use ms = new MemoryStream()
                            ProtoBuf.Serializer.Serialize(ms, msg.Command)
                            msg.Hmac <- calculateHmac this.Secret (ms.GetBuffer()) 0 (int ms.Length)

                            do! AsyncSend msg (value.Consume()) tcp // TODO dont consume here!! let the network consume it                    
                    }

    let receiver = async {
                    while running do
                        let! (resp:Message, value) = AsyncReceive tcp
                        let p = pendingReplies.[resp.Command.Header.AckSequence]
                        pendingReplies.TryRemove(resp.Command.Header.AckSequence) |> ignore

                        connectionID <- resp.Command.Header.ConnectionID // update connectionID to whatever the drive thinks

                        if log.isEnabled Log.Level.Debug then
                            log.debug "Received Seq=%i on %s:%i (Queued=%i, Pending=%i)" resp.Command.Header.AckSequence host port queuedCommands.Count pendingReplies.Count
                                                               
                        match resp.Command.Status.Code with
                        | StatusCode.SUCCESS -> p.Set <| Success (resp, value)
                        | _ -> p.Set <| Error (RemoteException (resp.Command.Status)) 
                    }      

    member this.QueueCount with get() = queuedCommands.Count
         
    member this.PendingCount with get() = pendingReplies.Count    

    member this.Outstanding with get() = this.QueueCount + this.PendingCount                                

    member val Host = host with get

    member val Port = port with get

    member val Identity = 1L with get, set // default identity

    member val Secret = System.Text.Encoding.UTF8.GetBytes("asdfasdf") 

    member val ConnectionID = connectionID with get

    member val ClusterVersion = 0L with get, set

    member this.Connect() = this.AsyncConnect() |> Async.RunSynchronously

    member this.AsyncConnect() = Async.FromBeginEnd(this.Host, this.Port,
                                   (fun (host:string, port:int, callback, state) -> tcp.BeginConnect(host, port, callback, state)),
                                   (fun iar -> 
                                        tcp.EndConnect(iar)
                                        running <- true
                                        receiver |> Async.Start
                                        sender |> Async.Start
                                        ) )
                                  
    member this.Close() =
        running <- false
        // TODO when queue's depleted, close socket

    member this.Send (command, ?timeout) = 
        match timeout with
        | Some t -> this.AsyncSend (command, t) |> Async.RunSynchronously
        | _ -> this.AsyncSend command |> Async.RunSynchronously

    member this.SendAndReceive (command, ?timeout) = 
        let p : Promise<Response> = match timeout with
                                    | Some t -> this.Send (command, t)
                                    | _ -> this.Send command
        p.Get()
                
    member this.AsyncSend (command: Command, ?timeout) =
        async {   
            let msg = Message() 
                      |> addHeader
                      |> command.Build

            if timeout.IsSome then msg.Command.Header.Timeout <- timeout.Value  
                      
            if log.isEnabled Log.Level.Debug && queuedCommands.Count >= 10 then
                log.debug "Reached queue limit for %s:%i (Queued=%i, Pending=%i)" host port queuedCommands.Count pendingReplies.Count

            let p = Promise()
            do! queuedCommands.AsyncAdd(Operation (msg, command.Value, p))

            if log.isEnabled Log.Level.Debug then
                log.debug "Enqueued command (Queued=%i, Pending=%i)" queuedCommands.Count pendingReplies.Count
                   
            return p
        }
                
    member this.Wait () =
        let s = new System.Threading.ManualResetEvent(false)
        queuedCommands.Add <| Signal s
        s.WaitOne() |> ignore
        for x in pendingReplies.Values do
            x.Get() |> ignore
        
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



                             