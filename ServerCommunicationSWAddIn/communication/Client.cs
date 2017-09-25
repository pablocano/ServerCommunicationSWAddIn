
using System.Threading;

namespace ServerCommunicationSWAddIn.communication
{
    public class Client
    {
        private bool IsRunning;

        public void Start()
        {
            IsRunning = true;
            while (IsRunning)
            {
                if (!CommTool.Instance.IsConnected)
                {
                    // Start the communication asynchronusly
                    CommTool.Instance.StartCommunication();

                    // Wait for the connection to be ready or to fail
                    CommTool.connectDone.WaitOne();

                    //Try again
                    CommTool.connectDone.Reset();
                }


                if (!IsRunning)
                    break;
                Thread.Sleep(1000);
            }
        }

        public void Stop()
        {
            IsRunning = false;
            if (CommTool.Instance.IsConnected)
                CommTool.Instance.EndCommunication();
            else
                CommTool.Instance.CloseCommunication();
        }
    }
}
