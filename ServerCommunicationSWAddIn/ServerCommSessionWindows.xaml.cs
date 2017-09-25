using System;
using System.Windows;
using ServerCommunicationSWAddIn.util;
using ServerCommunicationSWAddIn.core;
using System.ComponentModel;
using AngelSix.SolidDna;

namespace ServerCommunicationSWAddIn
{
    /// <summary>
    /// Interaction logic for ServerCommSessionWindows.xaml
    /// </summary>
    public partial class ServerCommSessionWindows : Window
    {
        #region Private Members
        /// <summary>
        /// The session in charge of communicating with the server
        /// </summary>
        private CommunicationSession m_Session;

        /// <summary>
        /// The root assembly been processed
        /// </summary>
        private Assembly m_StartAssembly;

        /// <summary>
        /// The worker in charge of iterate over all the tree of subassemblies
        /// </summary>
        private BackgroundWorker discover;

        /// <summary>
        /// The worker in charge of sending the messages to the server
        /// </summary>
        private BackgroundWorker senderWorker;

        /// <summary>
        /// The worker in charge of receiving all the messages from the server
        /// </summary>
        private BackgroundWorker receiverWorker;

        #endregion

        #region Constructor
        /// <summary>
        /// The constructor of the class
        /// </summary>
        /// <param name="document">The current document been processed</param>
        /// <param name="isPart">If the current document is a part or not</param>
        public ServerCommSessionWindows(Model document)
        {
            // Initialize the window
            InitializeComponent();

            // Both buttons are disabled initially
            btCancel.IsEnabled = false;
            btOk.IsEnabled = false;

            //
            lbResults.FontFamily = new System.Windows.Media.FontFamily("Consolas");

            // Create the communication session
            m_Session = new CommunicationSession();

            // Create the first assembly
            m_StartAssembly = new Assembly(document);

            // Initialize all the workers
            InitializeWorkers();
        }
        #endregion

        #region Initializations
        /// <summary>
        /// Initialize all the workers
        /// </summary>
        private void InitializeWorkers()
        {
            // Create all the workers
            discover = new BackgroundWorker();
            senderWorker = new BackgroundWorker();
            receiverWorker = new BackgroundWorker();

            // Configure the discover worker
            discover.WorkerReportsProgress = true;
            discover.DoWork += new DoWorkEventHandler(discover_DoWork);
            discover.RunWorkerCompleted += new RunWorkerCompletedEventHandler(discover_ProcessComplete);
            discover.ProgressChanged += new ProgressChangedEventHandler(discover_ProcessChanged);

            // Configure the sender worker
            senderWorker.WorkerReportsProgress = true;
            senderWorker.DoWork += new DoWorkEventHandler(sender_DoWork);
            senderWorker.ProgressChanged += new ProgressChangedEventHandler(sender_ProcessChanged);

            // Configure the receiver worker
            receiverWorker.WorkerReportsProgress = true;
            receiverWorker.DoWork += new DoWorkEventHandler(receiver_DoWork);
            receiverWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(receiver_ProcessComplete);
            receiverWorker.ProgressChanged += new ProgressChangedEventHandler(receiver_ProcessChanged);
        }

        /// <summary>
        /// Function that is called when the windows is created
        /// </summary>
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            // Set the initial message
            statusLabel.Text = "Searching for all the assemblies and parts to send";

            lbResults.Text = string.Format("  {0,-30}{1,50}", "Documents", "Status") + "\n" + new string('-', 82) + "\n";
            lbResults.ScrollToEnd();

            // Start the discovery worker
            discover.RunWorkerAsync();
        }
        #endregion

        #region Buttons
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // If the session had ended, close the window
            if (m_Session.SessionComplete)
            {
                Close();
            }

            // Set the second message
            if (m_StartAssembly.IsPart)
                statusLabel.Text = "Sending " + m_StartAssembly.DocumentName + "to the server";
            else
                statusLabel.Text = "Sending all the subassemblies and subparts of " + m_StartAssembly.DocumentName;

            //Set the initial message

            // Disable the buttos while the messages are been processed
            btCancel.IsEnabled = false;
            btOk.IsEnabled = false;

            // Set the maximum value of the progress bar
            pbStatus.Maximum = m_Session.TotalSteps;

            // Start the sender worker
            senderWorker.RunWorkerAsync();

            // Start the receiver worker
            receiverWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Close the window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion

