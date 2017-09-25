using System;
using AngelSix.SolidDna;
using System.Collections.Generic;
using ServerCommunicationSWAddIn.communication;
using System.Threading;
using SolidWorks.Interop.swconst;

namespace ServerCommunicationSWAddIn
{
    /// <summary>
    /// Register as a SolidWorks Add-in
    /// </summary>
    public class SolidDnaAddinIntegration : AddInIntegration
    {
        /// <summary>
        /// Specific application start-up code
        /// </summary>
        /// <param name="solidWorks"></param>
        public override void ApplicationStartup()
        {
        }

        public override void PreLoadPlugIns()
        {

        }
    }

    public class SolidDnaIntegration : SolidPlugIn
    {
        // The thread that host a client, which is connected to the server
        private Thread ClientThread;

        // The client that mantains the connection with the server
        private Client m_Client;

        /// <summary>
        /// The description of this addin
        /// </summary>
        public override string AddInDescription { get { return "Sends the current model to the server"; }}

        /// <summary>
        /// The title of this addin
        /// </summary>
        public override string AddInTitle { get { return "Send to server"; } }

        /// <summary>
        /// Callback function triggered when the addin is conected to solidworks
        /// </summary>
        public override void ConnectedToSolidWorks()
        {
            // Create a client of the main server
            m_Client = new Client();

            // Start the client
            ClientThread = new Thread(new ThreadStart(m_Client.Start));
            ClientThread.Start();

            // Add a callback function to the ModelSaved event, to change the document version
            Dna.Application.ActiveFileSaved += Application_ActiveFileSaved;

            // Part commands
            var partGroup = Dna.Application.CommandManager.CreateCommands(
                title: "Server Communication",
                items: new List<CommandManagerItem>(new[] {

                    new CommandManagerItem {
                        Name = "Send to server",
                        Tooltip = "Send to server",
                        Hint = "Send the model to the server",
                        VisibleForDrawings = false,
                        OnClick = () =>
                        {
                            ButtonAction();
                        }
                    }

                }));
        }

        /// <summary>
        /// Callback function called when the current model is saved. It is used to update the vesion of the document
        /// </summary>
        private void Application_ActiveFileSaved(string arg1, Model model)
        {
            // Get the version
            string sVersion = model.GetCustomProperty("version");

            // Initialice the version if its empty
            if (sVersion == "")
            {
                model.SetCustomProperty("version", "1");
            }
            else
            {
                // Update the version
                int version = Convert.ToInt32(sVersion) + 1;
                model.SetCustomProperty("version", version.ToString());
            }

            // Remove the callback to avoid a loop
            Dna.Application.ActiveFileSaved -= Application_ActiveFileSaved;

            int Errors = 0, Warnings = 0;
            model.UnsafeObject.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref Errors, ref Warnings);

            // Restart the callback
            Dna.Application.ActiveFileSaved += Application_ActiveFileSaved;
        }

        /// <summary>
        /// Callback function of the sendToServer button
        /// </summary>
        public void ButtonAction()
        {
            // Verify that we are connected to the server
            if (!CommTool.Instance.IsConnected)
            {
                Dna.Application.ShowMessageBox("Solid Edge is not connected to the server", SolidWorksMessageBoxIcon.Stop);
                return;
            }

            // Process the assembly or the part accordingly
            if (!Dna.Application.ActiveModel.IsDrawing) 
            {
                ServerCommSessionWindows sessionWindow = new ServerCommSessionWindows(Dna.Application.ActiveModel);
                sessionWindow.ShowDialog();
            }
            else
            {
                // Error if the current document is not a assembly nor a part.
                Dna.Application.ShowMessageBox("You can only send part or assemblies to the server", SolidWorksMessageBoxIcon.Stop);
            }
        }

        public override void DisconnectedFromSolidWorks()
        {
        }
    }
}
