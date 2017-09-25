using Newtonsoft.Json;

namespace ServerCommunicationSWAddIn.core
{
    /// <summary>
    /// Class that describes a position in the space, an its correspond rotation
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    public class Position
    {
        /// <summary>
        /// The position in xyz cords
        /// </summary>
        private Vectorf3D m_pos;

        /// <summary>
        /// The rotation in each axis
        /// </summary>
        private Vectorf3D m_rot;

        /// <summary>
        /// Constructor of the class
        /// </summary>
        /// <param name="pos">The position of the assembly</param>
        /// <param name="rot">The rotation of the assembly</param>
        public Position(Vectorf3D pos, Vectorf3D rot)
        {
            m_pos = pos;
            m_rot = rot;
        }
    }
}
