using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoLJV.Support;
using DeviceBatchGenerics.Support;
using DeviceBatchGenerics.Support.Bases;
using AsyncCommands;

namespace AutoLJV.Instrument_Control
{
    public class RasPiController : NotifyUIBase
    {
        public RasPiController()
        {
            Debug.WriteLine("RasPiController init");
            PiIdleCommand = AsyncCommand.Create(() => PiIdleAsync());
        }
        #region Members
        TCPComm tCPComm = new TCPComm();
        //TCPImageReceiver tCPImageReceiver = new TCPImageReceiver(); can't have two of these and this seems to belong in LJVScanCoordinator.cs
        string _gCodePiCommand = "";
        string _selectedPixel;
        private List<string> _piPixelsList = new List<string>()
        {
            "SiteA", "SiteB","SiteC","SiteD"
        };

        #endregion
        #region Properties
        public List<string> PiPixelsList
        {
            get { return _piPixelsList; }
            set
            {
                _piPixelsList = value;
                OnPropertyChanged();
            }
        }
        public string SelectedPixel
        {
            get { return _selectedPixel; }
            set
            {
                _selectedPixel = value;
                OnPropertyChanged();
                OnSelectedPixelChanged();
            }
        }
        public string GCodePiCommand
        {
            get { return _gCodePiCommand; }
            set
            {
                _gCodePiCommand = value;
                OnPropertyChanged();
                Debug.WriteLine("GCodePiCommand changed");
            }
        }
        public Dictionary<string, string> ActiveCNCCoordsDict { get; set; }
        #endregion
        #region Methods
        private void OnSelectedPixelChanged()
        {
            if (SelectedPixel == "SiteA")
                PiPixelAExecute();
            if (SelectedPixel == "SiteB")
                PiPixelBExecute();
            if (SelectedPixel == "SiteC")
                PiPixelCExecute();
            if (SelectedPixel == "SiteD")
                PiPixelDExecute();
        }
        public void SendPiString(string s)
        {
            tCPComm.SendCommand(s);
        }
        #endregion
        #region PiCommands
        private RelayCommand _PiInitialize;
        public ICommand PiInitialize
        {
            get
            {
                if (_PiInitialize == null)
                {
                    _PiInitialize = new RelayCommand(param => this.PiInitializeExecute());
                }
                return _PiInitialize;
            }
        }
        public void PiInitializeExecute()
        {
            tCPComm.SendCommand("Initialize");
        }
        private RelayCommand _PiIdle;
        public ICommand PiIdle
        {
            get
            {
                if (_PiIdle == null)
                {
                    _PiIdle = new RelayCommand(param => this.PiIdleExecute());
                }
                return _PiIdle;
            }
        }
        public IAsyncCommand PiIdleCommand { get; set; }
        public async Task PiIdleAsync()
        {
            await Task.Run(() =>
            {
                tCPComm.SendCommand("Return");
            }).ConfigureAwait(false);
        }
        public void PiIdleExecute()
        {
            tCPComm.SendCommand("Return");
        }
        private RelayCommand _PiPicture;
        public ICommand PiPicture
        {
            get
            {
                if (_PiPicture == null)
                {
                    _PiPicture = new RelayCommand(param => this.PiPictureExecute());
                }
                return _PiPicture;
            }
        }
        public void PiPictureExecute()
        {
            Debug.WriteLine("PiPictureExecute");
            InstrumentService.LJVScanCoordinator.TheImageReceiver.WaitingForImage = true;
            tCPComm.SendCommand("Picture");
            //System.Threading.Thread.Sleep(1000);
        }
        private RelayCommand _PiSwap;
        public ICommand PiSwap
        {
            get
            {
                if (_PiSwap == null)
                {
                    _PiSwap = new RelayCommand(param => this.PiSwapExecute());
                }
                return _PiSwap;
            }
        }
        public void PiSwapExecute()
        {
            tCPComm.SendCommand("Swap");
        }
        private RelayCommand _PiPixelA;
        public ICommand PiPixelA
        {
            get
            {
                if (_PiPixelA == null)
                {
                    _PiPixelA = new RelayCommand(param => this.PiPixelAExecute());
                }
                return _PiPixelA;
            }
        }
        public void PiPixelAExecute()
        {
            tCPComm.SendCommand("A");
            tCPComm.SendCommand(ActiveCNCCoordsDict["PixelA"]);
        }
        private RelayCommand _PiPixelB;
        public ICommand PiPixelB
        {
            get
            {
                if (_PiPixelB == null)
                {
                    _PiPixelB = new RelayCommand(param => this.PiPixelBExecute());
                }
                return _PiPixelB;
            }
        }
        public void PiPixelBExecute()
        {
            tCPComm.SendCommand("B");
            tCPComm.SendCommand(ActiveCNCCoordsDict["PixelB"]);
        }
        private RelayCommand _PiPixelC;
        public ICommand PiPixelC
        {
            get
            {
                if (_PiPixelC == null)
                {
                    _PiPixelC = new RelayCommand(param => this.PiPixelCExecute());
                }
                return _PiPixelC;
            }
        }
        public void PiPixelCExecute()
        {
            tCPComm.SendCommand("C");
            tCPComm.SendCommand(ActiveCNCCoordsDict["PixelC"]);

        }
        private RelayCommand _PiPixelD;
        public ICommand PiPixelD
        {
            get
            {
                if (_PiPixelD == null)
                {
                    _PiPixelD = new RelayCommand(param => this.PiPixelDExecute());
                }
                return _PiPixelD;
            }
        }
        public void PiPixelDExecute()
        {
            tCPComm.SendCommand("D");
            tCPComm.SendCommand(ActiveCNCCoordsDict["PixelD"]);
        }
        private RelayCommand _PiRestON;
        public ICommand PiRestON
        {
            get
            {
                if (_PiRestON == null)
                {
                    _PiRestON = new RelayCommand(param => this.PiRestONExecute());
                }
                return _PiRestON;
            }
        }
        void PiRestONExecute()
        {
            tCPComm.SendCommand("RestON");
        }
        private RelayCommand _SendGCodePiCommand;
        public ICommand SendGCodePiCommand
        {
            get
            {
                if (_SendGCodePiCommand == null)
                {
                    _SendGCodePiCommand = new RelayCommand(param => this.SendGCodePiCommandExecute());
                }
                return _SendGCodePiCommand;
            }
        }
        void SendGCodePiCommandExecute()
        {
            //tCPComm.SendCommand(string.Concat(GCodePiCommand, @"\n"));
            Debug.WriteLine("button works");
            tCPComm.SendCommand(GCodePiCommand);
        }
        #endregion
    }
}