﻿// Copyright (c) 2015 Ignacio Corderi

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// author: Ignacio Corderi

namespace Kinetic.Proto

open ProtoBuf
open System.Collections.Generic
open System.ComponentModel

type bytes = byte array

/// Operation code
type MessageType =
    | INVALID_MESSAGE_TYPE = -1
    | GET = 2 
    | GET_RESPONSE = 1
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
    // 13 and 14 are reserved, do not use
    | GETVERSION = 16
    | GETVERSION_RESPONSE = 15
    // 17, 18, 19, and 20 are reserved, do not use
    | SETUP = 22
    | SETUP_RESPONSE = 21
    | GETLOG = 24
    | GETLOG_RESPONSE = 23
    | SECURITY = 26
    | SECURITY_RESPONSE = 25
    | PEER2PEERPUSH = 28
    | PEER2PEERPUSH_RESPONSE = 27   
    | NOOP = 30
    | NOOP_RESPONSE = 29
    | FLUSHALLDATA = 32
    | FLUSHALLDATA_RESPONSE = 31
    | BACKOP = 34
    | BACKOP_RESPONSE = 33
    | PINOP = 36
    | PINOP_RESPONSE = 35


type Priority =
    | NORMAL = 5
    | LOWEST = 1
    | LOWER = 3
    | HIGHER = 7
    | HIGHEST = 9
   

[<ProtoContract>]
[<AllowNullLiteral>]
type Header() = 
    
    /// "cluster" is the version number of the cluster definition. If this is incompatible,
    /// the request is rejected. If it is missing, it is assumed to be 0. (0 allows systems not
    /// using cluster vesioning to ignore this field in the header and in the setup.)
    [<ProtoMember(1)>]
    member val ClusterVersion : int64 = 0L with get,set

    // 2 is reserved, do not use

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
    member val MessageType = MessageType.INVALID_MESSAGE_TYPE with get,set

    /// Request timeout (in ms). This is the amount of time that this request should take. If this timeout
    /// is triggered, there are three possible results that can be returned.
    ///     - SERVICE_BUSY meaning that the request was still on the queue waiting to be executed
    ///     - EXPIRED meaning that a long running operation was stopped because the time expired.
    ///     - DATA_ERROR meaning that the request was in process, but that the error recovery was not
    ///                  complete at the time that the time expired
    [<ProtoMember(9)>]
    member val Timeout : int64 = 0L with get,set

    /// If true, requests will not attempt multi revolution recoveries even if the timeout has not occurred.
    /// In this case the result will be DATA_ERROR. To have the drive exhaust all possible error recovery, leave
    /// this field off or set to false, and make sure that the timeout is set to be longer than any possible queue
    /// time and error recovery time. On a disk drive, the maximum error recovery time could be seconds.
    /// Once all possible data recovery operations are complete and have not succeeded, PERM_DATA_ERROR will be
    /// returned.
    [<ProtoMember(10)>]
    member val EarlyExit : bool = false with get,set

    // 11 is reserved

    /// Priority is a simple integer that determines the priority of this
    /// request. All activity at a higher priority will execute before that
    /// of lower priority traffic. A higher number is higher priority.
    [<DefaultValue(Priority.NORMAL)>]
    [<ProtoMember(12)>]
    member val Priority = Priority.NORMAL with get,set

    /// A hint of how long a job should run before yielding. Specified in
    /// miliseconds. A value of 0 indicates that the operation can perform one
    /// sub operation and then check to see if there are other sub higher
    /// priority operations. An example of a sub-operation might be a single put
    /// in a P2P operation, etc.
    [<ProtoMember(13)>]
    member val TimeQuanta : int64 = 0L with get,set


type Synchronization =
    | INVALID_SYNCHRONIZATION = -1
    /// Synchronouse write
    | WRITETHROUGH = 1
    /// Asynchronouse write
    | WRITEBACK = 2
    /// Wait for everything 
    | FLUSH = 3


