using LiteNetLib;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Data;

namespace MLAPI.Puncher.Shared
{
    public class LiteNetLibUDPTransport : IUDPTransport, INetEventListener
    {
        private readonly NetManager netManager;
        private bool externalManager = false;
        private Thread updateThread;
        private volatile bool terminateThread = false;

        public LiteNetLibUDPTransport()
        {
            netManager = new NetManager(this)
            {
                UnconnectedMessagesEnabled = true,
                UpdateTime = 15,
            };
        }

        public LiteNetLibUDPTransport(NetManager netManager)
        {
            this.netManager = netManager;
            externalManager = true;
        }

        void IUDPTransport.Bind(IPEndPoint endpoint)
        {
            if (externalManager)
                return;

            netManager.Start(endpoint.Port);

            terminateThread = false;

            updateThread = new Thread(() =>
            {
                while (!terminateThread)
                {
                    netManager.PollEvents();
                    Thread.Sleep(15);
                }
            })
            {
                IsBackground = true,
            };
            updateThread.Start();
        }

        void IUDPTransport.Close()
        {
            if (externalManager)
                return;

            terminateThread = true;

            while (updateThread != null && updateThread.IsAlive)
                Thread.Sleep(100);

            netManager.Stop();
        }

        int IUDPTransport.SendTo(byte[] buffer, int offset, int length, int timeoutMs, IPEndPoint endpoint)
        {
            if (!netManager.SendUnconnectedMessage(buffer, offset, length, endpoint))
                return 0;

            return length;
        }

        struct RecvBuffer
        {
            public IPEndPoint endPoint;
            public byte[] data;
        }
        private readonly Queue<RecvBuffer> recvBuffers = new Queue<RecvBuffer>();

        int IUDPTransport.ReceiveFrom(byte[] buffer, int offset, int length, int timeoutMs, out IPEndPoint endpoint)
        {
            DateTime start = DateTime.Now;
            while(recvBuffers.Count == 0)
            {
                Thread.Sleep(100);
                if (timeoutMs > 0 && (DateTime.Now - start).TotalMilliseconds > timeoutMs)
                {
                    endpoint = null;
                    return -1;
                }
            }

            RecvBuffer recvBuffer = recvBuffers.Dequeue();
            endpoint = recvBuffer.endPoint;

            int copyBytes = Math.Min(recvBuffer.data.Length, length);
            Array.Copy(recvBuffer.data, 0, buffer, offset, copyBytes);

            return copyBytes;
        }

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            recvBuffers.Enqueue(new RecvBuffer() { endPoint = remoteEndPoint, data = reader.GetRemainingBytes() });
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
        }
    }
}
