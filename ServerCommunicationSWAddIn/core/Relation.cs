using Newtonsoft.Json;
using System;

namespace ServerCommunicationSWAddIn.core
{
    /// <summary>
    /// Class that describe a relation between the owner of an instance of this class and a part or assembly referenced in this relation.
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    public class Relation
    {
        /// <summary>
        /// The id of the part or assembly referenced in this relation
        /// </summary>
        private int m_id_assembly;

        /// <summary>
        /// The center of the assembly or part linked by this relation, with respect to the center of the parent
        /// </summary>
        private Position m_position;

        /// <summary>
        /// The identifier of the instance of this part or assembly in the parent assembly
        /// </summary>
        private int m_id_instance;

        /// <summary>
        /// Constructor of the class
        /// </summary>
        /// <param name="name">The name of the ocurrence of this relation</param>
        /// <param name="id">The id of the part or assembly that this relation is reference to</param>
        /// <param name="position">The position that this relation describes</param>
        public Relation(string name, int id, Position position)
        {
            m_id_instance = Convert.ToInt32(name.Remove(0, name.LastIndexOf('-') + 1).Trim());
            m_id_assembly = id;
            m_position = position;
        }

        /// <summary>
        /// Access to the id propertie
        /// </summary>
        public int Id
        {
            get
            {
                return m_id_assembly;
            }
            set
            {
                m_id_assembly = value;
            }
        }
    }
}
