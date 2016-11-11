﻿/*
Technitium Bit Chat
Copyright (C) 2015  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using BitChatCore.Network.Connections;
using BitChatCore.Network.KademliaDHT;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace BitChatCore.Network
{
    class TcpRelayService : IDisposable
    {
        #region variables

        const int BIT_CHAT_TRACKER_UPDATE_INTERVAL = 120;
        const int TCP_RELAY_KEEP_ALIVE_INTERVAL = 30000; //30 sec

        static Dictionary<BinaryID, TcpRelayService> _relays = new Dictionary<BinaryID, TcpRelayService>(2);

        TrackerManager _trackerManager;
        Dictionary<BinaryID, Connection> _relayConnections = new Dictionary<BinaryID, Connection>(2);

        Timer _tcpRelayConnectionKeepAliveTimer;

        #endregion

        #region constructor

        private TcpRelayService(BinaryID networkID, int servicePort, DhtClient dhtClient)
        {
            _trackerManager = new TrackerManager(networkID, servicePort, dhtClient, BIT_CHAT_TRACKER_UPDATE_INTERVAL);

            //start keep alive timer
            _tcpRelayConnectionKeepAliveTimer = new Timer(RelayConnectionKeepAliveTimerCallback, null, TCP_RELAY_KEEP_ALIVE_INTERVAL, Timeout.Infinite);
        }

        #endregion

        #region IDisposable

        ~TcpRelayService()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool _disposed = false;

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_tcpRelayConnectionKeepAliveTimer != null)
                    _tcpRelayConnectionKeepAliveTimer.Dispose();

                //remove all connections
                lock (_relayConnections)
                {
                    _relayConnections.Clear();
                }

                //stop tracking
                if (_trackerManager != null)
                    _trackerManager.Dispose();

                _disposed = true;
            }
        }

        #endregion

        #region private

        private void RelayConnectionKeepAliveTimerCallback(object state)
        {
            try
            {
                //send noop to all connections to keep them alive
                lock (_relayConnections)
                {
                    foreach (Connection connection in _relayConnections.Values)
                    {
                        try
                        {
                            connection.SendNOOP();
                        }
                        catch
                        { }
                    }
                }
            }
            catch
            { }
            finally
            {
                if (_tcpRelayConnectionKeepAliveTimer != null)
                    _tcpRelayConnectionKeepAliveTimer.Change(TCP_RELAY_KEEP_ALIVE_INTERVAL, Timeout.Infinite);
            }
        }

        #endregion

        #region static

        public static TcpRelayService StartTcpRelay(BinaryID networkID, Connection connection, int servicePort, DhtClient dhtClient, Uri[] tracketURIs)
        {
            TcpRelayService relay;

            lock (_relays)
            {
                if (_relays.ContainsKey(networkID))
                {
                    relay = _relays[networkID];
                }
                else
                {
                    relay = new TcpRelayService(networkID, servicePort, dhtClient);
                    _relays.Add(networkID, relay);
                }

                lock (relay._relayConnections)
                {
                    relay._relayConnections.Add(connection.RemotePeerID, connection);
                }
            }

            relay._trackerManager.AddTracker(tracketURIs);
            relay._trackerManager.StartTracking();

            return relay;
        }

        public static List<IPEndPoint> GetPeerEPs(BinaryID channelName, Connection requestingConnection)
        {
            BinaryID localPeerID = requestingConnection.LocalPeerID;
            BinaryID remotePeerID = requestingConnection.RemotePeerID;

            lock (_relays)
            {
                foreach (KeyValuePair<BinaryID, TcpRelayService> itemRelay in _relays)
                {
                    BinaryID computedChannelName = Connection.GetChannelName(localPeerID, remotePeerID, itemRelay.Key);

                    if (computedChannelName.Equals(channelName))
                    {
                        Dictionary<BinaryID, Connection> relayConnections = itemRelay.Value._relayConnections;
                        List<IPEndPoint> peerEPs = new List<IPEndPoint>(relayConnections.Count);

                        lock (relayConnections)
                        {
                            foreach (KeyValuePair<BinaryID, Connection> itemProxyConnection in relayConnections)
                            {
                                peerEPs.Add(itemProxyConnection.Value.RemotePeerEP);
                            }
                        }

                        return peerEPs;
                    }
                }
            }

            return null;
        }

        public static void StopAllTcpRelays()
        {
            lock (_relays)
            {
                foreach (TcpRelayService relay in _relays.Values)
                {
                    relay.Dispose();
                }

                _relays.Clear();
            }
        }

        #endregion

        #region public

        public void StopTcpRelay(Connection connection)
        {
            bool removeSelf = false;

            lock (_relayConnections)
            {
                if (_relayConnections.Remove(connection.RemotePeerID))
                {
                    removeSelf = (_relayConnections.Count < 1);
                }
            }

            if (removeSelf)
            {
                lock (_relays)
                {
                    lock (_relayConnections) //lock to avoid race condition
                    {
                        if (_relayConnections.Count < 1) //recheck again
                        {
                            //stop tracking
                            _trackerManager.StopTracking();

                            //remove self from list
                            _relays.Remove(_trackerManager.NetworkID);
                        }
                    }
                }
            }
        }

        #endregion

        #region properties

        public BinaryID NetworkID
        { get { return _trackerManager.NetworkID; } }

        #endregion
    }
}
