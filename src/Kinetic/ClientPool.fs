namespace Kinetic

open System.Net.Sockets
open Kinetic.Proto
open Kinetic.Network
open Kinetic.Model
open Kinetic.Model.Builders
            
type ClientPool(clients) = 
          
    let log = new Log.Logger("ClientPool")

    let mutable running = false

    let pendingLimit = 40 // TODO change to property

    let queuedCommands = new FSharp.Control.BlockingQueueAgent<Command*int64 option*Promise<Response>>(40) // TODO make argument a property

    let pendingReplies = ref 0

    let mutable current = 0
    let scheduler = fun (cs : Client list) ->
        let c = cs.[current]
        current <- current + 1
        if current >= cs.Length then
            current <- 0
        c

    let sender = async {
                    while running do
                        if !pendingReplies >= pendingLimit then
                            log.warn "Reached pending limit (Queued=%i, Pending=%i)" queuedCommands.Count !pendingReplies

                        while !pendingReplies > pendingLimit do
                            do! Async.Sleep 0

                        let! (cmd, timeout, p) = queuedCommands.AsyncGet()

                        let c = scheduler clients

                        if log.isEnabled Log.Level.Debug then
                            log.debug "Pool transmitting to %s:%i (Queued=%i, Pending=%i)" c.Host c.Port queuedCommands.Count !pendingReplies

                        System.Threading.Interlocked.Increment pendingReplies |> ignore
                        let! cp = if timeout.IsSome then c.SendAsync(cmd, timeout.Value)
                                  else c.SendAsync cmd

                        async {
                            let! v = cp.GetAsync()
                            System.Threading.Interlocked.Decrement pendingReplies |> ignore
                            if Log.isEnabled Log.Level.Debug then
                                Log.debug "Received response from %s:%i, routing to pool promise." c.Host c.Port
                            p.Set v
                        } |> Async.Start
                    }

    member this.Connect() = this.ConnectAsync() |> Async.RunSynchronously

    member this.ConnectAsync() = 
        async {
            running <- true
            sender |> Async.Start }

    member this.QueueCount with get() = queuedCommands.Count
         
    member this.PendingCount with get() = !pendingReplies    

    member this.Outstanding with get() = this.QueueCount + this.PendingCount                                

    member this.Send (command, ?timeout) = 
        match timeout with
        | Some t -> this.SendAsync (command, t) |> Async.RunSynchronously
        | _ -> this.SendAsync command |> Async.RunSynchronously

    member this.SendAsync (command: Command, ?timeout) =
        async {            
            if queuedCommands.Count >= 40 then
                log.warn "Reached queue limit (Queued=%i, Pending=%i)" queuedCommands.Count !pendingReplies

            let p = Promise()

            do! queuedCommands.AsyncAdd((command, timeout, p))

            return p
        }

    /// Applies SendAsync to right hand side of operator and call GetAsync on promise
    static member (<<+) (c : ClientPool, rs) = 
        async {
            let! p = c.SendAsync rs
            return! p.GetAsync()
        }

    /// Applies SendAsync to right hand side of operator
    static member (<<-) (c : ClientPool, rhs) = c.SendAsync rhs

    /// Applies SendAsync to right hand side of operator and discards result
    static member (<--) (c : ClientPool, rhs) = c.SendAsync rhs |> Async.Ignore



                             