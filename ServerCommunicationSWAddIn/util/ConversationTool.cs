using Newtonsoft.Json.Linq;
using System;
using ServerCommunicationSWAddIn.communication;
using ServerCommunicationSWAddIn.core;

namespace ServerCommunicationSWAddIn.util
{
    /// <summary>
    /// Class used to establish different types of conversations with the server
    /// </summary>
    public class ConversationTool
    {
        /// <summary>
        /// Obtains the version of the requested assembly, using its id
        /// </summary>
        /// <param name="assembly">The assembly used to send the request</param>
        /// <returns>The version in the server of the requested assembly</returns>
        public static uint GetVersionId(Assembly assembly)
        {
            string message = "{\"id\": " + assembly.IdAssembly + "," + "\"version\":" + assembly.Version + "}";

            CommTool.Instance.SendMessage(message, HeaderPacketComm.Command.GET_VERSION_ID);

            HeaderPacketComm header;
            string inMessage;
            CommTool.Instance.Receive(out header, out inMessage);

            if (header.m_statusComm != HeaderPacketComm.StatusServer.OK_RESPONSE)
                return 0;


            dynamic stuff = JObject.Parse(inMessage);

            if (stuff.id != assembly.IdAssembly)
            {
                return 0;
            }
            return stuff.version;
        }

        /// <summary>
        /// Send an update of an assembly to the server
        /// </summary>
        /// <param name="assembly">The info of the updated assembly</param>
        /// <returns>If the server receive the assembly or not</returns>
        public static bool UpdateAssembly(Assembly assembly)
        {
            assembly.SendUpdate();

            HeaderPacketComm header;
            string inMessage;
            CommTool.Instance.Receive(out header, out inMessage);

            return header.m_statusComm == HeaderPacketComm.StatusServer.OK_RESPONSE;

        }

        /// <summary>
        /// Send a new assembly to the server
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns>If the server receive the assembly or not</returns>
        public static bool NewAssembly(ref Assembly assembly)
        {
            assembly.SendNew();

            HeaderPacketComm header;
            string inMessage;
            CommTool.Instance.Receive(out header, out inMessage);

            if (header.m_statusComm != HeaderPacketComm.StatusServer.OK_RESPONSE)
                return false;

            dynamic stuff = JObject.Parse(inMessage);

            try
            {
                assembly.IdAssembly = stuff.id;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

            return true;


        }
    }
}