type Algorithm =
    | INVALID_ALGORITHM = -1
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
    [<DefaultValue(Algorithm.INVALID_ALGORITHM)>]
    [<ProtoMember(6)>]
    member val Algorithm = Algorithm.INVALID_ALGORITHM with get,set

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
    [<DefaultValue(Synchronization.INVALID_SYNCHRONIZATION)>]
    [<ProtoMember(9)>]
    member val Synchronization = Synchronization.INVALID_SYNCHRONIZATION with get,set


/// Key range operation
[<ProtoContract>]
[<AllowNullLiteral>]
type Range() =

    [<ProtoMember(1)>]
    member val StartKey : bytes = null with get,set

    [<ProtoMember(2)>]
    member val EndKey : bytes = null with get,set

    [<ProtoMember(3)>]
    member val StartKeyInclusive : bool = false with get,set

    [<ProtoMember(4)>]
    member val EndKeyInclusive : bool = false with get,set

    /// The maximum number of keys returned
    [<ProtoMember(5)>]
    member val MaxReturned : int = 0 with get,set

    /// The keys are searched for and returned in a reverse order.
    [<ProtoMember(6)>]
    member val Reverse : bool = false with get,set

    // 7 is reserved

    /// Range of keys
    [<ProtoMember(8)>]
    member val Keys  = new List<bytes>() with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type Setup() = class end


[<ProtoContract>]
[<AllowNullLiteral>]
type P2POperation() = class end


type LogType =
    | INVALID_TYPE = -1
    | UTILIZATIONS = 0
    | TEMPERATURES = 1
    | CAPACITIES = 2
    | CONFIGURATION = 3
    | STATISTICS = 4
    | MESSAGES = 5
    | LIMITS = 6
    | DEVICE = 7


[<ProtoContract>]
[<AllowNullLiteral>]
type Utilization() =
    
    /// The name of the utilization being reported. These names can be standard and proprietary. The
    /// standard names are "HDA", "EN0" and "EN1". If there are more items that are
    /// being reported, such as processor utilization, can have a descriptive name.
    [<ProtoMember(1)>]
    member val Name : string = null with get,set

    /// A number between 0.00 and 1.00. The resolution of this number is up to the
    /// drive. 1 means 100% utilized.
    [<ProtoMember(2)>]
    member val Value : float = 0. with get,set

       
[<ProtoContract>]
[<AllowNullLiteral>]
type Temperature() =
    
    /// The name of the temperature being reported. These names can be standard and proprietary. The
    /// standard name is "HDA". If there are more items that are
    /// being reported, such as processor temperature, can have a descriptive name.
    [<ProtoMember(1)>]
    member val Name : string = null with get,set

    /// The current temperature in degrees c
    [<ProtoMember(2)>]
    member val Current : float = 0. with get,set

    [<ProtoMember(2)>]
    member val Minimum : float = 0. with get,set

    [<ProtoMember(2)>]
    member val Maximum : float = 0. with get,set

    [<ProtoMember(2)>]
    member val Target : float = 0. with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type Capacity() =

    // 1-3 are reserved

    /// These capacities are in bytes.
    [<ProtoMember(4)>]
    member val NominalCapacityInBytes : System.UInt64 = 0UL with get,set

    /// A number between 0.00 and 1.00. The resolution of this number is up to the
    /// drive. 1 means 100% utilized.
    [<ProtoMember(5)>]
    member val PortionFull : float = 0. with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type Interface() =

    [<ProtoMember(1)>]
    member val Name : string = null with get,set

    /// Defined as bytes on the protocol
    [<ProtoMember(2)>]
    member val MAC : string = null with get,set

    /// Defined as bytes on the protocol
    [<ProtoMember(3)>]
    member val ipv4Address : string = null with get,set

    /// Defined as bytes on the protocol
    [<ProtoMember(4)>]
    member val ipv6Address : string = null with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type Configuration() =

    // 1-4 are reserved

    /// name of the vendor. For example "Seagate"
    [<ProtoMember(5)>]
    member val Vendor : string = null with get,set

    /// The model of the device.
    /// "Simulator" for the simulator.
    [<ProtoMember(6)>]
    member val Model : string = null with get,set

    /// Device Serial number (SN)
    /// Defined as bytes on the protocol
    [<ProtoMember(7)>]
    member val SerialNumber : string = null with get,set 

     /// Device world wide name (WWN)
     /// Defined as bytes on the protocol
    [<ProtoMember(14)>]
    member val WorldWideName : string = null with get,set 

    /// This is the vendor specific version of the software on the drive in dot notation
    /// if this is not set or ends with "x" this is test code.
    [<ProtoMember(8)>]
    member val Version : string = null with get,set

    [<ProtoMember(12)>]
    member val CompilationDate : string = null with get,set
     
    [<ProtoMember(13)>]
    member val SourceHash : string = null with get,set

    /// This is the version of the protocol (.proto file) that the drive uses.
    /// This is not the highest or lowest version that is supported, just
    /// the version that was compiled.
    [<ProtoMember(15)>]
    member val ProtocolVersion : string = null with get,set

    [<ProtoMember(16)>]
    member val ProtocolCompilationDate : string = null with get,set

    [<ProtoMember(17)>]
    member val ProtocolSourceHash : string = null with get,set

    /// the interfaces for this device. one per interface.
    [<ProtoMember(9)>]
    member val Interfaces : List<Interface> = new List<Interface>() with get,set

    /// these are the port numbers for the software
    [<ProtoMember(10)>]
    member val Port : int = 0 with get,set

    [<ProtoMember(11)>]
    member val TlsPort : int = 0 with get,set

