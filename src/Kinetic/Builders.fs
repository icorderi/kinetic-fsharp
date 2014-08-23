/// This module contains extension methods for command record values to populate raw proto messages.
module Kinetic.Model.Builders

open Kinetic.Proto
open Kinetic.Model

type Get with 
    member x.Build (msg : Message) = 
        msg.Command.Header.MessageType <- MessageType.GET
        msg.Command.Body <- Body(KeyValue = KeyValue())

        let y = msg.Command.Body.KeyValue       
        y.Key <- x.Key.Consume()
        y.DbVersion <- x.Version.Consume()
        y.MetadataOnly <- x.MetadataOnly
        msg // return the modified message

type Put with 
    member x.Build (msg : Message) = 
        msg.Command.Header.MessageType <- MessageType.PUT
        msg.Command.Body <- Body(KeyValue = KeyValue())

        let y = msg.Command.Body.KeyValue       
        y.Key <- x.Key.Consume()
        y.DbVersion <- x.CurrentVersion.Consume()
        y.NewVersion <- x.NewVersion.Consume()
        y.Force <- x.Force
        y.Synchronization <- x.Synchronization
        y.Algorithm <- x.Algorithm
        y.Tag <- x.Tag.Consume()
        msg // return the modified message

type Delete with 
    member x.Build (msg : Message) = 
        msg.Command.Header.MessageType <- MessageType.DELETE
        msg.Command.Body <- Body(KeyValue = KeyValue())

        let y = msg.Command.Body.KeyValue       
        y.Key <- x.Key.Consume()
        y.DbVersion <- x.Version.Consume()
        y.Force <- x.Force
        y.Synchronization <- x.Synchronization
        msg // return the modified message

let buildNoop (msg : Message) = 
    msg.Command.Header.MessageType <- MessageType.NOOP
    msg // return the modified message

type Command with
    member x.Build =
        match x with
        | Noop -> buildNoop
        | Put c -> c.Build
        | Get c -> c.Build
        | Delete c -> c.Build