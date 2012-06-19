namespace Yaaf.Utils.IO.MessageProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;

    using Yaaf.Utils.Logging;

    public class MessageServer
    {
        private readonly object connectedClientsLock = new object();

        private Socket socket;


        private readonly List<MessageServerClient> connectedClients = new List<MessageServerClient>();

        public event EventHandler<ClientConnectedEventArgs> ClientConnected;

        public void OnClientConnected(ClientConnectedEventArgs e)
        {
            EventHandler<ClientConnectedEventArgs> handler = this.ClientConnected;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public IEnumerable<MessageServerClient> ConnectedClients
        {
            get
            {
                List<MessageServerClient> ret;
                lock (this.connectedClientsLock)
                {
                    ret = this.connectedClients.ToList();
                }
                return ret;
            }
        }
        
        public MessageServer()
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start(int port)
        {
            this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
            this.socket.Listen(10);
            this.WaitForClientConnect();
        }

        private void WaitForClientConnect()
        {
            var eventArgs = new SocketAsyncEventArgs();
            eventArgs.SocketFlags = SocketFlags.None;
            eventArgs.Completed += this.AcceptCompleted;
            if (!this.socket.AcceptAsync(eventArgs))
            {
                ((EventHandler<SocketAsyncEventArgs>)this.AcceptCompleted).BeginInvoke(
                    this.socket, eventArgs, null, null);
            }
        }

        private void AcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                var clientSocket = e.AcceptSocket;
                Logger.WriteLine("Client connected!");
                var client = this.CreateNewClient(clientSocket);
                this.OnClientConnected(new ClientConnectedEventArgs(client));
                
                client.WaitForClientData();

                this.WaitForClientConnect();
            }
            catch (global::System.ObjectDisposedException)
            {
                Logger.WriteLine("OnClientConnection: Socket has been closed!", TraceEventType.Information);
            }
            catch (SocketException se)
            {
                Logger.WriteLine("SocketException: {0}!", TraceEventType.Warning, se);
            }
            finally
            {
                e.Dispose();
            }
        }

        private MessageServerClient CreateNewClient(Socket clientSocket)
        {
            var client = new MessageServerClient(clientSocket);
            lock (this.connectedClientsLock)
            {
                this.connectedClients.Add(client);
            }
            client.Disconnected += this.OnClientDisconnected;
            return client;
        }

        void OnClientDisconnected(object sender, EventArgs e)
        {
            var client = (MessageServerClient)sender;
            lock (this.connectedClientsLock)
            {
                this.connectedClients.Remove(client);
            }
        }
    }

    public class ClientConnectedEventArgs : EventArgs
    {
        public ClientConnectedEventArgs(MessageServerClient connectedClient)
        {
            this.ConnectedClient = connectedClient;
        }

        public MessageServerClient ConnectedClient { get; private set; }
    }
}