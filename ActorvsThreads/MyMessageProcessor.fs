/// Implementation of the following protokoll: 
/// sending messages with: <id> <command> <parameter> RAWDATA <bytecount>\0<rawbytes>
/// or <id> <command> <parameter>\0
/// \ has to be escaped to \\ in <parameter>, <command>
/// " " has to be escaped to "\ " in <parameter>, <command>
/// \0 has to be escaped in "\0" everywhere but <rawbytes>
/// <id> is a unique byte array with all bytes > 0x20 (which is the space character)
///
/// The Parter can answer a command with 
/// <id> _ <parameter> RAWDATA <bytecount>\0<rawbytes>
/// or
/// <id> _ <parameter>\0
/// Where <id> is the same as the matching first message

module MyMessageProcessor

open Yaaf.Utils.Helper
open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Diagnostics

open SocketHelper

// Creates a mailbox that synchronizes printing to the console (so 
// that two calls to 'printfn' do not interleave when printing)
let printer = 
    MailboxProcessor.Start(fun x -> 
        let rec loop () =
            async {
                let! str = x.Receive()
                printfn "%s" str
                return! loop()
            }
        loop()
       )
// Hides standard 'printfn' function (formats the string using 
// 'kprintf' and then posts the result to the printer agent.
let myprint fmt = 
  Printf.kprintf printer.Post fmt


type RawData = {
    Bytes : byte array
    Size  : int
    }

type ClientMessageReceived = {
    Id : byte[]
    Command : string
    Parameter : StringParameter
    RawData : AsyncReplyChannel<MailboxProcessor<byte seq>>
    }

//type SendRawBytesMessage = 
//    | Bytes
type ClientMessageSent = {
    Id : byte[]
    Command : string
    Parameter : StringParameter
    RawData : MailboxProcessor<byte seq> -> unit
    }

let NoRawDataSent = 
    (fun (mailbox:MailboxProcessor<byte seq>) -> mailbox.Post(Array.zeroCreate 0))

let buffersize = 1024

type AnalysedLine = {
    Line : string
    Id : byte array
    ReadBytes : int
    }

let parseInt str =
    match System.Int32.TryParse str with
    | (true, result) -> Some result
    | (false, _) -> None

let analyseCommandLine (commandLine:byte array) = 
    let id = commandLine 
                |> Seq.takeWhile (fun b -> b > byte(0x20))
                |> Seq.toArray
    let other = commandLine
                |> Seq.skip (id.Length + 1) // Ignore id and the first ' ' (sep between id & command)
                |> Seq.takeWhile (fun b -> b <> byte(0)) // until First null byte
                |> Seq.toArray
    let line = System.Text.Encoding.UTF8.GetString(other)
    let splits = line.Split(' ')
    if (splits.Length < 3 || splits.[splits.Length - 2] <> "RAWDATA" ) then 
        { Line = line; Id = id; ReadBytes = 0 } 
    else
        match (parseInt splits.[splits.Length - 1]) with
        | None -> failwith "invalid rawbytecount data"
        | Some(readBytes) -> 
            {Line = System.String.Join(" ", splits |> Seq.take (splits.Length - 2)); Id  = id; ReadBytes = readBytes}  

let findNextSeperatorSpace (line:System.String) = 
    let rec findNext index = 
        if index + 1 >= line.Length then -1
        else 
            match line.[index] with
            | '\\' -> findNext (index+2) // Ignore 1
            | ' ' -> index
            | _ -> findNext (index+1)
    findNext 0

let rawUnescape (s:System.String) = s.Replace("\\ ", " ").Replace("\\\\", "\\");
let rawEscape (s:System.String) = s.Replace( "\\", "\\\\").Replace(" ", "\\ ");

let getParameterAndCommand line = 
    let sepSpace = findNextSeperatorSpace line
    if sepSpace = -1 then failwith "The Raw line doesn't contain a secound separation space"
    rawUnescape (line.Substring(0, sepSpace)), rawUnescape (line.Substring(sepSpace + 1))
    

type internal ClientProcessorMessageInternal = 
     | Data of byte array * int * MailboxProcessor<ClientMessageReceived>
     | DisconnectInternal

type ClientProcessorMessage = 
     | SendMessage of ClientMessageSent
     | Disconnect

