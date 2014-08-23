// ----------------------------------------------------------------------------
// F# async extensions (BlockingQueueAgent.fs)
// (c) Tomas Petricek, 2011, Available under Apache 2.0 license.
// ----------------------------------------------------------------------------
namespace FSharp.Control

open System
open System.Collections.Generic

// ----------------------------------------------------------------------------

type internal SemaphoreAgentMessage<'T> = 
    | AsyncRelease of AsyncReplyChannel<unit> 
    | Release
    | AsyncWait of AsyncReplyChannel<unit>

/// Agent that implements an asynchronous sempahore
type SemaphoreAgent(limit) =

    [<VolatileField>]
    let mutable count = 0

    let agent = Agent.Start(fun agent ->
    
        let rec full() = 
            agent.Scan(fun msg ->
              match msg with 
              | AsyncRelease reply -> 
                    reply.Reply ()
                    count <- count - 1
                    Some <| chooseState()
              | Release -> 
                    count <- count - 1
                    Some <| chooseState()
              | _ -> None )

        and normal() = async {
            let! msg = agent.Receive()
            match msg with 
            | AsyncRelease reply ->                 
                reply.Reply ()
                count <- count - 1
                return! chooseState()
            | Release ->                 
                count <- count - 1
                return! chooseState()
            | AsyncWait reply -> 
                reply.Reply ()
                count <- count + 1    
                return! chooseState() }

        and empty() = 
            agent.Scan(fun msg ->
                match msg with 
                | AsyncWait reply -> 
                    reply.Reply ()
                    count <- count + 1    
                    Some <| chooseState() 
                | _ -> None )

        and chooseState() = 
            if count = limit then full()
            elif count = 0 then empty()
            else normal()

        empty() )
       
    member x.Release() = 
      agent.Post Release
            
    member x.AsyncRelease(?timeout) = 
      agent.PostAndAsyncReply(AsyncRelease, ?timeout=timeout)

    member x.AsyncWait(?timeout) = 
      agent.PostAndAsyncReply(AsyncWait, ?timeout=timeout)

    member x.Wait(?timeout) = 
      agent.PostAndReply(AsyncWait, ?timeout=timeout)

    /// Gets the current count
    member x.Count = count
