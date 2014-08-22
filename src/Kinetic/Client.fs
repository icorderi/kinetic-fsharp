namespace Seagate.Kinetic

open System.Net.Sockets
open Seagate.Kinetic.Proto
open Seagate.Kinetic.Network
open Seagate.Kinetic.Model
open Seagate.Kinetic.Model.Builders
open Seagate.Kinetic.Model.Parsers

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

    member x.GetAsync () = 
        async {
            do! Async.AwaitWaitHandle signal |> Async.Ignore
            return value.Value 
        }

            
type Client(host:string, port:int) as this = 
    
    let tcp = new TcpClient()
  
    let mutable connectionID = 0L

    let sequence = ref 0L

    let mutable running = false

    let nextSequence () = System.Threading.Interlocked.Increment(sequence);

    let addHeader (message: Message) =
        message.Command <- Command()
        message.Command.Header <- Header()
        message.Command.Header.User <- this.User
        message.Command.Header.ConnectionID <- this.ConnectionID
        message.Command.Header.ClusterVersion <- this.ClusterVersion
        message.Command.Header.Sequence <- nextSequence()
        message

    let pendingLimit = 10 // TODO change to property

    let queuedCommands = new FSharp.Control.BlockingQueueAgent<Message*Bytes*Promise<Response>>(10) // TODO make argument a property

    let pendingReplies = new System.Collections.Concurrent.ConcurrentDictionary<int64,Promise<Response>>()

    let sender = async {
                    while running do
                        if pendingReplies.Count >= pendingLimit then
                            Log.warn "Reached pending limit for %s:%i (Queued=%i, Pending=%i)" host port queuedCommands.Count pendingReplies.Count

                        while pendingReplies.Count > pendingLimit do
                            do! Async.Sleep 0

                        let! (msg:Message, value:Bytes, p) = queuedCommands.AsyncGet()

                        if Log.isEnabled Level.Debug then
                            Log.debug "Transmitting Seq=%i on %s:%i (Queued=%i, Pending=%i)" msg.Command.Header.Sequence host port queuedCommands.Count pendingReplies.Count

                        pendingReplies.TryAdd(msg.Command.Header.Sequence,p) |> ignore // TODO make sure it got added instead of ignoring it
                        do! SendAsync msg (value.Consume()) tcp // TODO dont consume here!!                    
                    }

    let receiver = async {
                    while running do
                        let! (resp:Message, value) = ReceiveAsync tcp
                        let p = pendingReplies.[resp.Command.Header.AckSequence]
                        pendingReplies.TryRemove(resp.Command.Header.AckSequence) |> ignore

                        connectionID <- resp.Command.Header.ConnectionID // update connectionID to whatever the drive thinks

                        if Log.isEnabled Level.Debug then
                            Log.debug "Received Seq=%i on %s:%i (Queued=%i, Pending=%i)" resp.Command.Header.AckSequence host port queuedCommands.Count pendingReplies.Count
                                                               
                        match resp.Command.Status.Code with
                        | StatusCode.SUCCESS -> p.Set <| Success (resp, value)
                        | _ -> p.Set <| Error (RemoteException (resp.Command.Status)) 
                    }      

    member this.QueueCount with get() = queuedCommands.Count
         
    member this.PendingCount with get() = pendingReplies.Count    

    member this.Outstanding with get() = this.QueueCount + this.PendingCount                                

    member val Host = host with get

    member val Port = port with get

    member val User = 1L with get, set

    member val ConnectionID = connectionID with get

    member val ClusterVersion = 0L with get, set

    member this.Connect() = this.ConnectAsync() |> Async.RunSynchronously

    member this.ConnectAsync() = Async.FromBeginEnd(this.Host, this.Port,
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
        | Some t -> this.SendAsync (command, t) |> Async.RunSynchronously
        | _ -> this.SendAsync command |> Async.RunSynchronously

    member this.SendAsync (command: Command, ?timeout) =
        async {   
            let msg = Message() 
                      |> addHeader
                      |> command.Build

            if timeout.IsSome then msg.Command.Header.Timeout <- timeout.Value  
                      
            if queuedCommands.Count >= 10 then
                Log.warn "Reached queue limit for %s:%i (Queued=%i, Pending=%i)" host port queuedCommands.Count pendingReplies.Count

            let p = Promise()
            do! queuedCommands.AsyncAdd((msg, command.Value, p))

            if Log.isEnabled Level.Debug then
                Log.debug "Enqueued command (Queued=%i, Pending=%i)" queuedCommands.Count pendingReplies.Count
                   
            return p
        }

    /// Applies SendAsync to right hand side of operator and call GetAsync on promise
    static member (<<+) (c : Client, rs) = 
        async {
            let! p = c.SendAsync rs
            return! p.GetAsync()
        }

    /// Applies SendAsync to right hand side of operator
    static member (<<-) (c : Client, rs) = c.SendAsync rs

    /// Applies SendAsync to right hand side of operator and discards result
    static member (<--) (c : Client, rs) = c.SendAsync rs |> Async.Ignore



                             