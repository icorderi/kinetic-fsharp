/// This module contains extension methods for command record values to populate raw proto messages.
module Kinetic.Model.Builders

open Kinetic.Proto
open Kinetic.Model

type Get with 
    member x.Build (cmd : Kinetic.Proto.Command) = 
        cmd.Header.MessageType <- MessageType.GET
        cmd.Body <- Body(KeyValue = KeyValue())

        let y = cmd.Body.KeyValue       
        y.Key <- x.Key.Consume()
        y.DbVersion <- x.Version.Consume()
        y.MetadataOnly <- x.MetadataOnly
        cmd // return the modified message

type Put with 
    member x.Build (cmd : Kinetic.Proto.Command) = 
        cmd.Header.MessageType <- MessageType.PUT
        cmd.Body <- Body(KeyValue = KeyValue())

        let y = cmd.Body.KeyValue       
        y.Key <- x.Key.Consume()
        y.DbVersion <- x.CurrentVersion.Consume()
        y.NewVersion <- x.NewVersion.Consume()
        y.Force <- x.Force
        y.Synchronization <- x.Synchronization
        y.Algorithm <- x.Algorithm
        y.Tag <- x.Tag.Consume()
        cmd // return the modified message

type Delete with 
    member x.Build (cmd : Kinetic.Proto.Command) = 
        cmd.Header.MessageType <- MessageType.DELETE
        cmd.Body <- Body(KeyValue = KeyValue())

        let y = cmd.Body.KeyValue       
        y.Key <- x.Key.Consume()
        y.DbVersion <- x.Version.Consume()
        y.Force <- x.Force
        y.Synchronization <- x.Synchronization
        cmd // return the modified message

type GetLog with 
    member x.Build (cmd : Kinetic.Proto.Command) = 
        cmd.Header.MessageType <- MessageType.GETLOG
        cmd.Body <- Body(GetLog = Kinetic.Proto.GetLog())

        let y = cmd.Body.GetLog       
        y.Types.AddRange x.Types
        cmd // return the modified message

type Range with
    member x.Build (r : Kinetic.Proto.Range) =
        r.StartKey <- x.Start.Consume()
        r.EndKey <- x.End.Consume()
        r.StartKeyInclusive <- x.IsStartInclusive
        r.EndKeyInclusive <- x.IsEndInclusive       
        r.MaxReturned <-  match x.MaxReturned with
                          | Option.None -> 200 // TODO: change to device limit
                          | Some y -> y
        r.Reverse <- x.Reverse      
        r

let buildNoop (cmd : Kinetic.Proto.Command) = 
    cmd.Header.MessageType <- MessageType.NOOP
    cmd // return the modified message

let buildPinOperation (t : PinOperationType) (cmd : Kinetic.Proto.Command) = 
    cmd.Header.MessageType <- MessageType.PINOP
    cmd.Body <- Body(PinOperation = PinOperation())
    cmd.Body.PinOperation.PinOperationType <- t
    cmd

let buildBackgroundOperation (t : BackgroundOperationType) (r : Range) (cmd : Kinetic.Proto.Command) = 
    cmd.Header.MessageType <- MessageType.BACKOP
    cmd.Body <- Body(BackgroundOperation = BackgroundOperation())
    cmd.Body.BackgroundOperation.BackgroundOperationType <- t
    cmd.Body.BackgroundOperation.Range <- r.Build(Kinetic.Proto.Range())
    cmd

type Command with
    member x.Build =
        match x with
        | Noop -> buildNoop
        | Put c -> c.Build
        | Get c -> c.Build
        | Delete c -> c.Build
        | GetLog c -> c.Build
        | Erase -> buildPinOperation PinOperationType.ERASE_PINOP
        | SecureErase -> buildPinOperation PinOperationType.SECURE_ERASE_PINOP
        | Lock -> buildPinOperation PinOperationType.LOCK_PINOP
        | Unlock -> buildPinOperation PinOperationType.UNLOCK_PINOP
        | MediaOptimize r -> buildBackgroundOperation BackgroundOperationType.MEDIAOPTIMIZE r
        | MediaScan r -> buildBackgroundOperation BackgroundOperationType.MEDIASCAN r
 