/// These numbers start at 0 when the drive starts up and never wraps or resets.
[<ProtoContract>]
[<AllowNullLiteral>]
type Statistics() =

    [<ProtoMember(1)>]
    member val MessageType = MessageType.INVALID_MESSAGE_TYPE with get,set

    // 2 and 3 are reserved, do not use

    [<ProtoMember(4)>]
    member val Count : System.UInt64 = 0UL with get,set

    /// This is the sum of the data that is in the data portion. This does not include t
    /// the command description. For P2P operations, this is the amount of data moved between
    /// drives
    [<ProtoMember(5)>]
    member val Bytes : System.UInt64 = 0UL with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type DeviceLimits() =

    [<ProtoMember(1)>]
    member val MaxKeySize : int = 0 with get,set

    [<ProtoMember(2)>]
    member val MaxValueSize : int = 0 with get,set
   
    [<ProtoMember(3)>]
    member val MaxVersionSize : int = 0 with get,set

    [<ProtoMember(4)>]
    member val MaxTagSize : int = 0 with get,set

    [<ProtoMember(5)>]
    member val MaxConnections : int = 0 with get,set

    [<ProtoMember(6)>]
    member val MaxOutstandingReadRequests : int = 0 with get,set

    [<ProtoMember(7)>]
    member val MaxOutstandingWriteRequests : int = 0 with get,set

    [<ProtoMember(8)>]
    member val MaxMessageSize : int = 0 with get,set

    [<ProtoMember(9)>]
    member val MaxKeyRangeCount : int = 0 with get,set

    [<ProtoMember(10)>]
    member val MaxIdentityCount : int = 0 with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type DeviceLog() =

    /// The Device GetLog message is to ask the device to send back the
    /// log of a certain name in the value field. The limit of each
    /// log is 1m byte.
    ///
    /// Proprietary names should be prefaced by the vendor name so that name
    /// collisions do not happen in the future. An example could be names that
    /// start with “com.WD” would be for Western Digital devices.
    ///
    /// If the name is not found, the get log returns NOT_FOUND.
    ///
    /// There can be only one Device in the list of logs that can be retrieved.!
    /// Defined as bytes on the protocol
    [<ProtoMember(1)>]
    member val Name : string = null with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type GetLog() = 
  
    [<ProtoMember(1)>]
    member val Types : List<LogType> = new List<LogType>() with get,set

    [<ProtoMember(2)>]
    member val Utilizations : List<Utilization> = new List<Utilization>() with get,set

    [<ProtoMember(3)>]
    member val Temperatures : List<Temperature> = new List<Temperature>() with get,set

    [<ProtoMember(4)>]
    member val Capacity : Capacity = null with get,set

    [<ProtoMember(5)>]
    member val Configuration : Configuration = null with get,set

    [<ProtoMember(6)>]
    member val Statistics : List<Statistics> = new List<Statistics>() with get,set

    /// Defined as bytes on the protocol
    [<ProtoMember(7)>]
    member val Messages : string = null with get,set

    [<ProtoMember(8)>]
    member val Limits : DeviceLimits = null with get,set

    [<ProtoMember(9)>]
    member val Device : DeviceLog = null with get,set
      

