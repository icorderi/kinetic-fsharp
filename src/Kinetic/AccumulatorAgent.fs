// ----------------------------------------------------------------------------
// F# async extensions (BlockingQueueAgent.fs)
// (c) Tomas Petricek, 2011, Available under Apache 2.0 license.
// ----------------------------------------------------------------------------
namespace FSharp.Control

open System
open System.Collections.Generic

// ----------------------------------------------------------------------------

type internal AccumulatorAgentMessage<'T> = 
    | AsyncIncrement of 'T * AsyncReplyChannel<'T> 
    | AsyncDecrement of 'T * AsyncReplyChannel<'T> 
    | AsyncReset of AsyncReplyChannel<'T> 
    | AsyncGet of AsyncReplyChannel<'T>
    | Increment of 'T
    | Decrement of 'T

//type AccumulatorAgent<'T when 'T : (static member (+) : 'T -> 'T -> 'T) and 'T : (static member (-) : 'T -> 'T -> 'T)>(initialValue) =

/// Agent that implements an asynchronous accumulator
type AccumulatorAgent(initialValue : int64) =

    [<VolatileField>]
    let mutable accum = initialValue

    let agent = Agent.Start(fun agent ->

        let rec loop() = async {
            let! msg = agent.Receive()
            match msg with 
            | AsyncIncrement(value, reply) -> 
                accum <- accum + value
                reply.Reply accum 
            | AsyncDecrement(value, reply) ->
                accum <- accum - value
                reply.Reply accum 
            | AsyncReset(reply) ->
                reply.Reply accum 
                accum <- initialValue
            | AsyncGet(reply) ->
                reply.Reply accum 
            | Increment value -> 
                accum <- accum + value
            | Decrement value ->
                accum <- accum - value

            return! loop()
            }

        loop() )

    member x.Increment(v) = 
      agent.Post <| Increment v

    member x.Decrement(v) = 
      agent.Post <| Decrement v
            
    member x.AsyncIncrement(v, ?timeout) = 
      agent.PostAndAsyncReply((fun ch -> AsyncIncrement(v, ch)), ?timeout=timeout)

    member x.AsyncDecrement(v, ?timeout) = 
      agent.PostAndAsyncReply((fun ch -> AsyncDecrement(v, ch)), ?timeout=timeout)

    member x.AsyncReset(?timeout) = 
      agent.PostAndAsyncReply(AsyncReset, ?timeout=timeout)

    member x.AsyncGet(?timeout) = 
      agent.PostAndAsyncReply(AsyncGet, ?timeout=timeout)
