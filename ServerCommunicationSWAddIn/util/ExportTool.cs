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
    public class ExportTool
    {
        #region InternalList

        private Dictionary<string, Assembly> m_ProcessedAssemblies;

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public ExportTool()
        {
            // Initialize the internal dictionary
            m_ProcessedAssemblies = new Dictionary<string, Assembly>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Method that process an assembly document and check if every subpart and subassembly has been sent to the server and if it is updated.
        /// <param name="currentAssembly">The current assembly been processing</param>
        /// <param name="worker">The worker in charge of doing this task</param>
        /// </summary>
        public bool ProcessAssemblyRecursively(Assembly currentAssembly)
        {
            // If this assembly has already been procesed, it is OK
            if (m_ProcessedAssemblies.ContainsKey(currentAssembly.Guid))
            {
                return true;
            }

            // If the current assembly is not in the server
            if (!currentAssembly.CanBeSent)
                return false;

            // If the current assembly is not a part, analize every child
            if (!currentAssembly.IsPart)
            {
                // Access to the colection of occurrences
                object[] allComponents = (object[])currentAssembly.AssemblyDocument.GetComponents(true);

                // Verify that all the subassemblies and subparts have an id
                for (int i = 0; i < allComponents.Length; i++)
                {
                    // Access to the occurrence
                    Component2 component = (Component2)allComponents[i];
                    object modelDoc = component.GetModelDoc2();
                    var componentAssembly = new Assembly(new Model((ModelDoc2)modelDoc));

                    // Analize each occurrence
                    var result = ProcessAssemblyRecursively(componentAssembly);

                    // Stop the recursion if any child is not in the server
                    if (!result)
                        return false;
                }
            }

            // If we are here is because all the child components are OK
            m_ProcessedAssemblies.Add(currentAssembly.Guid, currentAssembly);
            return true;
        }

        /// <summary>
        /// Process that export every part and assembly into a step file
        /// </summary>
        /// <param name="filepath">The place to save the files</param>
        /// <param name="worker">The worker in charge of doing this task</param>
        public void Export(string filepath, string rootAssemblyName)
        {
            // Set the preferences of the export from assembly to part
            Dna.Application.UnsafeObject.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSaveAssemblyAsPartOptions, (int)swSaveAsmAsPartOptions_e.swSaveAsmAsPart_ExteriorFaces);
            int Errors = 0, Warnings = 0;

            // Process all the assemblies
            foreach(var currentAssembly in m_ProcessedAssemblies.Values)
            {

                // If the model is already in the lastest version, do nothing
                if (currentAssembly.ModelVersion == currentAssembly.Version)
                {
                    continue;
                }

                // If the current assembly is a part, it can be converted immediately. If is not, it has to be transformed into a part first
                if (currentAssembly.IsPart)
                {
                    var sldworks = Dna.Application.UnsafeObject;
                    // Activate the part
                    var modelDoc = (ModelDoc2)sldworks.ActivateDoc3(currentAssembly.DocumentName, false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref Errors);

                    // Save the part as a step file
                    modelDoc.Extension.SaveAs(filepath + "\\" + currentAssembly.DocumentName + ".STEP", 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref Errors, ref Warnings);

                    // If OK, update the model version
                    if (Errors == 0)
                    {
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

                }
                else
                {
                    var modelDoc = (ModelDoc2)Dna.Application.UnsafeObject.ActivateDoc3(currentAssembly.DocumentName, false, 1, ref Errors);

                    // Do it silently and as a copy, updating anything thats needed before saving
                    var options = (int)(swSaveAsOptions_e.swSaveAsOptions_Copy | swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_UpdateInactiveViews);

                    // Save the assembly as a part
                    modelDoc.Extension.SaveAs(filepath + "\\" + currentAssembly.DocumentName + "part.SLDPRT", 0, options, null, ref Errors, Warnings);

                    // If it works, continue the process
                    if (Errors == 0)
                    {
                        // Open the recently exported part
                        ModelDoc2 exportedPart = Dna.Application.UnsafeObject.OpenDoc6(filepath + "\\" + currentAssembly.DocumentName + "part.SLDPRT", (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref Errors, ref Warnings);

                        // Save the part as a step file
                        exportedPart.Extension.SaveAs(filepath + "\\" + currentAssembly.DocumentName + ".STEP", 0, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref Errors, ref Warnings);

                        Dna.Application.UnsafeObject.CloseDoc(exportedPart.GetTitle());

                        // If it works, update the model version
                        if (Errors == 0)
                        {              
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
                    }
                }

                // Close the opened document
                if (currentAssembly.DocumentName != rootAssemblyName)
                    Dna.Application.UnsafeObject.CloseDoc(currentAssembly.DocumentName);
            }
        }
        #endregion
    }
}