[<ProtoContract>]
[<AllowNullLiteral>]
type Security() = class end


type BackgroundOperationType =
    | INVALID_BACKOP = -1

    /// Media scan is to check that the user data is readable, and
    /// if the end to end integrity is known to the device, if the
    /// end to end integrity field is correct.
    | MEDIASCAN = 1

    /// This performs optimizations of the media. Things like
    /// defragmentation, compaction, garbage collection, compression
    /// could be things accomplished using the media optimize command.
    | MEDIAOPTIMIZE = 2


/// These are long running background operations. The value of the
/// system controlling background operations is that the system
/// can make sure that during high performance periods that
/// background operations are not being performed.
[<ProtoContract>]
[<AllowNullLiteral>]
type BackgroundOperation() = 
   
    /// Determine the operation type
    [<ProtoMember(1)>]
    member val BackgroundOperationType = BackgroundOperationType.INVALID_BACKOP with get,set

    /// The range on the request and results on the response
    [<ProtoMember(2)>]
    member val Range : Range = null with get,set


type PinOperationType = 
    | INVALID_PINOP = -1

    /// The pin will unlock the device
    | UNLOCK_PINOP = 1

    /// This will lock the device. This includes all
    /// configuration and user data. This operation is
    /// secure from even given physical access and
    /// disassembly of the device.
    | LOCK_PINOP = 2

    /// Both erase operations will return
    /// the device to an as manufactured state removing all
    /// user data and configuration settings.

    /// Erase the device. This may be secure
    /// or not. The implication is that it may be faster
    /// than the secure operation.
    /// Will return
    /// the device to an as manufactured state removing all
    /// user data and configuration settings.
    | ERASE_PINOP = 3

    /// Erase the device in a way that will
    /// physical access and disassembly of the device
    /// will not.
    /// Will return
    /// the device to an as manufactured state removing all
    /// user data and configuration settings.
    | SECURE_ERASE_PINOP = 4


[<ProtoContract>]
[<AllowNullLiteral>]
type PinOperation() = 
  
    /// Pin Operations are used for special commands that are valid when the device
    /// is locked or to be locked. These are unlock, lock and erase.
    /// This must come over the TLS connection to protect the confidentiality and
    /// integrity. This operations must be used with PinAuth.    
    [<ProtoMember(1)>]
    member val PinOperationType = PinOperationType.INVALID_PINOP with get,set


[<ProtoContract>]
[<AllowNullLiteral>]
type Body() = 

    /// key/value operations
    [<ProtoMember(1)>]
    member val KeyValue : KeyValue = null with get,set
   
    /// range operations
    [<ProtoMember(2)>]
    member val Range : Range = null with get,set

    /// set up opeartions
    [<ProtoMember(3)>]
    member val Setup : Setup = null with get,set
   
    /// Peer to Peer operations
    [<ProtoMember(4)>]
    member val P2POperation : P2POperation = null with get,set

    /// Perform background operations
    [<ProtoMember(5)>]
    member val BackgroundOperation : BackgroundOperation = null with get,set

    /// Log operations
    [<ProtoMember(6)>]
    member val GetLog : GetLog = null with get,set

    /// Security operations
    [<ProtoMember(7)>]
    member val Security : Security = null with get,set

    /// Perform Pin-based operations
    [<ProtoMember(8)>]
    member val PinOperation : PinOperation = null with get,set


