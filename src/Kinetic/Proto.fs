namespace Seagate.Kinetic.Proto

open ProtoBuf
open System.Collections.Generic

type bytes = byte array


[<ProtoContract>]
type Property() = 
    ///prop name
    [<ProtoMember(1, IsRequired=true)>]
    member val Name : string = null with get,set

    ///prop value
    [<ProtoMember(2)>]
    member val Value : string = null with get,set


/// operation code
type MessageType =
    /// NOT IN THE PROTO
    | NONE = 0
    /// get operation
    | GET = 2 
    /// get response
    | GET_RESPONSE = 1
    /// put operation 
    | PUT = 4 
    | PUT_RESPONSE = 3
    | DELETE = 6
    | DELETE_RESPONSE = 5
    | GETNEXT = 8
    | GETNEXT_RESPONSE = 7
    | GETPREVIOUS = 10
    | GETPREVIOUS_RESPONSE = 9
    | GETKEYRANGE = 12
    | GETKEYRANGE_RESPONSE = 11
    | GETVERSION = 16
    | GETVERSION_RESPONSE = 15
    | STEALER = 18
    | STEALER_RESPONSE = 17
    | DONOR = 20
    | DONOR_RESPONSE = 19
    | SETUP = 22
    | SETUP_RESPONSE = 21
    | GETLOG = 24
    | GETLOG_RESPONSE = 23
    | SECURITY = 26
    | SECURITY_RESPONSE = 25

    /// peer to peer push operation
    | PEER2PEERPUSH = 28
    | PEER2PEERPUSH_RESPONSE = 27

    | NOOP = 30
    | NOOP_RESPONSE = 29


[<ProtoContract>]
[<AllowNullLiteral>]
type Header() = 
    
    /// "cluster" is the version number of the cluster definition. If this is incompatible,
    /// the request is rejected. If it is missing, it is assumed to be 0. (0 allows systems not
    /// using cluster vesioning to ignore this field in the header and in the setup.)
    [<ProtoMember(1)>]
    member val ClusterVersion : int64 = 0L with get,set

    /// The "user" identifies the user and the key and algorithm to be used for hmac. (See security document).
    [<ProtoMember(2)>]
    member val User : int64 = 0L with get,set

    /// A unique number for this connection between the source and target. On the first request
    /// to the drive, this should be the time of day in seconds since 1970. The drive can change this
    /// number and the client must continue to use the new number and the number must remain
    /// constant during the session. (See security document).
    [<ProtoMember(3)>]
    member val ConnectionID : int64 = 0L with get,set
  
    /// the sequence of this request in this TCP connection. As long as this value is getting larger we have
    /// strong ordering and replay prevention within a session. This combined with the time and connectionID
    /// provides strong ordering between sessions. (See security document).
    [<ProtoMember(4)>]
    member val Sequence : int64 = 0L with get,set

    ///co-related sequence
    [<ProtoMember(6)>]
    member val AckSequence : int64 = 0L with get,set

    ///operation code - put/get/delete/GetLog, etc.
    [<ProtoMember(7)>]
    member val MessageType : MessageType = MessageType.NONE with get,set

    /// Request timeout (in ms). This is the amount of time that this request should take. If this timeout
    /// is triggered, there are three possible results that can be returned.
    ///     - SERVICE_BUSY meaning that the request was still on the queue waiting to be executed
    ///     - EXPIRED meaning that a long running operation was stopped because the time expired.
    ///     - DATA_ERROR meaning that the request was in process, but that the error recovery was not
    ///                  complete at the time that the time expired
    [<ProtoMember(9)>]
    member val Timeout : int64 = 0L with get,set

    /// fail fast. Requests will not attempt multi revolution recoveries even if the timeout has not occurred.
    /// In this case the result will be DATA_ERROR. To have the drive exhaust all possible error recovery, leave
    /// this field off or set to false, and make sure that the timeout is set to be longer than any possible queue
    /// time and error recovery time. On a disk drive, the maximum error recovery time could be seconds.
    /// Once all possible data recovery operations are complete and have not succeeded, PERM_DATA_ERROR will be
    /// returned.
    [<ProtoMember(10)>]
    member val FailFast : bool = false with get,set

    /// A hint that this request is part of a background scan, this is a hint that can allow the drive
    /// to do it's background read process on this record. This allows the drive not to do it's own
    /// background scan.
    [<ProtoMember(11)>]
    member val BackgroundScan : bool = false with get,set

    ///message properties (reserved for future use)
    [<ProtoMember(8)>]
    member val Properties : List<Property> = new List<Property>() with get,set


type Synchronization =
    | INVALID_SYNCHRONIZATION = -1
    /// Synchronouse write
    | WRITETHROUGH = 1
    /// Asynchronouse write
    | WRITEBACK = 2
    | FLUSH = 3


type Algorithm =
    /// NOT IN THE PROTO!
    | NONE = 0
    /// see NIST
    | SHA1 = 1 
    /// see NIST
    | SHA2 = 2
    /// see NIST. The length of the tag determined the lenth of the hash 
    | SHA3 = 3
    /// the CRC32 is the standard ethernet CRC32. See IEEE 
    | CRC32 = 4
    /// The CRC is ... 
    | CRC64 = 5
     
    // 6-99 are reserverd.
    // 100-inf are private algorithms.


