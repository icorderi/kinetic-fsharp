namespace Kinetic.Model

open Kinetic.Proto

/// Represents possible value sources for bytes
type Bytes =
    | None 
    /// Plain array of bytes
    | Bytes of bytes
    /// Section of an array (array, offset, length)
    | View of bytes * int * int
    /// Stream containing bytes to transfer, length
    | Stream of System.IO.Stream * int 
    /// String that will be converted to bytes using UTF-8
    | String of string
    /// Defer creation or consumption of bytes until needed for transfer
    | Lazy of Async<Bytes>

    override x.ToString() =
        match x with 
        | None -> ""
        | Bytes bs -> System.Text.UTF8Encoding.UTF8.GetString(bs)
        | View (bs, offset, length) -> "Some view"
        | Stream (s, length) -> "Why are you doing this?"
        | String s -> s
        | Lazy a -> "[<Lazy>]" 

    /// Gets the actual bytes. 
    /// Lazy values will block until bytes are produced.
    member x.Consume() = 
        match x with 
        | None -> null
        | Bytes bs -> bs
        | View (bs, offset, length) -> Array.sub bs offset length
        | Stream (s, length) -> failwith "Not implemented."
        | String s -> System.Text.Encoding.UTF8.GetBytes(s)
        | Lazy a -> let x2 = a |> Async.RunSynchronously
                    x2.Consume()      
                      
// -------------------------------------------------------------------
// Exceptions
// -------------------------------------------------------------------

exception RemoteException of Status

// -------------------------------------------------------------------
// Commands
// -------------------------------------------------------------------

/// Represents a get command
type Get = { 
    Key : Bytes 
    Version : Bytes
    MetadataOnly : bool
    }

/// Represents a put 
type Put = { 
    Key : Bytes 
    Value : Bytes
    NewVersion : Bytes 
    CurrentVersion : Bytes
    Force : bool
    Tag : Bytes
    Algorithm : Algorithm
    Synchronization : Synchronization
    }

/// Represents a delete command
type Delete = { 
    Key : Bytes 
    Version : Bytes 
    Force : bool
    Synchronization : Synchronization
    }

/// Represents a getLog command
type GetLog = { 
    Types : List<LogType> 
    }
// -------------------------------------------------------------------

/// Union type all commands
type Command =
    | Noop
    | Get of Get
    | Put of Put
    | Delete of Delete
    | GetLog of GetLog

    member x.Value =
        match x with
        | Put c -> c.Value
        | _ -> None

// -------------------------------------------------------------------
// Reponses
// -------------------------------------------------------------------

type Response =
    | Success of Message * bytes option
    | Cancelled
    | Error of exn   

/// Module containing default values for easier command instantiation
module Default =

    /// <summary> Default value for a get command </summary>
    /// <remarks> Usage, { get with Key = String "some-key" } </remarks>
    /// <returns> Pre-initialized <see cref="Get"> Get</see> command</returns>
    let get = { Key = None ; Version = None ; MetadataOnly = false }

    /// <summary> Default value for a put command </summary>
    /// <remarks> Usage, { put with Key = String "some-key" ; Value = String "my-value" } </remarks>
    /// <returns> Pre-initialized <see cref="Put"> Put</see> command</returns>
    let put = { Key = None ; Value = None ; NewVersion = None ; CurrentVersion = None ; Force = false
                Tag = String "1337" ; Algorithm = Algorithm.SHA1 ; Synchronization = Synchronization.WRITEBACK }

    /// <summary> Default value for a delete command </summary>
    /// <remarks> Usage, { delete with Key = String "some-key" ; Force = true } </remarks>
    /// <returns> Pre-initialized <see cref="Delete"> Delete</see> command</returns>
    let delete = { Key = None ; Version = None ; Force = false ; Synchronization = Synchronization.WRITEBACK }
      