        #region Discovery Worker
        /// <summary>
        /// Actual work function of the discover worker
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        void discover_DoWork(object sender, DoWorkEventArgs e)
        {
            // Recover the sender worker
            BackgroundWorker worker = sender as BackgroundWorker;

            // Analize all assemblies recursively
            m_Session.ProcessAssemblyRecursively(m_StartAssembly, worker);
        }

        /// <summary>
        /// Event raised when the dicovery worker its completed
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        void discover_ProcessComplete(object sender, RunWorkerCompletedEventArgs e)
        {

            // Add a line in the text box to separate the processes
            lbResults.Text += new string('-', 82) + "\n\n";
            lbResults.ScrollToEnd();

            if (m_Session.SessionComplete)
            {
                // Set the progress bar for the next process
                pbStatus.IsIndeterminate = false;
                pbStatus.Value = 100;

                // Enable the buttons
                btCancel.IsEnabled = true;
                btOk.IsEnabled = false;

                // Focus the cancel button
                btCancel.Focus();

                // Change the text shown to the user
                currentProcess.Text = "";
                statusLabel.Text = "No assembly can be sent";
            }
            else
            {
                // Set the progress bar for the next process
                pbStatus.IsIndeterminate = false;
                pbStatus.Value = 0;

                // Enable the buttons
                btCancel.IsEnabled = true;
                btOk.IsEnabled = true;

                // Focus the send button
                btOk.Focus();

                // Change the text shown to the user
                currentProcess.Text = "";
                statusLabel.Text = "Ready to send";
            }
        }

        /// <summary>
        /// Event raised when the discovery process wants to change something from the window
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        void discover_ProcessChanged(object sender, ProgressChangedEventArgs e)
        {
            // Recover the status message
            var message = e.UserState as CommunicationSession.StatusMessage;

            // Add the current assembly to the list
            lbResults.Text += (string.Format("  {0,-30}{1,50}\n", message.m_Name, message.m_Message));
            lbResults.ScrollToEnd();

            // Set the current process label
            currentProcess.Text = "Adding " + message.m_Name;
        }
        #endregion

        #region Sender Worker
        /// <summary>
        /// Actual work function of the receiver worker
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        private void sender_DoWork(object sender, DoWorkEventArgs e)
        {
            // Recover the sender worker
            BackgroundWorker worker = sender as BackgroundWorker;

            // Start the sender process
            m_Session.Sender(worker);
        }

        /// <summary>
        /// Event raised when the dicovery worker its completed
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        private void sender_ProcessChanged(object sender, ProgressChangedEventArgs e)
        {
            // Recover the status message
            var message = e.UserState as CommunicationSession.StatusMessage;

            // Set the details label
            currentProcess.Text = message.m_Name + " sent";

            // Add the current process to the list
            lbResults.Text += (string.Format("  {0,-30}{1,50}\n", message.m_Name, message.m_Message));
            lbResults.ScrollToEnd();

            // Update the progress
            pbStatus.Value = e.ProgressPercentage;
        }

        #endregion

        #region Receiver Worker
        /// <summary>
        /// Actual work function of the receiver worker
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        private void receiver_DoWork(object sender, DoWorkEventArgs e)
        {
            // Recover the sender worker
            BackgroundWorker worker = sender as BackgroundWorker;

            // Start the receiver process
            m_Session.Receiver(worker);
        }
        /// <summary>
        /// Event raised when the receiver process wants to change something from the window
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        private void receiver_ProcessChanged(object sender, ProgressChangedEventArgs e)
        {
            // Recover the status message
            var message = e.UserState as CommunicationSession.StatusMessage;

            // Set the details label
            currentProcess.Text = message.m_Name + " received";

            // Add the current process to the list
            lbResults.Text += (string.Format("  {0,-30}{1,50}\n", message.m_Name, message.m_Message));
            lbResults.ScrollToEnd();

            // Update the progress
            pbStatus.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// Event raised when the receiver worker its completed
        /// </summary>
        /// <param name="sender">The object that raise the event</param>
        /// <param name="e">The argument of the event</param>
        private void receiver_ProcessComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // Set the status labels
            currentProcess.Text = "";
            statusLabel.Text = "Process complete";

            //Hide the cancel button
            btCancel.Visibility = Visibility.Hidden;

            // Enable the ok button
            btOk.IsEnabled = true;

            // Change the button content to finish
            btOk.Content = "Finish";
            btOk.Focus();

            // Show the summary
            lbResults.Text += new string('-', 82) + "\n" + m_Session.Summary();
            lbResults.ScrollToEnd();
        }


        #endregion


    }
}
