/// This module contains extension methods for command record values to populate raw proto messages.
module Seagate.Kinetic.Model.Parsers

open Seagate.Kinetic.Proto
open Seagate.Kinetic.Model

type Get with 
    member x.Parse (resp : Message, value : bytes) = 
        match resp.Command.Status.Code with
        | StatusCode.SUCCESS -> Value <| Some value
        | StatusCode.NOT_FOUND -> Value Option.None
        | x -> Error <| RemoteException (resp.Command.Status)

type Put with 
    member x.Parse (resp : Message, value : bytes) = 
        match resp.Command.Status.Code with
        | StatusCode.SUCCESS -> Value x
        | x -> Error <| RemoteException (resp.Command.Status)

type Delete with 
    member x.Parse (resp : Message, value : bytes) = 
        match resp.Command.Status.Code with
        | StatusCode.SUCCESS -> Value true
        | StatusCode.NOT_FOUND -> Value false
        | x -> Error <| RemoteException (resp.Command.Status)

let parseNoop (resp : Message, value : bytes) = 
    match resp.Command.Status.Code with
    | StatusCode.SUCCESS -> Value ()
    | x -> Error <| RemoteException (resp.Command.Status)


type Command with
    member x.Parse<'T> (resp : Message, value : bytes) : 'T =
        match x with
        | Noop -> parseNoop(resp, value)
        | Put c -> c.Parse
        | Get c -> c.Parse
        | Delete c -> c.Parse