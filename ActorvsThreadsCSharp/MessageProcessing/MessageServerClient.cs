namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;

    using Yaaf.Utils.Helper;
    using Yaaf.Utils.Logging;

    public class MessageServerClient
    {
        public static int CurrentId;


        private readonly Encoding encoding;

        private readonly Dictionary<RawLineId, List<Action<ParsedLineEventArgs>>> onAnswerDelegates =
            new Dictionary<RawLineId, List<Action<ParsedLineEventArgs>>>();

        private readonly SafeCommandReader reader;

        private readonly Queue<SendPacket> sendQueue = new Queue<SendPacket>();

        private readonly object sendSync = new object();

        private SendPacket currentPacket;

        private bool isConnected = true;

        private RawLineId sendCounter = new RawLineId();

        public MessageServerClient(Socket socket)
        {
            this.encoding = Encoding.UTF8;
            this.Id = CurrentId++;
            this.Socket = socket;

            this.reader = new SafeCommandReader(1024 * 8); //NOTE max messageSize
            this.Events = new EventHelper();
        }

        private Socket Socket { get; set; }

        public int Id { get; private set; }

        public EventHelper Events { get; private set; }

        public Object UserToken { get; set; }

        private SafeCommandReader Reader
        {
            get
            {
                return this.reader;
            }
        }

        public event EventHandler<RawLineReceivedEventArgs> RawLineReceived;

        public event EventHandler<ParsedLineEventArgs> ParsedLineReceived;

        public event EventHandler<ParsedLineEventArgs> ParsedLineSend;

        public event EventHandler<EventArgs> Disconnected;

        public event EventHandler<ErrorEventArgs> UnknownError;

        private void OnUnknownError(ErrorEventArgs e)
        {
            EventHandler<ErrorEventArgs> handler = this.UnknownError;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void OnDisconnected(EventArgs e)
        {
            EventHandler<EventArgs> handler = this.Disconnected;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void OnParsedLineSend(ParsedLineEventArgs e)
        {
            EventHandler<ParsedLineEventArgs> handler = this.ParsedLineSend;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler<ParsedLineEventArgs> AnswerReceived;

        private void OnAnswerReceived(ParsedLineEventArgs e)
        {
            EventHandler<ParsedLineEventArgs> handler = this.AnswerReceived;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void OnParsedLineReceived(ParsedLineEventArgs e)
        {
            EventHandler<ParsedLineEventArgs> handler = this.ParsedLineReceived;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void OnRawLineReceived(RawLineReceivedEventArgs line)
        {
            EventHandler<RawLineReceivedEventArgs> handler = this.RawLineReceived;
            if (handler != null)
            {
                handler(this, line);
            }
        }

        private void OnProcessData(byte[] buffer, int offset, int count)
        {
            this.Reader.WriteData(buffer, offset, count);

            var lineData = new LineData(null, string.Empty, null);
            while (lineData != null) // ignoring empty lines
            {
                lineData = this.Reader.ReadLine();

                if (lineData != null)
                {
                    if (lineData.Line.Length > 0)
                    {
                        var lineEvent = new RawLineReceivedEventArgs(lineData);
                        var handleRawLine = ((Action<RawLineReceivedEventArgs>)this.HandleRawLine);
                        handleRawLine.BeginInvoke(lineEvent, handleRawLine.EndInvoke, null);
                    }

                    if (lineData.Line.Length == 0)
                    {
                        Debug.Assert(false); // Temp breakpoint
                    }
                }
            }
        }

        private void HandleRawLine(RawLineReceivedEventArgs lineEvent)
        {
            try
            {
                string line = lineEvent.RawLine.Line;
                var rawLineId = lineEvent.RawLine.Id;
                Logger.WriteLine("Received: {0}, with {1}", TraceEventType.Information, line, rawLineId);
            
                this.OnRawLineReceived(lineEvent);
                if (lineEvent.PreventProcessing)
                {
                    return;
                }
                ParsedLineEventArgs parsedLine;
                string afterId = line;
                int secoundSpace = this.FindFirstSeparatingSpace(afterId);
                if (secoundSpace == -1)
                {
                    throw new RawLineFormatException("The Raw line doesn't contain a secound separation space");
                }
                string command = afterId.Substring(0, secoundSpace);
                string parameter = afterId.Substring(secoundSpace + 1);
                command = UnEscapeString(command);
                parameter = UnEscapeString(parameter);

                parsedLine = new ParsedLineEventArgs(
                    rawLineId, command, new StringParameter(parameter), lineEvent.RawLine.RawData);

                this.HandleParsedLineReceived(parsedLine);
            }
            catch (RawLineFormatException e)
            {
                Logger.WriteLine("Received Invalid Line... Disconnecting: {0}", TraceEventType.Error, e);
                this.HandleDisconnect();
            }
            catch (Exception e)
            {
                this.HandleUnknownException(e);
            }
        }

        private void HandleUnknownException(Exception e)
        {
            Logger.WriteLine("Unknown Error: {0}, Disconnecting", TraceEventType.Critical, e);
            this.HandleDisconnect();
            this.OnUnknownError(new ErrorEventArgs(e));
        }

        internal static string UnEscapeString(string toUnescape)
        {
            return toUnescape.Replace("\\ ", " ").Replace("\\\\", "\\");
        }

        private int FindFirstSeparatingSpace(string data)
        {
            bool ignoreNext = false;
            for (int i = 0; i < data.Length; i++)
            {
                if (ignoreNext)
                {
                    ignoreNext = false;
                    continue;
                }
                if (data[i] == '\\')
                {
                    ignoreNext = true;
                }
                if (data[i] == ' ')
                {
                    return i;
                }
            }
            return -1;
        }

        private void HandleParsedLineReceived(ParsedLineEventArgs parsedLine)
        {
            this.OnParsedLineReceived(parsedLine);
            if (parsedLine.PreventProcessing)
            {
                return;
            }
            var eventArgs = new ParameterEventReceivedEventArgs(parsedLine.Parameter, parsedLine.Id);
            this.Events[parsedLine.Command].OnEvent(eventArgs);
            if (eventArgs.PreventProcessing)
            {
                return;
            }

            if (parsedLine.Command == "_")
            {
                this.OnAnswerReceived(parsedLine);
                if (parsedLine.PreventProcessing)
                {
                    return;
                }
                List<Action<ParsedLineEventArgs>> onAnswerList;
                if (this.onAnswerDelegates.TryGetValue(parsedLine.Id, out onAnswerList))
                {
                    foreach (var action in onAnswerList)
                    {
                        action.BeginInvoke(parsedLine, null, null);
                    }
                }
            }
        }

        public void RemoveListener(RawLineId id, Action<ParsedLineEventArgs> listener)
        {
            List<Action<ParsedLineEventArgs>> onAnswerList;

            if (this.onAnswerDelegates.TryGetValue(id, out onAnswerList))
            {
                onAnswerList.Remove(listener);
                if (onAnswerList.Count == 0)
                {
                    this.RemoveAllListener(id);
                }
            }
        }

        public bool RemoveAllListener(RawLineId id)
        {
            return this.onAnswerDelegates.Remove(id);
        }

        private readonly object idLock = new object();
        public void SendCommandAsync(
            string command, 
            StringParameter parameter, 
            Stream rawData = null, 
            Action<ParsedLineEventArgs> onAnswer = null,
            bool disposeStream = false, long dataLengthToSend = -1)
        {
            RawLineId id;
            lock (this.idLock)
            {
                id = this.sendCounter;
                this.sendCounter = this.sendCounter.Inc(); 
            }

            this.SendCommandAsyncPrivate(id, command, parameter, rawData, onAnswer, disposeStream, dataLengthToSend);
        }

        private void SendCommandAsyncPrivate(RawLineId id, string command, StringParameter messageParameter = null, Stream rawData = null, Action<ParsedLineEventArgs> onAnswer = null, bool disposeStream = false, long bytesToSend = -1)
        {
            var eventArgs = new ParsedLineEventArgs(id, command, messageParameter, rawData);
            if (onAnswer != null)
            {
                List<Action<ParsedLineEventArgs>> delList;
                if (!this.onAnswerDelegates.TryGetValue(id, out delList))
                {
                    delList = new List<Action<ParsedLineEventArgs>>();
                    this.onAnswerDelegates.Add(id, delList);
                }
                delList.Add(onAnswer);
            }

            command = EscapeString(command);
            var parameterString = EscapeString(messageParameter == null ? "" : messageParameter.Parameter);
            if (rawData != null && bytesToSend == -1)
            {
                bytesToSend = rawData.Length - rawData.Position;
            }

            string rawDataString = rawData == null ? string.Empty : string.Format(" RAWDATA {0}", bytesToSend);
            string commandLine = string.Format("{0} {1}{2}", command, parameterString, rawDataString);
            Logger.WriteLine("Sending: {0}, with {1}", TraceEventType.Information, commandLine, id);
            byte[] commandBytes = this.encoding.GetBytes(commandLine + "\0");
            byte[] idBytes = id.GetIdBytes();

            var completeCommandBytes = new byte[commandBytes.Length + idBytes.Length + 1];
            Array.Copy(idBytes, 0, completeCommandBytes, 0, idBytes.Length);
            completeCommandBytes[idBytes.Length] = 0x20; // Code for Space
            Array.Copy(commandBytes, 0, completeCommandBytes, idBytes.Length + 1, commandBytes.Length);

            this.EnqueuePacket(
                new SendPacket(completeCommandBytes, eventArgs, rawData, this, disposeStream, bytesToSend));
            lock (this.sendSync)
            {
                if (this.currentPacket == null)
                {
                    this.SendNextPacket();
                }
            }
        }

        private void SendNextPacket()
        {
            lock (this.sendSync)
            {
                if (this.sendQueue.Count > 0)
                {
                    this.currentPacket = this.sendQueue.Dequeue();

                    this.SendAsync(this.currentPacket.BytesToSend);
                }
                else
                {
                    this.currentPacket = null;
                }
            }
        }

        private void EnqueuePacket(SendPacket sendPacket)
        {
            lock (this.sendSync)
            {
                this.sendQueue.Enqueue(sendPacket);
            }
        }

        internal static string EscapeString(string toEscape)
        {
            return toEscape.Replace("\\", "\\\\").Replace(" ", "\\ ");
        }

        private void SendAsync(byte[] sendBuffer, int offset = 0, int count = -1)
        {
            // Write data from buffer to socket asynchronously.
            if (count == -1)
            {
                count = sendBuffer.Length - offset;
            }
            var sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(sendBuffer, offset, count);
            sendEventArgs.Completed += this.SendCompleted;

            if (!this.Socket.SendAsync(sendEventArgs))
            {
                ((EventHandler<SocketAsyncEventArgs>)this.SendCompleted).BeginInvoke(
                    this.Socket, sendEventArgs, null, null);
            }
        }

        private int sendcounter = 0;
        private void SendCompleted(object sender, SocketAsyncEventArgs e)
        {

            try
            {
                this.CheckEventArgs(e);
                Interlocked.Increment(ref this.sendcounter);
                lock (this.sendSync)
                {
                    Interlocked.Decrement(ref this.sendcounter);
                    Debug.Assert(this.sendcounter == 0);
                    SendPacket packet = this.currentPacket;
                    if (packet.RawData == null)
                    {
                        this.OnParsedLineSend(this.currentPacket.EventArgs);
                        this.SendNextPacket();
                    }
                    else
                    {
                        // Send Raw Data
                        var readBuffer = new byte[4 * 1024];
                        var toRead = (int)Math.Min(readBuffer.Length, this.currentPacket.ToSend);
                        int read = this.currentPacket.RawData.Read(readBuffer, 0, toRead);
                        this.currentPacket.ToSend -= read;
                        if (read == 0)
                        {
                            if (this.currentPacket.ToSend > 0)
                            {
                                // Protocol needs all bytes!
                                throw new InvalidOperationException(
                                    "there was a higher bytecount given as there were in the stream!");
                                var sendNext = (int)Math.Min(int.MaxValue, this.currentPacket.ToSend);
                                var sendBuffer = new byte[sendNext];
                                this.currentPacket.ToSend -= sendNext;
                                this.SendAsync(sendBuffer, count: sendNext);
                                Debug.Assert(false); // Should not happen
                            }
                            else
                            {
                                // Packet complete
                                if (this.currentPacket.DisposeStream)
                                {
                                    try
                                    {
                                        this.currentPacket.RawData.Dispose();
                                    }
                                    catch (ObjectDisposedException ex)
                                    { // Ignore
                                        Logger.WriteLine("Stream was already disposed (ObjectDisposedException: {0})!", TraceEventType.Warning, ex);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.WriteLine("Exception while disposing Rawdata: {0}", TraceEventType.Error, ex);
                                    }
                                }

                                this.OnParsedLineSend(this.currentPacket.EventArgs);
                                this.SendNextPacket();
                            }

                        }
                        else
                        {
                            this.SendAsync(readBuffer, count: read);
                        }
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {
                this.HandleDisposed(ex);
            }
            catch (SocketException se)
            {
                this.HandleSocketException(se);
            }
            catch (Exception ex)
            {
                this.HandleUnknownException(ex);
            }
            finally
            {
                e.Dispose();
            }

            //Debug.Assert(sendcounter == 1);
        }

        public static void ConnectAsync(IPEndPoint endPoint, Action<MessageServerClient> doOnFinished, Action<Exception> onError = null)
        {
            var eventArgs = new SocketAsyncEventArgs();
            eventArgs.Completed += ConnectCompleted;
            eventArgs.RemoteEndPoint = endPoint;
            var userToken = Tuple.Create(doOnFinished, onError);
            eventArgs.UserToken = userToken;
            // new Action<Socket>(s => doOnFinished(s == null ? null : new MessageServerClient(s, encoding)));
#if NET2
            System.Net.Sockets.Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (!s.ConnectAsync(eventArgs))
#else
            if (!System.Net.Sockets.Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, eventArgs))
#endif
            {
                ((EventHandler<SocketAsyncEventArgs>)ConnectCompleted).BeginInvoke(null, eventArgs, null, null);
            }
        }

        private static void ConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            var token = (Tuple<Action<MessageServerClient>, Action<Exception>>)e.UserToken;
            var doOnFinished = token.Item1;
            var onError = token.Item2;
            try
            {
                Console.WriteLine("Client connected");
                CheckEventArgsStatic(e);
#if NET2
                Socket socket = (Socket)sender;
#else
                Socket socket = e.ConnectSocket;
#endif
                MessageServerClient client = null;
                client = new MessageServerClient(socket);
                client.WaitForClientData();
                doOnFinished(client);
            }
            catch (ObjectDisposedException ex)
            {
                onError(ex);
            }
            catch (Exception ex)
            {
                Logger.WriteLine("Unknown Error: {0}", TraceEventType.Critical, ex);
                onError(ex);
            }
            finally
            {
                e.Dispose();
            }
        }

        private void CheckEventArgs(SocketAsyncEventArgs e)
        {
            SocketError socketError = e.SocketError;
            if (socketError == SocketError.ConnectionReset)
            {
                this.HandleDisconnect();
                throw new ObjectDisposedException("MessageServerClient"); // Will be ignored
            }

            CheckEventArgsStatic(e);
        }

        private static void CheckEventArgsStatic(SocketAsyncEventArgs e)
        {
            var socketError = e.SocketError;
            if (socketError != SocketError.Success)
            {
                var ex = new RemoteServerException("SocketError!: " + socketError
#if NET4
                    , e.ConnectByNameError
#endif
                    );
                throw ex;
            }
        }

        public void SendAnswer(
            RawLineId id, StringParameter parameter, Stream rawData = null, Action<ParsedLineEventArgs> onAnswer = null, bool disposeStream = false, long dataLengthToSend = -1)
        {
            this.SendCommandAsyncPrivate(id, "_", parameter, rawData, onAnswer, disposeStream, dataLengthToSend);
        }

        internal void WaitForClientData()
        {
            try
            {
                // Start receiving any data written by the connected client
                // asynchronously
                var eventArgs = new SocketAsyncEventArgs();
                var buffer = new byte[4 * 1024];
                eventArgs.SetBuffer(buffer, 0, buffer.Length);
                eventArgs.SocketFlags = SocketFlags.None;
                eventArgs.Completed += this.ReceiveCompleted;

                if (!this.Socket.ReceiveAsync(eventArgs))
                {
                    ((EventHandler<SocketAsyncEventArgs>)this.ReceiveCompleted).BeginInvoke(
                        this.Socket, eventArgs, null, null);
                }
            }
            catch (ObjectDisposedException ex)
            {
                this.HandleDisposed(ex);
            }
            catch (SocketException se)
            {
                this.HandleSocketException(se);
            }
            catch (Exception ex)
            {
                this.HandleUnknownException(ex);
            }
        }

        private int receivecounter = 0;
        private object testLock = new object();
        private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                Interlocked.Increment(ref this.receivecounter);
                lock (this.testLock)
                {

                    Interlocked.Decrement(ref this.receivecounter);
                    Debug.Assert(this.receivecounter == 0);
                    this.CheckEventArgs(e);
                    int readBytes = e.BytesTransferred;

                    // Check if remote host has closed connection.
                    if (readBytes == 0)
                    {
                        this.HandleDisconnect();
                        return;
                    }
                    this.OnProcessData(e.Buffer, 0, readBytes);
                }
                
                this.WaitForClientData();
            }
            catch (ObjectDisposedException ex)
            {
                this.HandleDisposed(ex);
            }
            catch (SocketException se)
            {
                this.HandleSocketException(se);
            }
            catch (FormatException fe) //Invalid Package contents
            {
                Logger.WriteLine("Received Invalid Package... Disconnecting: {0}", TraceEventType.Error, fe);
                this.HandleDisconnect();
            }
            catch (Exception ex)
            {
                this.HandleUnknownException(ex);
            }
            finally
            {
                e.Dispose();
            }
        }

        private void HandleDisposed(ObjectDisposedException e)
        {
            Logger.WriteLine("Socket has been closed (ObjectDisposedException: {0})!", TraceEventType.Warning, e);
            if (this.isConnected)
            {
                this.HandleDisconnect();
            }
        }

        public void DisconnectAsync()
        {
            this.DisconnectAsyncPrivate(new SocketAsyncEventArgs());
        }

        private void DisconnectAsyncPrivate(SocketAsyncEventArgs eventArgs)
        {
            if (!this.isConnected)
            {
                return;
            }
            eventArgs.DisconnectReuseSocket = true;
            eventArgs.Completed += this.DisconnectCompleted;
            if (!this.Socket.DisconnectAsync(eventArgs))
            {
                ((EventHandler<SocketAsyncEventArgs>)this.DisconnectCompleted).BeginInvoke(
                    this.Socket, eventArgs, null, null);
            }
        }

        private void HandleDisconnect()
        {
            var eventArgs = new SocketAsyncEventArgs();
            try
            {
                this.DisconnectAsyncPrivate(eventArgs);
            }
            catch (Exception ex)
            {
                ((EventHandler<SocketAsyncEventArgs>)this.DisconnectCompleted).Invoke(this.Socket, eventArgs);
                if (!(ex is ObjectDisposedException || ex is SocketException))
                {
                    this.HandleUnknownException(ex);
                }
                var socketException = ex as SocketException;
                if (socketException != null)
                {
                    this.HandleSocketException(socketException);
                }
            }
        }

        private void HandleSocketException(SocketException e)
        {
            Logger.WriteLine("Socket Exception: {0}, Disconnecting", TraceEventType.Error, e);
            this.HandleDisconnect();
            //this.OnUnknownError(new ErrorEventArgs(e));
        }

        private void DisconnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                try
                {
                    // Discard all packages
                    lock (this.sendSync)
                    {
                        while (this.sendQueue.Count > 0)
                        {
                            var pack = this.sendQueue.Dequeue();
                            if (pack.DisposeStream && pack.RawData != null)
                            {
                                pack.RawData.Dispose();
                            }
                        }
                    }
                    this.isConnected = false;
                    this.OnDisconnected(EventArgs.Empty);
                    this.CheckEventArgs(e);
                }
                finally
                {
                    e.Dispose();
                }
            }
            catch (Exception ex)
            {
                this.HandleUnknownException(ex);
            }
        }

        #region Nested type: SendPacket

        private class SendPacket
        {
            public SendPacket(byte[] bytesToSend, ParsedLineEventArgs eventArgs, Stream rawData, MessageServerClient client, bool disposeStream, long toSend)
            {
                this.BytesToSend = bytesToSend;
                this.EventArgs = eventArgs;
                this.RawData = rawData;
                this.Client = client;
                this.DisposeStream = disposeStream;
                this.ToSend = toSend;
            }

            /// <summary>
            /// The bytes to send
            /// </summary>
            public byte[] BytesToSend { get; private set; }

            /// <summary>
            /// when not null there will be a CompletedEvent triggerd (last packet)
            /// </summary>
            public ParsedLineEventArgs EventArgs { get; private set; }

            /// <summary>
            /// The Raw Data to send
            /// </summary>
            public Stream RawData { get; private set; }

            /// <summary>
            /// The Client the message was send from
            /// </summary>
            public MessageServerClient Client { get; private set; }

            /// <summary>
            /// Indicates whether we should dispose the given stream.
            /// </summary>
            public bool DisposeStream { get; private set; }

            /// <summary>
            /// Indicates how much of rawdata we need to send.
            /// </summary>
            public long ToSend { get; set; }

            ///// <summary>
            ///// Indicates whether the Starting Bytes of this Packet are sent
            ///// </summary>
            //public bool BytesSend { get; set; }
        }

        #endregion
    }

    public class EventHelper
    {
        private readonly Dictionary<string, MyEvent> dict = new Dictionary<string, MyEvent>();

        private readonly object syncLock = new object();

        public MyEvent this[string index]
        {
            get
            {
                lock (this.syncLock)
                {
                    if (!this.dict.ContainsKey(index))
                    {
                        this.dict.Add(index, new MyEvent());
                    }

                    return this.dict[index];
                }
            }
        }
    }

    public class MyEvent
    {
        public event EventHandler<ParameterEventReceivedEventArgs> Event;

        internal void OnEvent(ParameterEventReceivedEventArgs e)
        {
            EventHandler<ParameterEventReceivedEventArgs> handler = this.Event;
            if (handler != null)
            {
                foreach (
                    var singleHandler in (EventHandler<ParameterEventReceivedEventArgs>[])handler.GetInvocationList())
                {
                    if (e.PreventProcessing)
                    {
                        return;
                    }
                    singleHandler(this, e);
                }
            }
        }
    }
}