/// key/value entry operation
[<ProtoContract>]
[<AllowNullLiteral>]
type KeyValue() = 
        
    /// On a put or delete, this is the next version that the data will be. The version field is opaque to the
    /// target. (See Atomic operations document)
    [<ProtoMember(2)>]
    member val NewVersion : bytes = null with get,set

    /// On a put or delete, this forces the write to ignore the existing version of existing data (if it exists).
    [<ProtoMember(8)>]
    member val Force : bool = false with get,set

    ///entry key
    [<ProtoMember(3)>]
    member val Key : bytes = null with get,set

    ///entry version in store
    [<ProtoMember(4)>]
    member val DbVersion : bytes = null with get,set

    /// this is the integrity value of the data. This may or may not be in the clear, depending on the algorithm
    /// used.
    [<ProtoMember(5)>]
    member val Tag : bytes = null with get,set

    /// The following is for the protection of the data. If the data is protected with a hash or CRC, then
    /// the algorithm will be negative. If the data protection algorithm is not a standard unkeyed algorithm
    /// then  a positive number is used and the drive has no idea what the key is. See the discussion of
    /// encrypted key/value store.(See security document).
    [<ProtoMember(6)>]
    member val Algorithm : Algorithm = Algorithm.NONE with get,set

    /// for read operations, this will get all the information about the value except for the
    /// value itself. This is valuable for getting the integrity field or the version without also
    /// having to get the data. If this field is not present, it is as if it is false. For
    /// write or delete operations, if this is set, the command is rejected.
    [<ProtoMember(7)>]
    member val MetadataOnly : bool = false with get,set

    /// Synchronization allows the puts and deletes to determine if they are to be
    /// SYNC: This request is made persistent before returning. This does not effect any other pending operations.
    /// ASYNC: They can be made persistent when the drive chooses, or when a subsequent FLUSH is give to the drive.
    /// FLUSH: All pending information that has not been written is pushed to the disk and the command that
    ///        specifies FLUSH is written last and then returned. All ASYNC writes that have received ending
    ///        status will be guaranteed to be written before the FLUSH operation is returned completed.
    [<ProtoMember(9)>]
    member val Synchronization : Synchronization = Synchronization.INVALID_SYNCHRONIZATION with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type Range() = class end


[<ProtoContract>]
[<AllowNullLiteral>]
type Setup() = class end


[<ProtoContract>]
[<AllowNullLiteral>]
type P2POperation() = class end


[<ProtoContract>]
[<AllowNullLiteral>]
type GetLog() = class end


[<ProtoContract>]
[<AllowNullLiteral>]
type Security() = class end


[<ProtoContract>]
[<AllowNullLiteral>]
type Body() = 

    ///key/value op
    [<ProtoMember(1)>]
    member val KeyValue : KeyValue = null with get,set
   
    ///range operation
    [<ProtoMember(2)>]
    member val Range : Range = null with get,set

    ///set up opeartion
    [<ProtoMember(3)>]
    member val Setup : Setup = null with get,set
   
    /// EXPERIMENTAL!
    ///
    /// The following is incomplete and evolving. Implement at your own risk.
    ///
    /// Peer to Peer operations.
    [<ProtoMember(4)>]
    member val P2POperation : P2POperation = null with get,set

    ///GetLog
    [<ProtoMember(6)>]
    member val GetLog : GetLog = null with get,set

    ///set up security
    [<ProtoMember(7)>]
    member val Security : Security = null with get,set


//enum of status code
type StatusCode = 
    | NOT_ATTEMPTED = 0
    | SUCCESS = 1
    | HMAC_FAILURE = 2
    | NOT_AUTHORIZED = 3
    | VERSION_FAILURE = 4
    | INTERNAL_ERROR = 5
    | HEADER_REQUIRED = 6
    | NOT_FOUND = 7
    | VERSION_MISMATCH = 8

    /// If there are too many requests in the device at this time, requests
    /// will be rejected with this error message. The common response is to
    /// wait and retry the operation with an exponential back-off.
    | SERVICE_BUSY = 9

    /// A long operation was started and a timeout happened mid operation. This
    /// does not imply a failure.
    | EXPIRED = 10

    /// A data error happened and either fastFail was set or the timeout happened.
    | DATA_ERROR = 11

    /// A data error happened and all possible error recovery operations have been
    /// performed. There is no value to trying this again. If the system has the ability
    /// to determine the correct information, writing the data again can get rid
    | PERM_DATA_ERROR = 12

    /// A TCP connection to the remote peer failed. This is only for the P2P Operation
    | REMOTE_CONNECTION_ERROR = 13

    /// When the drive is full, it returns this error. The background scrubbing may free space,
    /// so this error may go away
    | NO_SPACE = 14

    /// In the set security, an HmacAlgorithm was specified as Unknown or there is a protocol
    /// version mis-match
    | NO_SUCH_HMAC_ALGORITHM = 15
  
       
/// operation status
[<ProtoContract>]
[<AllowNullLiteral>]
type Status() =
  
    /// status code
    [<ProtoMember(1)>]
    member val Code : StatusCode = StatusCode.NOT_ATTEMPTED with get,set

    /// status message
    [<ProtoMember(2)>] 
    member val StatusMessage : string = null with get,set

    /// optional information comes with status
    [<ProtoMember(3)>] 
    member val DetailedMessage : bytes = null with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type Command() =
    [<ProtoMember(1)>]
    member val Header : Header = null with get,set
    [<ProtoMember(2)>]
    member val Body : Body = null with get,set
    [<ProtoMember(3)>]
    member val Status : Status = null with get,set


[<ProtoContract>]
type Message() =
    [<ProtoMember(1)>]
    member val Command : Command = null with get,set
    [<ProtoMember(2)>]
    member val Value : bytes = null with get,set
    [<ProtoMember(3)>]
    member val Hmac : bytes = null with get,set
