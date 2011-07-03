﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using QuickFix.Transport;
using QuickFix;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using QuickFix.Applications;

namespace UnitTests
{
    [TestFixture]
    public class SocketInitiatorTests
    {
        [TearDown]
        public void Dispose()
        {
            if (initiator_ != null)
                initiator_.Stop();
        }

        [Test]
        public void TestConnectAndReceive()
        {
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            TcpListener listener = new TcpListener(localAddr, 56123);
            listener.Start();
            listener.BeginAcceptSocket(new AsyncCallback(Connected), listener);
            
            string conf = new StringBuilder()
                .AppendLine("[DEFAULT]")
                .AppendLine("ConnectionType=initiator")
                .AppendLine("ReconnectInterval=1")
                .AppendLine("HeartBtInt=60")
                .AppendLine("SocketConnectHost=127.0.0.1")
                .AppendLine("SocketConnectPort=56123")
                .AppendLine("UseDataDictionary=N")
                .AppendLine("[SESSION]")
                .AppendLine("BeginString=FIX.4.2")
                .AppendLine("SenderCompID=TW")
                .AppendLine("TargetCompID=ISLD")
                .ToString();
            settings_ = new SessionSettings(new System.IO.StringReader(conf));
         
            _fixApp = new FIX4Application();
            initiator_ = new SocketInitiator(_fixApp, new MemoryStoreFactory(), settings_);
            initiator_.RawDataReceived += new SocketInitiator.RawDataReceivedHandler(i_RawDataReceived);
            initiator_.Start();

            Thread.Sleep(250);  
            // Server sends initiator a message.

            const string testData = "8=FIX.4.2\x01" + "9=46\x01" + "35=0\x01" + "34=3\x01" + "49=TW\x01" +
                "52=20000426-12:05:06\x01" + "56=ISLD\x01" + "1=acct123\x01" + "10=000\x01";
            Assert.NotNull(_clientSocket);
            _clientSocket.Send(Encoding.UTF8.GetBytes(testData + "\n"));
            Thread.Sleep(500);

            // Assert that the initiator is connected and receives it.
            //Assert.That(_clientSocket.Connected, Is.True);
            //Assert.That(initiator_.Connected, Is.True);
            Assert.That(_lastReceived, Is.EqualTo(testData));
            
            // Send message from initiator to server.
            SessionID sessionID = new SessionID("FIX.4.2", "TW", "ISLD");
            string testSend = testData;
            Session.SendToTarget(testSend, sessionID);

            byte[] r = new byte[256];
            _clientSocket.Receive(r);
            string received = Encoding.UTF8.GetString(r);
            Thread.Sleep(100);

            // Assert that dummy server receives our test message.
            // Note the substring, our dummy TcpListener is only receiving raw socket blocks of 256 bytes,
            // so we only assert that what we sent is contained in the first part of the 256 byte block.
            Assert.That(received.Substring(0, testSend.Length), Is.EqualTo(testSend));

            _clientSocket.Shutdown(SocketShutdown.Both);
            _clientSocket.Close();
            listener.Stop();
        }

        void i_RawDataReceived(object sender, string rawData)
        {
            _lastReceived = rawData;
        }

        private void Connected(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener)ar.AsyncState;
            _clientSocket = listener.EndAcceptSocket(ar);
        }

        private string _lastReceived;
        private Application _fixApp;
        private SessionSettings settings_;
        private Socket _clientSocket;
        private SocketInitiator initiator_;
    }
}
