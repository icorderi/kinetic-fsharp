// ----------------------------------------------------------------------------
// F# async extensions (BlockingQueueAgent.fs)
// (c) Tomas Petricek, 2011, Available under Apache 2.0 license.
// ----------------------------------------------------------------------------
namespace FSharp.Control

open System
open System.Collections.Generic

// ----------------------------------------------------------------------------

type internal CountdownAgentMessage<'T> = 
    | AsyncTick of AsyncReplyChannel<'T> 
    | Tick
    | AsyncWait of AsyncReplyChannel<unit>

/// Agent that implements an asynchronous countdown
type CountdownAgent(startingValue) =

    [<VolatileField>]
    let mutable count = startingValue

    let agent = Agent.Start(fun agent ->
    
        let rec finishedCount() = 
            agent.Scan(fun msg ->
              match msg with 
              | Tick -> Some <| chooseState()
              | AsyncTick reply -> 
                    reply.Reply count
                    Some <| chooseState()
              | AsyncWait reply -> 
                    reply.Reply()
                    Some <| chooseState() )

        and counting() =
            agent.Scan(fun msg ->
              match msg with 
              | Tick -> 
                    count <- count - 1
                    Some <| chooseState()
              | AsyncTick reply -> 
                    count <- count - 1
                    reply.Reply count
                    Some <| chooseState()
              | _ -> None )

        and chooseState() = 
            if count = 0 then finishedCount()
            else counting()

        chooseState() )
     
    member x.Tick() = 
      agent.Post Tick
            
    member x.AsyncTick(?timeout) = 
      agent.PostAndAsyncReply(AsyncTick, ?timeout=timeout)

    member x.AsyncWait(?timeout) = 
      agent.PostAndAsyncReply(AsyncWait, ?timeout=timeout)

     member x.Wait(?timeout) = 
      agent.PostAndReply(AsyncWait, ?timeout=timeout)

    /// Gets the current count
    member x.Count = count
