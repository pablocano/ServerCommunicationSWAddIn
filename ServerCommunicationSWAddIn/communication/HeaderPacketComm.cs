using System;

namespace ServerCommunicationSWAddIn.communication
{
    /// <summary>
    /// Header class for the communication whit the server
    /// </summary>
    public class HeaderPacketComm
    {
        /// <summary>
        /// Enum that declares the posibles commands to send to the server
        /// </summary>
        public enum Command : uint
        {
            NONE,
            CLOSE_CONNECTION,
            GET_INFO_PLANT,
            GET_PLANT,
            GET_ASSEMBLIES,
            GET_MODEL_ASSEMBLY,
            GET_INFO_ASSEMBLY,
            GET_MAINTENANCE_ASSEMBLY,
            GET_LIST_SENSORS,
            GET_SENSORS,
            GET_VERSION_ID,
            NEW_ASSEMBLY,
            UPDATE_ASSEMBLY
        };

        /// <summary>
        /// Enum that declares the posible responses given by the server 
        /// </summary>
        public enum StatusServer : uint
        {
            NORMAL,
            WRONG_COMMAND,
            WRONG_VERSION,
            OK_RESPONSE,
            ERROR_RESPONSE,
            FULL_CONNECTION,
            ERROR_CONNECTION
        }
        /// <summary>
        /// The size of the header
        /// </summary>
        public const byte SIZE_HEADER_PACKET = 17;

        /// <summary>
        /// Defines the mayor version number
        /// </summary>
        private const byte MAYOR_NUM_VERSION_PACKET = 0x1;

        /// <summary>
        /// Defines the minor version number
        /// </summary>
        private const byte MINOR_NUM_VERSION_PACKET = 0x0;

        /// <summary>
        /// The version of the header
        /// </summary>
        public const byte version = ((MAYOR_NUM_VERSION_PACKET << 4) | MINOR_NUM_VERSION_PACKET);

        /// <summary>
        /// The command to send to the server
        /// </summary>
        public Command m_command;

        /// <summary>
        /// The status of the communication
        /// </summary>
        public StatusServer m_statusComm;

        /// <summary>
        /// The response of the server
        /// </summary>
        public uint m_idResponse;

        /// <summary>
        /// The size of the message to send
        /// </summary>
        public uint m_size;

        /// <summary>
        /// Default constructor
        /// </summary>
        public HeaderPacketComm()
        {
            m_command = Command.NONE;
            m_statusComm = StatusServer.NORMAL;
            m_idResponse = 0;
            m_size = 0;
        }

        /// <summary>
        /// Constructor that get the values from a buffer of bytes
        /// </summary>
        /// <param name="buffer"></param>
        public HeaderPacketComm(byte[] buffer)
        {
            // Create an array to obtain the values
            uint[] values = new uint[4];

            // Copy the values in the buffer into the new created array
            Buffer.BlockCopy(buffer, 1, values, 0, SIZE_HEADER_PACKET - 1);

            // Set the values of the class
            m_command = (Command)values[0];
            m_statusComm = (StatusServer)values[1];
            m_idResponse = values[2];
            m_size = values[3];
        }

        /// <summary>
        /// Obtains the byte representation of the header
        /// </summary>
        /// <returns>a byte array with the header information</returns>
        public byte[] getByteArray()
        {
            // Create the output buffer
            byte[] buffer = new byte[SIZE_HEADER_PACKET];

            // Create an array with all the data of the header
            uint[] values = { (uint)m_command, (uint)m_statusComm, m_idResponse, m_size };

            // Set the first byte with the version of the header
            buffer[0] = version;

            // Copy the info of the header in the buffer
            Buffer.BlockCopy(values, 0, buffer, 1, SIZE_HEADER_PACKET - 1);

            // Return the output buffer
            return buffer;
        }
    }
}