/// Creates a new Mailbox processor which parses the incomming data stream to messages
let internal createInternalClientProcessorMailbox() = new MailboxProcessor<ClientProcessorMessageInternal>(fun inbox -> 
    /// Waits for data and buffers the data in the memorystream
    /// maybeRawReceiver indicates whether we are receiving raw data at the moment
    let rec waitForDataLoop (mem:MemoryStream) count readBytes (maybeRawReceiver:Option<MailboxProcessor<byte seq>>) = 
        async {
            let! msg = inbox.Receive()
            match msg with
            | DisconnectInternal ->
                return ()
            | Data(buffer, receivedBytes, receiver) ->
                if (readBytes > 0) then
                    match maybeRawReceiver with
                    | None -> failwith "Invalid state: No RawProcessor"
                    | Some(rawReceiver) ->
                        let takeNow = Math.Min(readBytes, receivedBytes)
                        rawReceiver.Post(buffer |> Seq.take takeNow)
                        let bytesRemaining = readBytes - takeNow
                        let newReceiver = if bytesRemaining = 0 then None else maybeRawReceiver
                        return! waitForDataLoop mem (count+1) bytesRemaining newReceiver
                else    
                    match maybeRawReceiver with 
                    | Some(_) -> failwith "invalid state (rawprocessor available)"
                    | None ->
                        match (buffer 
                            |> Seq.take receivedBytes
                            |> Seq.tryFindIndex (fun b -> b = byte(0))) with
                        | None -> 
                            // Line is not complete so go ahaid and buffer it.
                            do! mem.AsyncWrite(buffer, 0, receivedBytes)
                            return! waitForDataLoop mem count 0 maybeRawReceiver
                        | Some(nullIndex) -> // Found exit (Line complete but there could be very well some raw data)
                            do! mem.AsyncWrite(buffer, 0, nullIndex) 
                            let line = analyseCommandLine (mem.ToArray())
                            mem.Dispose()
                            let command, parameter = getParameterAndCommand line.Line
                            let! rawMailboxProcessorOption = receiver.PostAndTryAsyncReply((fun replyChannel ->
                                        {Id = line.Id; Command = command; Parameter = new StringParameter(parameter); RawData = replyChannel }), 50)
                            let rawMailboxProcessor = 
                                match rawMailboxProcessorOption with 
                                | Some(s) -> s
                                // Default ignore every byte
                                | None -> 
                                    MailboxProcessor.Start(fun inbox ->
                                                    let rec loop () =
                                                        async {
                                                            let! bytes = inbox.Receive()
                                                            if bytes |> Seq.isEmpty then return ()
                                                            else 
                                                                return! loop()
                                                        }
                                                    loop())
                            let rawDataSendNow = Math.Min(line.ReadBytes, receivedBytes - (nullIndex + 1))
                            if line.ReadBytes > 0 then
                                rawMailboxProcessor.Post(buffer |> Seq.skip (nullIndex + 1) |> Seq.take rawDataSendNow)
                            myprint "processed message %d" count
                            let newReadBytes = line.ReadBytes - rawDataSendNow
                            let newRaw = if newReadBytes = 0 then None else Some(rawMailboxProcessor)
                            return! waitForDataLoop (new MemoryStream()) (count + 1) newReadBytes newRaw 
        }
                        
    waitForDataLoop (new MemoryStream()) 0 0 None
    )



/// Some Messages to control the server
type ServerProcessorMessage =
    | Stop
    | Start

/// Notifies about events on the server
type ServerReceiverMessage = 
    | ClientConnected of AsyncReplyChannel<MailboxProcessor<ClientMessageReceived>> * MailboxProcessor<ClientProcessorMessage>

type internal ServerProcessorMessageInternal = 
     | StartInternal
     | StopInternal
     | ClientAccepted of Socket

type internal InternalSendCommands = 
    | StartSendRaw of AsyncReplyChannel<unit>
    | RawData of byte seq 

let internal createClientProcessor (internalClientProcessor:MailboxProcessor<_>) (client:Socket) = 
    /// Processor for client actions
    new MailboxProcessor<ClientProcessorMessage>(fun inbox ->
        let rec waitForDataLoop() = 
            async { 
                let! msg = inbox.Receive()
                match msg with 
                | Disconnect ->
                    internalClientProcessor.Post(DisconnectInternal)
                | SendMessage(m) -> 
                    myprint "preparing to send message: %s" m.Command
                    let line = (rawEscape m.Command) + " " + (rawEscape m.Parameter.Parameter)
                    let firstBytes = System.Text.Encoding.UTF8.GetBytes(line)
                    let buffer = Array.zeroCreate (firstBytes.Length + m.Id.Length + 2)
                    Array.Copy(m.Id, buffer, m.Id.Length)
                    buffer.[m.Id.Length] <- byte(0x20)
                    Array.Copy(firstBytes, 0, buffer, m.Id.Length + 1, firstBytes.Length)
                    let! sent = client.MySendAsync(buffer, SocketFlags.None)
                    System.Diagnostics.Debug.Assert( (sent = buffer.Length) )
                    
                    let internalSendBox =  MailboxProcessor.Start(fun inbox -> 
                        async {
                            let! start = inbox.Receive()
                            let repl =
                                match start with
                                | StartSendRaw repl -> repl
                                | _ -> failwith "Expected Start message!"

                            let rec sendLoop() = 
                                async {
                                    let! msg = inbox.Receive() 
                                    match msg with
                                    | StartSendRaw repl -> failwith "Expected Start message!"
                                    | RawData bytes -> 
                                        let bytesToSend = bytes |> Seq.toArray
                                        if (bytesToSend.Length = 0) then repl.Reply()
                                        else 
                                            let! sent = client.MySendAsync(bytesToSend, SocketFlags.None)
                                            return! sendLoop()
                                }
                            return! sendLoop()
                        })

                    let sendMailbox = MailboxProcessor.Start(fun inbox -> 
                        async {
                            while true do 
                                let! msg = inbox.Receive() 
                                internalSendBox.Post(RawData msg)
                        })
                    let waitForFinished = internalSendBox.PostAndAsyncReply(fun replyChannel -> StartSendRaw(replyChannel))
                    m.RawData(sendMailbox)
                    do! waitForFinished
                    return! waitForDataLoop()
            }
        waitForDataLoop()
        )

