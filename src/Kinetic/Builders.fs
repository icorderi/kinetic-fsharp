/// This module contains extension methods for command record values to populate raw proto messages.
module Kinetic.Model.Builders

open Kinetic.Proto
open Kinetic.Model

type Get with 
    member x.Build (cmd : Kinetic.Proto.Command) = 
        cmd.Header.MessageType <- MessageType.GET
        cmd.Body <- Body(KeyValue = Kinetic.Proto.KeyValue())

        let y = cmd.Body.KeyValue       
        y.Key <- x.Key.Consume()
        y.DbVersion <- x.Version.Consume()
        y.MetadataOnly <- x.MetadataOnly
        cmd // return the modified message

type Put with 
    member x.Build (cmd : Kinetic.Proto.Command) = 
        cmd.Header.MessageType <- MessageType.PUT
        cmd.Body <- Body(KeyValue = Kinetic.Proto.KeyValue())

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
        cmd.Body <- Body(KeyValue = Kinetic.Proto.KeyValue())

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
    member x.Build (cmd : Kinetic.Proto.Command) =
        cmd.Header.MessageType <- MessageType.GETKEYRANGE
        cmd.Body <- Body(Range = Kinetic.Proto.Range())
        x.BuildRange cmd.Body.Range |> ignore
        cmd

    member x.BuildRange (r : Kinetic.Proto.Range) =
        r.StartKey <- x.Start.Consume()
        r.EndKey <- match x.End with
                    | Bytes.None -> Array.create (4*1024) 255uy // TODO: change 4k to device limit
                    | _ -> x.End.Consume()
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
    cmd.Body.BackgroundOperation.Range <- r.BuildRange <| (Kinetic.Proto.Range())
    cmd

let buildParameter (cmd : Kinetic.Proto.Command) (p : Parameter) =
    match p with
    | Timeout t -> cmd.Header.Timeout <- t
    | EarlyExit -> cmd.Header.EarlyExit <- true
    | Priority l -> cmd.Header.Priority <- l
    | TimeQuanta tq -> cmd.Header.TimeQuanta <- tq
    cmd

type Command with
    member x.Build cmd =
        match x with
        | Noop -> buildNoop cmd
        | Put c -> c.Build cmd
        | Get c -> c.Build cmd
        | GetKeyRange r -> r.Build cmd
        | Delete c -> c.Build cmd
        | GetLog c -> c.Build cmd
        | Erase -> 
            buildPinOperation PinOperationType.ERASE_PINOP cmd
        | SecureErase -> 
            buildPinOperation PinOperationType.SECURE_ERASE_PINOP cmd
        | Lock -> 
            buildPinOperation PinOperationType.LOCK_PINOP cmd
        | Unlock -> 
            buildPinOperation PinOperationType.UNLOCK_PINOP cmd
        | MediaOptimize r -> 
            buildBackgroundOperation BackgroundOperationType.MEDIAOPTIMIZE r cmd
        | MediaScan r -> 
            buildBackgroundOperation BackgroundOperationType.MEDIASCAN r cmd
        | WithParams (inner,prms) -> 
            List.fold buildParameter (inner.Build(cmd)) prms        
 