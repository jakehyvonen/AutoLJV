using System;
using System.Text;
using System.Net.Sockets;
using System.Diagnostics;
using System.Configuration;

namespace AutoLJV.Support
{
    public class TCPComm
    {
        public TCPComm()
        {
            Initialize();
        }
        string _ipAddress;
        int _port;
        private void Initialize()
        {
            string batchTestSystem = ConfigurationManager.AppSettings.Get("BatchTestSystem");
            if (batchTestSystem == "BTS1")
            {
                _ipAddress = "192.168.1.39";
                _port = 5005;
            }
            else if (batchTestSystem == "BTS2")
            {
                _ipAddress = "192.168.1.104";
                _port = 7007;
            }
            else
                Debug.WriteLine("BatchTestSystem needs to be properly set in App.Config");

        }
        public void SendCommand(string _command)
        {
            try
            {
                //Debug.WriteLine("IP Address set to: " + _ipAddress);
                TcpClient client = new TcpClient();
                client.Connect(_ipAddress, _port);
                NetworkStream ns = client.GetStream();
                byte[] message = new byte[1024];
                message = Encoding.UTF8.GetBytes(_command);
                ns.Write(message, 0, message.Length);
                ns.Flush();
                client.Close();
                Debug.WriteLine("Sent command to RPi: " + _command);
            }
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show("Error: " + e);
            }
        }

    }
}