/// Handles the raw byte stream
let internal createByteProcessor (internalClientProcessor:MailboxProcessor<_>) (client:Socket) (res:MailboxProcessor<_>)= 
    new MailboxProcessor<_>(fun inbox -> 
        async { 
            while true do
                let buffer = Array.zeroCreate buffersize
                let! receivedBytes = client.MyReceiveAsync(buffer, SocketFlags.None)
                internalClientProcessor.Post(Data(buffer, receivedBytes, res))
        })

/// Creates a new MessageServer
let createServer port (clientReceiver:MailboxProcessor<_>) = 
    let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), port))
    
    /// Processor for new clients
    let maybeClientAccepter = ref (None:Option<MailboxProcessor<_>>)
    
    /// Processor which handles all events on the server
    let internalProcessor = 
        new MailboxProcessor<ServerProcessorMessageInternal>(fun inbox -> 
            let rec waitLoop count isStarted = 
                async {
                    let! msg = inbox.Receive()
                    match msg with
                    | StopInternal ->
                        if not isStarted then failwith "Please start the server first!"
                        do! socket.MyDisconnectAsync(true)

                        return! waitLoop 0 false
                    | StartInternal ->
                        if isStarted then failwith "Please stop the server first!"
                        socket.Listen(10)
                        match !maybeClientAccepter with 
                        | None -> failwith "clientAccepter not initialized (should not happen)"
                        | Some(clientAccepter) -> clientAccepter.Start()
                        myprint "Server started, waiting for clients!"
                        return! waitLoop count true
                    | ClientAccepted(client) ->
                        if not isStarted then failwith "Please start the server first!"
                        myprint "Client %d connected" count
                        /// The internal processing
                        let internalClientProcessor = createInternalClientProcessorMailbox()
                        internalClientProcessor.Error.Add(fun ex -> Console.WriteLine("Exception: {0}", ex))
                        
                        /// Processing for user defined inputs
                        let clientProcessor = createClientProcessor internalClientProcessor client
                
                        /// Ask user to reply a handler for this client
                        let! res = clientReceiver.PostAndAsyncReply(fun replyChannel -> ClientConnected(replyChannel, clientProcessor))

                        /// Processor for incomming bytes
                        let byteReceiver = createByteProcessor internalClientProcessor client res
                        byteReceiver.Error.Add(fun ex -> Console.WriteLine("Exception: {0}", ex))

                        internalClientProcessor.Start()
                        clientProcessor.Start()
                        byteReceiver.Start()
                        return! waitLoop (count + 1) true
                }
            waitLoop 1 false
        )

    maybeClientAccepter := Some(
        new MailboxProcessor<_>(fun inbox ->
            async {
                while true do
                    let! client = socket.MyAcceptAsync()
                    internalProcessor.Post(ClientAccepted(client))
            }
        ))
        
    maybeClientAccepter.Value.Value.Error.Add(fun ex -> Console.WriteLine("Exception: {0}", ex))

    /// Processor used by the user
    let externalProcessor = 
        new MailboxProcessor<ServerProcessorMessage>(fun inbox -> 
            let rec waitLoop() = 
                async {
                    let! msg = inbox.Receive()
                    match msg with
                    | Start ->
                        internalProcessor.Post(StartInternal)
                    | Stop ->
                        internalProcessor.Post(StopInternal)
                    return! waitLoop()
                }
            waitLoop()
        )
    internalProcessor.Error.Add(fun ex -> Console.WriteLine("Exception: {0}", ex))
    
    internalProcessor.Start()
    externalProcessor.Start()
    externalProcessor

let connectToServer (ep:IPEndPoint) (receiver:MailboxProcessor<ClientMessageReceived>) = 
    async {
        let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        do! socket.MyConnectAsync(ep.Address, ep.Port)
        let internalProcessor = createInternalClientProcessorMailbox()
        internalProcessor.Error.Add(fun ex -> Console.WriteLine("Exception: {0}", ex))

        let processor = createClientProcessor internalProcessor socket

        /// Processor for incomming bytes
        let byteReceiver = createByteProcessor internalProcessor socket receiver
        byteReceiver.Error.Add(fun ex -> Console.WriteLine("Exception: {0}", ex))
        internalProcessor.Start()
        processor.Start()
        byteReceiver.Start()
        return processor
    }

