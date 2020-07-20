using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Drawing;
using System.ComponentModel;
using System.Windows;
using DeviceBatchGenerics.Support.Bases;
using System.Configuration;

namespace AutoLJV.Support
{
    public class TCPImageReceiver : NotifyUIBase
    {
        public TCPImageReceiver()
        {
            string batchTestSystem = ConfigurationManager.AppSettings.Get("BatchTestSystem");
            if (batchTestSystem == "BTS1")
            {
                _port = 5005;
            }
            else if (batchTestSystem == "BTS2")
            {
                _port = 7007;
            }
            else
                MessageBox.Show("BatchTestSystem needs to be properly set in App.Config");
                //Debug.WriteLine("BatchTestSystem needs to be properly set in App.Config");
            Thread tcpServerRunThread = new Thread(new ThreadStart(TcpServerRun));
            tcpServerRunThread.Start();
        }
        #region members
        private int _port;
        private string _displayedImage;
        private string _picLabel;
        private string _filePath;
        private bool _waitingForImage = false;
        TcpListener tcpListener;
        #endregion
        #region Properties
        public ManualResetEvent[] ImageReceivedEvent = new ManualResetEvent[1] { new ManualResetEvent(false) };
        public string DisplayedImage
        {
            get { return _displayedImage; }
            set
            {
                _displayedImage = value;
                OnPropertyChanged();
            }
        }
        public string PicLabel
        {
            get { return _picLabel; }
            set
            {
                _picLabel = value;
                OnPropertyChanged();
            }
        }
        public string FilePath
        {
            get { return _filePath; }
            set
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }
        public bool WaitingForImage
        {
            get { return _waitingForImage; }
            set
            {
                _waitingForImage = value;
                OnPropertyChanged();
            }
        }
        public Bitmap ReceivedImage;
        #endregion
        #region Methods
        private void TcpServerRun()
        {
            tcpListener = new TcpListener(IPAddress.Any, _port);
            tcpListener.Start();
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                Thread tcpHandlerThread = new Thread(new ParameterizedThreadStart(tcpHandler));
                tcpHandlerThread.Start(client);
            }
        }
        private void tcpHandler(object client)
        {
            try
            {
                NetworkStream ns = null;
                TcpClient mClient = (TcpClient)client;
                ns = mClient.GetStream();
                if (mClient.Connected)
                {
                    //ImageReceivedEvent = new ManualResetEvent[1] { new ManualResetEvent(false) };
                    byte[] data = new byte[4];
                    ns.Read(data, 0, data.Length);
                    int size = BitConverter.ToInt32(data, 0);
                    Debug.WriteLine("Image size is: " + size + " bytes");
                    data = new byte[size];
                    int bytesReceived = 0;
                    while (bytesReceived != data.Length)
                    {
                        bytesReceived += ns.Read(data, bytesReceived, data.Length - bytesReceived);
                        //Debug.WriteLine("bytesReceived = " + bytesReceived);
                    }
                    MemoryStream ms = new MemoryStream(data);
                    ReceivedImage = new Bitmap(ms);
                    string SavePath = string.Concat(FilePath + PicLabel + ".jpg");
                    //ReceivedImage.Save(SavePath);
                    DisplayedImage = SavePath;
                    WaitingForImage = false;
                    mClient.Close();
                    //ImageReceivedEvent[0].Set();
                }
                mClient.Close();
                Debug.WriteLine("Closed TcpClient");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }
        public void SaveImage(string path)
        {
            //string SavePath = string.Concat(path + PicLabel + ".jpg");
            while (WaitingForImage)
                Thread.Sleep(1);
            ReceivedImage.Save(path);
        }
        public void Dispose()
        {
            tcpListener.Stop();
        }
        #endregion
    }
}
