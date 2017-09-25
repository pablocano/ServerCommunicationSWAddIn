using AngelSix.SolidDna;
using Newtonsoft.Json;
using ServerCommunicationSWAddIn.communication;
using System;
using System.Collections.Generic;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.sldworks;

namespace ServerCommunicationSWAddIn.core
{
    /// <summary>
    /// Class used to wrap and send the important information of an assembly to a server
    /// </summary>
    [JsonObject(MemberSerialization.Fields)]
    public class Assembly
    {
        #region Private Members
        /// <summary>
        /// List of part of this assembly
        /// </summary>
        private List<Relation> m_listAssemblyRelations;

        /// <summary>
        /// The name of the assembly
        /// </summary>
        private string m_name;

        /// <summary>
        /// The part number of the assembly
        /// </summary>
        private string m_part_number;

        /// <summary>
        /// The unique identifier of this assembly
        /// </summary>
        private int m_id_assembly;

        /// <summary>
        /// The current version of this assembly
        /// </summary>
        private uint m_version;

        /// <summary>
        /// The name of the file of this assembly
        /// </summary>
        private string m_filename;

        /// <summary>
        /// The unique identifier of the document used to create this class
        /// </summary>
        [JsonIgnore]
        private string m_guid;

        /// <summary>
        /// The document from solid edge from which this assembly was made of
        /// </summary>
        [JsonIgnore]
        private Model m_document;

        /// <summary>
        /// A dictionary used to wait for others documents to be send to the server
        /// </summary>
        [JsonIgnore]
        private Dictionary<string, List<Relation>> m_waiting_ids;

        [JsonIgnore]
        private uint m_model_version;

        #endregion

        #region Public Members

        /// <summary>
        /// Access to the id of the assembly
        /// </summary>
        public int IdAssembly
        {
            get
            {
                return m_id_assembly;
            }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(
                          $"{nameof(value)} must be positive");
                m_id_assembly = value;
            }
        }

        /// <summary>
        /// Access to the name of the assembly
        /// </summary>
        public string Name { get { return m_name; } set { m_name = value; } }

        /// <summary>
        /// Access to the part number of the assembly
        /// </summary>
        public string PartNumber { get { return m_part_number; } set { m_part_number = value; } }

        /// <summary>
        /// Access to the current version of the assembly
        /// </summary>
        public uint Version { get { return m_version; } }

        /// <summary>
        /// Access to the current version of the exported model
        /// </summary>
        public uint ModelVersion { get { return m_model_version; } }

        /// <summary>
        /// Access to the unique identifier of the document used to create this class
        /// </summary>
        public string Guid { get { return m_guid; } }

        /// <summary>
        /// Indicates if this assembly can be sent to the server as a new assembly
        /// </summary>
        public bool CanBeSent
        {
            get
            {
                return m_name != "" && m_part_number != "";
            }
        }

        /// <summary>
        /// Indicates if this assembly can be sent to the server as an update
        /// </summary>
        public bool CanBeUpdated
        {
            get
            {
                return m_id_assembly > 0;
            }
        }

        /// <summary>
        /// Access to the m_is_part property
        /// </summary>
        public bool IsPart { get { return m_document.IsPart; } }

        /// <summary>
        /// Access to the assembly document, only if this assembly is not a part
        /// </summary>
        public AssemblyDoc AssemblyDocument
        {
            get
            {
                if (IsPart)
                {
                    return null;
                }
                else
                {
                    return m_document.AsAssembly();
                }
            }
        }

        /// <summary>
        /// Access to the part document, only if this assembly is a part
        /// </summary>
        public PartDoc PartDocument
        {
            get
            {
                if (!IsPart)
                {
                    return null;
                }
                else
                {
                    return m_document.AsPart();
                }
            }
        }

        /// <summary>
        /// Access to the solidworks document
        /// </summary>
        public ModelDoc2 Document { get { return m_document.UnsafeObject; } }

        /// <summary>
        /// Access to the document name
        /// </summary>
        public string DocumentName
        {
            get
            {
                return m_document.UnsafeObject.GetTitle();
            }
        }

