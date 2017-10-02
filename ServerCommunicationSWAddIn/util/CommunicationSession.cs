using AngelSix.SolidDna;
using Newtonsoft.Json.Linq;
using ServerCommunicationSWAddIn.communication;
using ServerCommunicationSWAddIn.core;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace ServerCommunicationSWAddIn.util
{
    public class CommunicationSession
    {
        #region Internal Lists

        /// <summary>
        /// Dictionaty of all the assemblies that already enter to the process.
        /// </summary>
        private Dictionary<string, ProcessStatus> m_ProcessedAssemblies;

        /// <summary>
        /// Concurrent queue used to send new assemblies
        /// </summary>
        private ConcurrentQueue<Assembly> m_NewAssemblies;

        /// <summary>
        /// Concurrent queue used to send updates of an assembly 
        /// </summary>
        private ConcurrentQueue<Assembly> m_UpdateAssemblies;

        /// <summary>
        /// Concurrent queue used to send updates of an assembly 
        /// </summary>
        private ConcurrentQueue<Assembly> m_WaitingForVersion;

        /// <summary>
        /// Concurrent queue used to wait for this assemblies to be ready to process
        /// </summary>
        private ConcurrentQueue<Assembly> m_WaitingForChildIds;

        /// <summary>
        /// Waiting queue
        /// </summary>
        private ConcurrentDictionary<uint, Assembly> m_WaitingForResponse;

        /// <summary>
        /// Ready queue
        /// </summary>
        private ConcurrentDictionary<string, Assembly> m_ReadyAssemblies;

        /// <summary>
        /// Failed Queue
        /// </summary>
        private ConcurrentDictionary<string, Assembly> m_FailedAssemblies;

        #endregion

        #region Private Members
        /// <summary>
        /// Lock used to increase the progress of this session
        /// </summary>
        private object progressNumberLock = new Object();

        /// <summary>
        /// The actual progress of the session
        /// </summary>
        private int m_ProgressStatus;
        #endregion

        #region Public Properties

        /// <summary>
        /// Boolean to indicates if the session is over
        /// </summary>
        public bool SessionComplete
        {
            get
            {
                return m_ProcessedAssemblies.Count == m_FailedAssemblies.Count + m_ReadyAssemblies.Count;
            }
        }

        /// <summary>
        /// Boolean to indicate if the session can be exported
        /// </summary>
        public bool CanBeExported { get { return m_ProcessedAssemblies.Count == m_WaitingForVersion.Count; } }

        /// <summary>
        /// The total number of steps needed to end the session
        /// </summary>
        public int TotalSteps
        {
            get
            {
                return m_ProcessedAssemblies.Count * 4;
            }
        }

        /// <summary>
        /// The current progress of the session
        /// </summary>
        public int ProgressStatus
        {
            get
            {
                return m_ProgressStatus;
            }
        }

        #endregion

        #region Helper Classes
        /// <summary>
        /// Posibles results of the process of the assembly
        /// </summary>
        public enum ProcessStatus
        {
            UPDATE,
            NEW,
            WAITING_FOR_ID,
            FAIL
        }

        public enum MessageComm
        {
            SENDED,
            RECEIVED
        }

        /// <summary>
        /// Class used to send a message to the user
        /// </summary>
        public class StatusMessage
        {
            public string m_Name, m_Message;
            public ProcessStatus m_Status;
            public MessageComm m_MessageComm;

            public StatusMessage(string name, string message, ProcessStatus status = ProcessStatus.FAIL, MessageComm messageComm = MessageComm.SENDED)
            {
                m_Name = name;
                m_Message = message;
                m_Status = status;
                m_MessageComm = messageComm;
            }
        };
        #endregion

        #region Constructor

        /// <summary>
        /// The constructor of the class. Iniitalize all the queue and the dictionary
        /// </summary>
        public CommunicationSession()
        {
            // Initialization of the queues an the dictionary
            m_ProcessedAssemblies = new Dictionary<string, ProcessStatus>();
            m_NewAssemblies = new ConcurrentQueue<Assembly>();
            m_UpdateAssemblies = new ConcurrentQueue<Assembly>();
            m_WaitingForVersion = new ConcurrentQueue<Assembly>();
            m_WaitingForChildIds = new ConcurrentQueue<Assembly>();
            m_WaitingForResponse = new ConcurrentDictionary<uint, Assembly>();
            m_ReadyAssemblies = new ConcurrentDictionary<string, Assembly>();
            m_FailedAssemblies = new ConcurrentDictionary<string, Assembly>();

        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Add an assembly to the communication session, in its corresponding queue
        /// </summary>
        /// <param name="assembly">The assembly to be added</param>
        /// <param name="status">The status of the assembly </param>
        public void Add(Assembly assembly, ProcessStatus status)
        {
            m_ProcessedAssemblies.Add(assembly.Guid, status);
            switch (status)
            {
                case ProcessStatus.UPDATE:
                    m_WaitingForVersion.Enqueue(assembly);
                    break;

                case ProcessStatus.WAITING_FOR_ID:
                    m_WaitingForChildIds.Enqueue(assembly);
                    break;

                case ProcessStatus.NEW:
                    m_NewAssemblies.Enqueue(assembly);
                    break;
                default:
                    m_FailedAssemblies.TryAdd(assembly.Guid, assembly);
                    break;
            }
        }

        /// <summary>
        /// Execute an step in the progress of the sesion
        /// </summary>
        /// <param name="number"> The number of steps to do</param>
        public void Step(int number = 1)
        {
            // Wait for the lock
            lock (progressNumberLock)
            {
                m_ProgressStatus += number;
            }
        }

        /// <summary>
        /// Description of the result of this session
        /// </summary>
        /// <returns>A string of the description</returns>
        public string Summary()
        {
            return string.Format("Processed: {0}. Correctly: {1}, Failed: {2}", m_ProcessedAssemblies.Count, m_ReadyAssemblies.Count, m_FailedAssemblies.Count);
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Verify if all the child of the assembly have an id now, and send the assembly to its corresponding list
        /// </summary>
        /// <param name="assembly">The assebly been processed in this iteration</param>
        /// <param name="worker">The worker in charge of doing this task</param>
        private void ReprocessAssembly(Assembly assembly, BackgroundWorker worker)
        {
            // Ready is going to be true if every waiting child has an id now
            var ready = true;

            // Helper list to remove the already updated relations
            var removeList = new List<string>();
            foreach (KeyValuePair<string, List<Relation>> entry in assembly.WaitingChildIds)
            {
                // Verify if the child is in the ready dictionary
                Assembly childAssembly;
                if (m_ReadyAssemblies.TryGetValue(entry.Key, out childAssembly))
                {
                    //Iterate over all the instances of this assembly
                    foreach (Relation relation in entry.Value)
                    {
                        // Set the id of the child
                        relation.Id = childAssembly.IdAssembly;

                        // Add the relation to the list of relation of the assembly
                        assembly.ListOfRelations.Add(relation);
                    }

                    // Add the key into the helper list to remove this relation later, from the waiting child list;
                    removeList.Add(entry.Key);
                }
                // Verify if the child is in the failed dictionary
                else if (m_FailedAssemblies.TryGetValue(entry.Key, out childAssembly))
                {
                    // Add this assembly to the failed dictionary
                    m_FailedAssemblies.TryAdd(assembly.Guid, assembly);

                    Step(4);

                    worker.ReportProgress(ProgressStatus, new StatusMessage(assembly.DocumentName, string.Format("Failed because ${0} document was not sent correctly.", childAssembly.DocumentName), messageComm: MessageComm.SENDED));
                    return;
                }
                // If the child is not in any list, it dont have an id yet
                else
                {
                    ready = false;
                }
            }

            // Remove all the ready relations
            foreach (var key in removeList)
            {
                assembly.WaitingChildIds.Remove(key);
            }

            // If the assembly is ready, pass it to the corresponding queue
            if (ready)
            {
                if (assembly.CanBeUpdated)
                    m_UpdateAssemblies.Enqueue(assembly);
                else if (assembly.CanBeSent)
                    m_NewAssemblies.Enqueue(assembly);
                else
                    Console.Write("This can not be happening");
            }
            else
            {
                // If the assembly is not ready, re-entered into the waiting queue
                m_WaitingForChildIds.Enqueue(assembly);
            }
        }
        #endregion

        #region Communication Processes
        /// <summary>
        /// The main of the sender thread. Sends every message present in any queue
        /// </summary>
        public void Sender(BackgroundWorker worker)
        {
            // Stop if the session has ended
            while (!SessionComplete)
            {
                if (CommTool.Instance.IsConnected)
                {
                    // Extract one assembly from one of the queues
                    Assembly currentAssembly;
                    if (m_NewAssemblies.TryDequeue(out currentAssembly))
                    {
                        uint idResponse;
                        try
                        {
                            // Send the new assembly
                            idResponse = currentAssembly.SendNew();
                        }
                        catch (SocketException e)
                        {
                            //If send fails, resave the current assembly
                            m_NewAssemblies.Enqueue(currentAssembly);
                            continue;
                        }

                        // Do one step
                        Step();

                        // Report to the user
                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "New assembly sent correctly", messageComm: MessageComm.SENDED));

                        // Add it to the waiting list, using the idResponse number as identifier
                        m_WaitingForResponse.TryAdd(idResponse, currentAssembly);

                        // Restart loop
                        continue;
                    }
                    if (m_WaitingForVersion.TryDequeue(out currentAssembly))
                    {
                        // Create a request for version of an assembly
                        string message = "{\"id\": " + currentAssembly.IdAssembly + "," + "\"version\":" + currentAssembly.Version + "}";

                        uint idResponse;
                        try
                        {
                            // Send the request and store the idResponse
                            idResponse = CommTool.Instance.SendMessage(message, HeaderPacketComm.Command.GET_VERSION_ID);
                        }
                        catch (SocketException e)
                        {
                            //If send fails, resave the current assembly
                            m_WaitingForVersion.Enqueue(currentAssembly);
                            continue;
                        }

                        Step();

                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Id request sent correctly", messageComm: MessageComm.SENDED));

                        // Add the assembly to the waiting list, using the idResponse number as identifier
                        m_WaitingForResponse.TryAdd(idResponse, currentAssembly);
                        continue;
                    }
                    if (m_UpdateAssemblies.TryDequeue(out currentAssembly))
                    {
                        uint idResponse;
                        try
                        {
                            // Send an update of an assembly to the server
                            idResponse = currentAssembly.SendUpdate();
                        }
                        catch (SocketException e)
                        {
                            //If send fails, resave the current assembly
                            m_UpdateAssemblies.Enqueue(currentAssembly);
                            continue;
                        }


                        Step();

                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Update request sent correctly", messageComm: MessageComm.SENDED));

                        // Add the assembly to the waiting list, using the idResponse number as identifier
                        m_WaitingForResponse.TryAdd(idResponse, currentAssembly);

                        // Restart loop
                        continue;
                    }
                    if (m_WaitingForChildIds.TryDequeue(out currentAssembly))
                    {
                        // Verify if all the child of the assembly have an id now
                        ReprocessAssembly(currentAssembly, worker);

                        // Restart loop
                        continue;
                    }
                }
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// The main of the receiver thread. Process every message received in the socket
        /// <param name="worker">The worker in charge of doing this task</param>
        /// </summary>
        public void Receiver(BackgroundWorker worker)
        {
            // Stop if the session has ended
            while (!SessionComplete)
            {
                if (CommTool.Instance.IsConnected)
                {
                    // Header to be received
                    HeaderPacketComm header;

                    // Message to be received
                    string inMessage;
                    try
                    {
                        CommTool.Instance.Receive(out header, out inMessage);
                    }
                    catch (SocketException e)
                    {
                        Debug.Print(e.Message);
                        continue;
                    }


                    // Try to match the received id with a waiting message
                    Assembly currentAssembly;
                    if (m_WaitingForResponse.TryRemove(header.m_idResponse, out currentAssembly))
                    {
                        // Process the message accordingly to the command
                        switch (header.m_command)
                        {
                            case HeaderPacketComm.Command.NEW_ASSEMBLY:
                                {
                                    Step(3);

                                    // If the return message is not an OK_RESPONSE, mark as failed
                                    if (header.m_statusComm != HeaderPacketComm.StatusServer.OK_RESPONSE)
                                    {
                                        m_FailedAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Failed server response", ProcessStatus.FAIL, MessageComm.RECEIVED));
                                    }
                                    else
                                    {
                                        // Process the incomming message
                                        dynamic parsedMessage = JObject.Parse(inMessage);
                                        try
                                        {
                                            //Try to get the id of the message
                                            currentAssembly.AddId((int)parsedMessage.id);
                                            currentAssembly.Save();
                                        }
                                        catch (Exception e)
                                        {
                                            // If there is an error, mark as failed
                                            Console.WriteLine(e.Message);
                                            m_FailedAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Failed parsing received message", ProcessStatus.FAIL, MessageComm.RECEIVED));
                                            break;
                                        }

                                        // If everything is ok, mark as ready
                                        m_ReadyAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "New assembly received correctly", ProcessStatus.NEW, MessageComm.RECEIVED));
                                    }
                                }
                                break;
                            case HeaderPacketComm.Command.GET_VERSION_ID:
                                {
                                    Step();
                                    // If the return message is not an OK_RESPONSE, mark as failed
                                    if (header.m_statusComm != HeaderPacketComm.StatusServer.OK_RESPONSE)
                                    {
                                        m_FailedAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Failed server response", ProcessStatus.FAIL, MessageComm.RECEIVED));
                                    }
                                    else
                                    {
                                        // Process the incomming message
                                        dynamic parsedMessage = JObject.Parse(inMessage);

                                        // Try to obtain the info from the parsedMessage
                                        int id;
                                        uint version;
                                        try
                                        {
                                            id = parsedMessage.id;
                                            version = parsedMessage.version;
                                        }
                                        catch (Exception e)
                                        {
                                            // If there is an error, mark as failed
                                            Console.WriteLine(e.Message);
                                            Step(2);
                                            m_FailedAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Failed parsing received message", ProcessStatus.FAIL, MessageComm.RECEIVED));
                                            break;
                                        }
                                        // If the ids don't match, mark as failed
                                        if (id != currentAssembly.IdAssembly)
                                        {
                                            Step(2);
                                            m_FailedAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Failed server response", ProcessStatus.FAIL, MessageComm.RECEIVED));
                                            break;
                                        }

                                        // If the version in the server is lower, update the assembly
                                        if (version < currentAssembly.Version)
                                        {
                                            m_UpdateAssemblies.Enqueue(currentAssembly);
                                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly need to be updated", ProcessStatus.UPDATE, MessageComm.RECEIVED));
                                        }
                                        else
                                        {
                                            // If everything is ok, mark as ready
                                            m_ReadyAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                            Step(2);
                                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly up to date", ProcessStatus.UPDATE, MessageComm.RECEIVED));
                                        }
                                    }
                                }
                                break;
                            case HeaderPacketComm.Command.UPDATE_ASSEMBLY:
                                {
                                    Step();
                                    // If the return message is not an OK_RESPONSE, mark as failed
                                    if (header.m_statusComm == HeaderPacketComm.StatusServer.OK_RESPONSE)
                                    {
                                        // If everything is ok, mark as ready
                                        m_ReadyAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly updated correctly", ProcessStatus.UPDATE, MessageComm.RECEIVED));
                                    }
                                    else
                                    {
                                        m_FailedAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Failed server response", ProcessStatus.FAIL, MessageComm.RECEIVED));
                                    }
                                }
                                break;
                            default:
                                // If the message has a different command, mark as failed
                                m_FailedAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);

                                Step();
                                worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Failed server response", ProcessStatus.FAIL, MessageComm.RECEIVED));
                                break;
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }

        #endregion

        #region Discovery Process

        /// <summary>
        /// Method that process an assembly document and check if every subpart and subassembly has been sent to the server and if it is updated.
        /// <param name="currentAssembly">The current assembly been processing</param>
        /// <param name="worker">The worker in charge of doing this task</param>
        /// </summary>
        public ProcessStatus ProcessAssemblyRecursively(Assembly currentAssembly, BackgroundWorker worker)
        {
            // If the current assembly is already been processed in the current session, skip this iteration
            ProcessStatus possibleResult;
            if (m_ProcessedAssemblies.TryGetValue(currentAssembly.Guid, out possibleResult))
            {
                return possibleResult;
            }

            var allSubOccurrencesValid = true;
            var waitingForId = false;
            if (!currentAssembly.IsPart)
            {
                // Access to the colection of occurrences
                object[] allComponents = (object[])currentAssembly.AssemblyDocument.GetComponents(true);

                // True if all the suboccurrences are already in the server

                // Verify that all the subassemblies and subparts have an id
                for (int i = 0; i < allComponents.Length; i++)
                {
                    // Access to the occurrence
                    Component2 component = (Component2)allComponents[i];
                    object modelDoc = component.GetModelDoc2();
                    var componentAssembly = new Assembly(new Model((ModelDoc2)modelDoc));

                    // Analize each occurrence
                    ProcessStatus result = ProcessAssemblyRecursively(componentAssembly, worker);

                    switch (result)
                    {
                        case ProcessStatus.UPDATE:
                            {
                                // Get the occurrence transform.
                                MatrixTransform transform = new MatrixTransform(component.Transform2);

                                // Create the relation between this assembly and it subassembly or subpart
                                Relation relation = new Relation(component.Name, componentAssembly.IdAssembly, new Position(transform.Translation(), transform.EulerAngles()));

                                currentAssembly.ListOfRelations.Add(relation);
                            }
                            break;

                        case ProcessStatus.FAIL:
                            {
                                allSubOccurrencesValid = false;
                            }
                            break;

                        case ProcessStatus.WAITING_FOR_ID:
                        case ProcessStatus.NEW:
                            {
                                waitingForId = true;

                                // Get the occurrence transform.
                                MatrixTransform transform = new MatrixTransform(component.Transform2);

                                // Create the relation between this assembly and it subassembly or subpart
                                Relation relation = new Relation(component.Name, componentAssembly.IdAssembly, new Position(transform.Translation(), transform.EulerAngles()));

                                if (currentAssembly.WaitingChildIds.ContainsKey(componentAssembly.Guid))
                                {
                                    currentAssembly.WaitingChildIds[componentAssembly.Guid].Add(relation);
                                }
                                else
                                {
                                    var relationsList = new List<Relation>();
                                    relationsList.Add(relation);
                                    currentAssembly.WaitingChildIds.Add(componentAssembly.Guid, relationsList);
                                }

                            }
                            break;
                    }
                }
            }

            // If at leat one of the childs can not be sent to the server, neither the current assembly
            if (!allSubOccurrencesValid)
            {
                worker.ReportProgress(0, new StatusMessage(currentAssembly.DocumentName, "On of the assembly childs cannot be sent"));
                Add(currentAssembly, ProcessStatus.FAIL);
                return ProcessStatus.FAIL;
            }

            // If some of the child are waiting for id, mark as waiting
            if (waitingForId && (currentAssembly.CanBeUpdated || currentAssembly.CanBeSent))
            {
                worker.ReportProgress(0, new StatusMessage(currentAssembly.DocumentName, "Waiting for child id"));
                Add(currentAssembly, ProcessStatus.WAITING_FOR_ID);
                return ProcessStatus.WAITING_FOR_ID;
            }

            // If it can be updated, mark for update
            if (currentAssembly.CanBeUpdated)
            {
                worker.ReportProgress(0, new StatusMessage(currentAssembly.DocumentName, "Ready for update"));
                Add(currentAssembly, ProcessStatus.UPDATE);
                return ProcessStatus.UPDATE;
            }

            // If it can be sent, mark for send
            if (currentAssembly.CanBeSent)
            {
                worker.ReportProgress(0, new StatusMessage(currentAssembly.DocumentName, "Ready to be sent"));
                Add(currentAssembly, ProcessStatus.NEW);
                return ProcessStatus.NEW;
            }

            // If the current assembly cant be sent or updated, mark as failed
            worker.ReportProgress(0, new StatusMessage(currentAssembly.DocumentName, "Cannot be sent"));
            Add(currentAssembly, ProcessStatus.FAIL);
            return ProcessStatus.FAIL;
        }

        #endregion

        #region Exporting Process

        /// <summary>
        /// Process that export every part and assembly into a step file
        /// </summary>
        /// <param name="filepath">The place to save the files</param>
        /// <param name="worker">The worker in charge of doing this task</param>
        public void Export(string filepath, string rootAssemblyName, BackgroundWorker worker)
        {
            // Set the preferences of the export from assembly to part
            Dna.Application.UnsafeObject.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSaveAssemblyAsPartOptions, (int)swSaveAsmAsPartOptions_e.swSaveAsmAsPart_ExteriorFaces);
            int Errors = 0, Warnings = 0;

            // Process all the assemblies
            while (!m_WaitingForVersion.IsEmpty)
            {
                // Extract the current assembly
                Assembly currentAssembly;
                m_WaitingForVersion.TryDequeue(out currentAssembly);

                // If the model is already in the lastest version, do nothing
                if(currentAssembly.ModelVersion == currentAssembly.Version)
                {
                    Step(4);
                    worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Document has been already exported", messageComm: MessageComm.SENDED));
                    m_ReadyAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
                    continue;
                }

                // If the current assembly is a part, it can be converted immediately. If is not, it has to be transformed into a part first
                if (currentAssembly.IsPart)
                {
                    try
                    {
                        // Activate the part
                        var modelDoc = (ModelDoc2)Dna.Application.UnsafeObject.ActivateDoc3(currentAssembly.DocumentName, false, 0, ref Errors);

                        // Save the part as a step file
                        modelDoc.Extension.SaveAs(filepath + "\\" + currentAssembly.DocumentName + ".STEP", 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref Errors, ref Warnings);

                        // Report to the user
                        Step(4);
                        if (Errors == 0)
                        {
                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Part exported correctly", messageComm: MessageComm.SENDED));

                            // Update the model version
                            try
                            {
                                currentAssembly.UpdateModelVersion();
                            }
                            catch
                            {
                                var model = new Model(modelDoc);
                                model.SetCustomProperty("modelVersion", model.GetCustomProperty("version"));
                                model.UnsafeObject.Save3((int)(swSaveAsOptions_e.swSaveAsOptions_AvoidRebuildOnSave | swSaveAsOptions_e.swSaveAsOptions_Silent), ref Errors, ref Warnings);
                            }
                        }
                        else
                        {
                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Part failed to export", messageComm: MessageComm.SENDED));
                        }
                    }
                    catch
                    {
                        // Report to the user
                        Step(4);
                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Part failed to export. Open this part separately and then try to export it.", messageComm: MessageComm.SENDED));
                    }

                }
                else
                {
                    try
                    {
                        var modelDoc = (ModelDoc2)Dna.Application.UnsafeObject.ActivateDoc3(currentAssembly.DocumentName, false, 0, ref Errors);
                        // Save the assembly as a part
                        modelDoc.Extension.SaveAs(filepath + "\\" + currentAssembly.DocumentName + "part.SLDPRT", 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref Errors, Warnings);

                        // If it works, continue the process
                        if (Errors == 0)
                        {
                            // Report the middle progress
                            Step(2);
                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly transformed to a part", messageComm: MessageComm.SENDED));

                            // Open the recently exported part
                            ModelDoc2 exportedPart = Dna.Application.UnsafeObject.OpenDoc6(filepath + "\\" + currentAssembly.DocumentName + "part.SLDPRT", (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref Errors, ref Warnings);

                            // Save the part as a step file
                            exportedPart.Extension.SaveAs(filepath + "\\" + currentAssembly.DocumentName + ".STEP", 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref Errors, ref Warnings);

                            Dna.Application.UnsafeObject.CloseDoc(currentAssembly.DocumentName + "part.SLDPRT");
                            Step(2);

                            // If it works, report to the user
                            if (Errors == 0)
                            {
                                worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly exported correctly", messageComm: MessageComm.SENDED));

                                // Update the model version
                                try
                                {
                                    currentAssembly.UpdateModelVersion();
                                }
                                catch
                                {
                                    var model = new Model(modelDoc);
                                    model.SetCustomProperty("modelVersion", model.GetCustomProperty("version"));
                                    model.UnsafeObject.Save3((int)(swSaveAsOptions_e.swSaveAsOptions_AvoidRebuildOnSave | swSaveAsOptions_e.swSaveAsOptions_Silent), ref Errors, ref Warnings);
                                }
                            }
                            else
                            {
                                worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly failed to export", messageComm: MessageComm.SENDED));
                            }
                        }
                        else
                        {
                            Step(4);
                            worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly failed to be transformed to a part", messageComm: MessageComm.SENDED));
                        }
                    }
                    catch
                    {
                        Step(4);
                        worker.ReportProgress(ProgressStatus, new StatusMessage(currentAssembly.DocumentName, "Assembly failed to be exported. Try it again", messageComm: MessageComm.SENDED));
                    }
                }

                // Close the opened document
                if (currentAssembly.DocumentName != rootAssemblyName)
                    Dna.Application.UnsafeObject.CloseDoc(currentAssembly.DocumentName);

                // Add the current document to the ready list
                m_ReadyAssemblies.TryAdd(currentAssembly.Guid, currentAssembly);
            }
        }
        #endregion
    }
}