//enum of status code
type StatusCode = 
    /// Don't ask...
    | INVALID_STATUS_CODE = -1
    /// For a P2P operation, there was a reason the list was incomplete. This is for items
    /// that were not attempted.
    | NOT_ATTEMPTED = 0
    /// We have a winner!
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

    /// The request is not valid. Subsequent attempts with the same request will return the same code.
    /// Examples: GET does not specify keyValue message, GETKEYRANGE operation does not specify startKey, etc
    | INVALID_REQUEST = 16

    /// For P2P Requests, the operation was executed successfully but some nested operations
    /// did not succeed. This indicates that callers should review the status of nested operations.
    /// This status should only be used in the Command > Status, not in the Status messages
    /// of nested P2POperations
    | NESTED_OPERATION_ERRORS = 17
  
       
/// operation status
[<ProtoContract>]
[<AllowNullLiteral>]
type Status() =
  
    /// status code
    [<ProtoMember(1)>]
    member val Code = StatusCode.NOT_ATTEMPTED with get,set

    /// status message
    [<ProtoMember(2)>] 
    member val StatusMessage : string = null with get,set

    /// optional information comes with status
    [<ProtoMember(3)>] 
    member val DetailedMessage : bytes = null with get,set

    override this.ToString() =
        sprintf "%A %s" this.Code this.StatusMessage


[<ProtoContract>]
[<AllowNullLiteral>]
type Command() =

    [<ProtoMember(1)>]
    member val Header : Header = null with get,set

    [<ProtoMember(2)>]
    member val Body : Body = null with get,set

    [<ProtoMember(3)>]
    member val Status : Status = null with get,set


/// The Message Type determines how the the message is to be processed.
type AuthenticationType =
    /// If the message type is unknown, close the connection
    | INVALID_AUTH_TYPE = -1

    /// This is for normal traffic. Check the HMAC of the command and
    /// if correct, process the command.
    | HMACAUTH = 1

    /// device unlock and ISE command. These must come over the TLS connection.
    /// If they do not, close the connection. If it is over
    /// the TLS connection, execute the pin operation.
    | PINAUTH = 2

    /// In the event that the device is going to close the connection, an
    /// unsolicited status will be returned first.
    | UNSOLICITEDSTATUS = 3


/// This is for normal message to the device
/// and for responses. These are allowed once the
/// device is unlocked. The HMAC provides for
/// authenticity, Integrity and to enforce roles.
[<ProtoContract>]
[<AllowNullLiteral>]
type HmacAuthentication() =

    /// The "identity" identifies the requester and the key and algorithm to
    /// be used for hmac.
    [<ProtoMember(1)>]
    member val Identity : int64 = 0L with get,set

    [<ProtoMember(2)>]
    member val Hmac : bytes = null with get,set


/// Pin based authentication for Pin operations.
[<ProtoContract>]
[<AllowNullLiteral>]
type PinAuthentication() =
       
    /// The pin necessary to make the operations valid
    [<ProtoMember(1)>]
    member val Pin : bytes = null with get,set


[<ProtoContract>]
type Message() =

    // 1-3 are reserved, do not use

    /// Every message must be one of the following types.
    [<ProtoMember(4)>]
    member val AuthenticationType = AuthenticationType.INVALID_AUTH_TYPE with get,set
  
    /// Normal messages
    [<ProtoMember(5)>]
    member val HmacAuthentication : HmacAuthentication = null with get,set

    /// For Pin based operations. These include device unlock and
    /// device erase
    [<ProtoMember(6)>]
    member val PinAuthentication : PinAuthentication = null with get,set

    /// The embedded message providing the request (for HMACauth) and
    /// the response (for all auth types).
    [<ProtoMember(7)>]
    member val CommandBytes : bytes = null with get,set