        /// <summary>
        /// Access to the list of relations of this assembly with child assemblies
        /// </summary>
        public List<Relation> ListOfRelations
        {
            get
            {
                return m_listAssemblyRelations;
            }
        }

        /// <summary>
        /// A dictionary used to wait child assemblies to be send to the server
        /// </summary>
        public Dictionary<string, List<Relation>> WaitingChildIds
        {
            get
            {
                return m_waiting_ids;
            }
        }

        #endregion

        #region Constructor Methods

        /// <summary>
        /// General constructor
        /// </summary>
        /// <param name="document">The document used to create the assembly</param>
        public Assembly(Model document)
        {
            m_document = document;
            m_listAssemblyRelations = new List<Relation>();
            m_waiting_ids = new Dictionary<string, List<Relation>>();
            m_filename = DocumentName;
            Init();
        }

        /// <summary>
        /// Common section of the construction for an assembly or a part
        /// </summary>
        private void Init()
        {
            // Save the initial configuration of the assemblies
            bool needSave = false;

            // Verify if this assembly has already an id
            string id = m_document.GetCustomProperty("Id");
            if(id == "")
            {
                needSave = true;
                m_id_assembly = -1;
                m_document.SetCustomProperty("Id", "-1");
            }
            else
            {
                try
                {
                    AddId(Convert.ToInt32(id));
                }
                catch
                {

                }
            }


            // Set the information needed with the properties values
            m_part_number = m_document.GetCustomProperty("partNumber");
            if(m_part_number == "")
            {
                needSave = true;
                m_document.SetCustomProperty("partNumber", "");
            }


            string version = m_document.GetCustomProperty("version");
            if(version == "")
            {
                needSave = true;
                m_version = 1;
                m_document.SetCustomProperty("version", "1");
            }
            else
            {
                m_version = Convert.ToUInt32(version);
            }

            string sModelVersion = m_document.GetCustomProperty("modelVersion");
            if (sModelVersion == "")
            {
                needSave = true;
                m_model_version = 1;
                m_document.SetCustomProperty("modelVersion", "0");
            }
            else
            {
                m_model_version = Convert.ToUInt32(sModelVersion);
            }

            m_name = m_document.UnsafeObject.GetTitle();
            m_guid = m_document.FilePath;

            if (needSave)
            {
                Save();
            }
        }

        /// <summary>
        /// Validates the id
        /// </summary>
        /// <param name="id"></param>
        public void AddId(int id)
        {
            m_id_assembly = id;
            if (m_id_assembly < 0)
            {
                throw new ArgumentException("The id cannot be negative");
            }
            m_document.SetCustomProperty("Id", m_id_assembly.ToString());
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Function that sends an Assembly to the server
        /// </summary>
        public uint SendUpdate()
        {
            // Obtain a string of the json representation of this class
            string jsonRepresentation = JsonConvert.SerializeObject(this);

            // Send the message over the communication protocol
            return CommTool.Instance.SendMessage(jsonRepresentation, HeaderPacketComm.Command.UPDATE_ASSEMBLY);

        }

        /// <summary>
        /// Function that sends an Assembly to the server
        /// </summary>
        public uint SendNew()
        {
            // Obtain a string of the json representation of this class
            string jsonRepresentation = JsonConvert.SerializeObject(this);

            // Send the message over the communication protocol
            return CommTool.Instance.SendMessage(jsonRepresentation, HeaderPacketComm.Command.NEW_ASSEMBLY);

        }

        /// <summary>
        /// Save the id of the assembly in the document
        /// </summary>
        public void Save()
        {
            int Errors = 0, Warnings = 0;
            m_document.UnsafeObject.Save3((int)(swSaveAsOptions_e.swSaveAsOptions_AvoidRebuildOnSave | swSaveAsOptions_e.swSaveAsOptions_Silent), ref Errors, ref Warnings);
        }

        /// <summary>
        /// Updates the model version of this assembly
        /// </summary>
        public void UpdateModelVersion()
        {
            m_model_version = m_version;
            m_document.SetCustomProperty("modelVersion", m_model_version.ToString());
            Save();
        }
        #endregion
    }
}
