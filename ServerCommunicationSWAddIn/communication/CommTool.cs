using ServerCommunicationSWAddIn.util;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ServerCommunicationSWAddIn.communication
{
    /// <summary>
    /// Helper class used to communicate with the server
    /// </summary>
    public sealed class CommTool
    {
        #region Singleton Definition

        /// <summary>
        /// The only instance of this class
        /// </summary>
        private static volatile CommTool instance;
        private static object syncRoot = new Object();

        /// <summary>
        /// A private constructure to ensure the singleton pattern
        /// </summary>
        private CommTool() { }

        /// <summary>
        /// Access to the only instance of this class
        /// </summary>
        public static CommTool Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null)
                            instance = new CommTool();
                    }
                }

                return instance;
            }
        }


        #endregion

        #region Private Members
        /// <summary>
        /// The socket that connects to the server
        /// </summary>
        private Socket m_commSocket;

        /// <summary>
        /// The port number of the communication channel
        /// </summary>
        private const int m_PORT_NO = 4321;

        /// <summary>
        /// The IP direction of the server
        /// </summary>
        private const string m_SERVER_IP = "10.31.13.100";

        /// <summary>
        /// The key used to encrypt the messages
        /// </summary>
        private readonly uint[] m_key = { 56324394, 73576, 12030122, 56 };

        /// <summary>
        /// The unique idenfier of a message for this session
        /// </summary>
        private static uint m_id_response = 0;

        #endregion

        #region Public Members

        /// <summary>
        /// Signal that triggers when the connections is established
        /// </summary>
        public static ManualResetEvent connectDone = new ManualResetEvent(false);

        /// <summary>
        /// Indicates if the socket is connected to the server 
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (m_commSocket == null)
                    return false;
                return m_commSocket.Connected;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles the completion of the asynchronous connect call
        /// </summary>
        /// <param name="ar">The object pased to the BeginConnect call</param>
        private static void CallbackConnectMethod(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket s = (Socket)ar.AsyncState;

                // Complete the connection.  
                s.EndConnect(ar);

                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                connectDone.Set();
                Debug.Print(e.Message);
            }
        }

        #endregion

        #region Public Methods
        /// <summary>
        /// Starts the communication with the server
        /// </summary>
        public void StartCommunication(bool blocking = false)
        {
            try
            {
                // Create a new socket to connect to a server via TCP.
                m_commSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                if (blocking)
                {
                    m_commSocket.Connect(IPAddress.Parse(m_SERVER_IP), m_PORT_NO);
                }
                else
                {
                    //Try to connect asynchronusly.
                    m_commSocket.BeginConnect(IPAddress.Parse(m_SERVER_IP), m_PORT_NO, new AsyncCallback(CallbackConnectMethod), m_commSocket);
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
            }
        }

        /// <summary>
        /// Ends the communication with the server
        /// </summary>
        public void EndCommunication()
        {
            m_commSocket.Disconnect(true);
        }

        /// <summary>
        /// Close the internal socket
        /// </summary>
        public void CloseCommunication()
        {
            m_commSocket.Close();
        }

        /// <summary>
        /// Send a message to the server using the communication protocol
        /// </summary>
        /// <param name="messsage">The string representation of the message</param>
        public uint SendMessage(string messsage, HeaderPacketComm.Command command)
        {
            // Creation of the header
            HeaderPacketComm a = new HeaderPacketComm();
            a.m_command = command;
            lock (syncRoot)
            {
                a.m_idResponse = ++m_id_response;
            }
            uint lenght = (uint)messsage.Length + 1;
            uint rest = lenght % 4;
            a.m_size = rest == 0 ? lenght : lenght + 4 - rest;

            // Get a byte array of the message
            byte[] bytes = Encoding.ASCII.GetBytes(messsage);

            ///Create the stream block to encrypt
            uint[] stream = new uint[a.m_size / 4];
            Buffer.BlockCopy(bytes, 0, stream, 0, bytes.Length);

            // Encrypt the message with the key of this class
            Cryptography.encrypt(ref stream, m_key);

            // Create the final buffer to be sent
            byte[] buffer = new byte[HeaderPacketComm.SIZE_HEADER_PACKET + a.m_size];

            // Copy the header in the buffer
            Buffer.BlockCopy(a.getByteArray(), 0, buffer, 0, HeaderPacketComm.SIZE_HEADER_PACKET);

            // Copy the encrypted stream into the buffer
            Buffer.BlockCopy(stream, 0, buffer, HeaderPacketComm.SIZE_HEADER_PACKET, stream.Length * 4);

            // Send the buffer to the server
            m_commSocket.Send(buffer);

            return m_id_response;

        }

        /// <summary>
        /// Receive a message from the server
        /// </summary>
        /// <param name="inMessage">the place where the message is going to be stored</param>
        public void Receive(out HeaderPacketComm header, out string inMessage)
        {
            // Create a buffer to store the header
            byte[] buffer = new byte[HeaderPacketComm.SIZE_HEADER_PACKET];

            //Get the header from the socket
            m_commSocket.Receive(buffer, HeaderPacketComm.SIZE_HEADER_PACKET, 0);

            // Create a header with the received buffer
            header = new HeaderPacketComm(buffer);

            // If the header contains no message, return
            if (header.m_size == 0)
            {
                inMessage = "";
                return;
            }

            // Creates a buffer to store the message
            byte[] infoBuffer = new byte[header.m_size];

            //Get the actual message from the socket
            m_commSocket.Receive(infoBuffer, (int)header.m_size, 0);

            // Create a stream to store the buffer in a int type array
            uint[] stream = new uint[(int)header.m_size / 4];

            // copy the buffer to the array
            Buffer.BlockCopy(infoBuffer, 0, stream, 0, infoBuffer.Length);

            // Decrypt the message
            Cryptography.decrypt(ref stream, m_key);

            //Get the bytes of the decrypted message
            byte[] decodedbyteArray = stream.SelectMany(BitConverter.GetBytes).ToArray();

            //obtain the string message from the byte array
            inMessage = System.Text.Encoding.ASCII.GetString(decodedbyteArray).Trim();
        }

        #endregion


    }
}
