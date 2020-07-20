using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using DeviceBatchGenerics.Support;
using DeviceBatchGenerics.Support.Bases;
using DeviceBatchGenerics.Support.DataMapping;
using EFDeviceBatchCodeFirst;
using AutoLJV.Support;

namespace AutoLJV.Instrument_Control
{
    public class LJVScanCoordinator : NotifyUIBase
    {
        #region Members
        decimal presentLuminance = 0;
        decimal presentVoltage = 0;
        decimal presentCurrent = 0;
        decimal presentPhotoCurrent = 0;
        double assumedAlpha;
        public double ELSpecCurrent { get; set; }
        public double ELSpecPhotoCurrent { get; set; }
        decimal[] sourceRanges = new decimal[] { 1.05E-6m, 1.05E-5m, 1.05E-4m, 1.05E-3m, 1.05E-2m, 1.05E-1m };
        decimal[] picoRanges = new decimal[] { 2E-9m, 2E-8m, 2E-7m, 2E-6m, 2E-5m, 2E-4m, 2E-3m, 2E-2m };
        string _saveDirectory = "Path not set";
        string batchTestSystem = "not set";
        RasPiController _theRasPiController = new RasPiController();
        Keithley2400Controller KE2400;
        Keithley6485Controller KE6485;
        PRCameraController PRCamera;
        TCPImageReceiver theImageReceiver = new TCPImageReceiver();
        ObservableCollection<RawLJVDatum> _rawLJVData = new ObservableCollection<RawLJVDatum>();
        ObservableCollection<ELSpecDatum> _elSpecData = new ObservableCollection<ELSpecDatum>();
        Dictionary<string, double> _redAlphaDict;
        Dictionary<string, double> _greenAlphaDict;
        Dictionary<string, double> _blueAlphaDict;
        LJVScanSpec _activeSpec = new LJVScanSpec
        {
            DeviceDwellTime = 1000,
            ShouldRecordSpectrumAtEachStep = false,
            StartVoltage = 0,
            StopVoltage = 5,
            StepSize = 0.1m,
            StopCurrent = 10,
            StopLuminance = 10000,
            ActiveArea = 4E-6m
        };
        Dictionary<string, Dictionary<string, string>> _coordsDictsDict;
        private Dictionary<string, string> _selectedCoordsDict;
        private TIS.Imaging.ICImagingControl theImagingControl;

        #endregion
        #region Properties
        public decimal PresentVoltage
        {
            get { return presentVoltage; }
            set
            {
                presentVoltage = value;
                OnPropertyChanged();
            }
        }
        public decimal PresentCurrent
        {
            get { return presentCurrent; }
            set
            {
                presentCurrent = value;
                OnPropertyChanged();
            }
        }
        public event EventHandler VoltageSweepFinished;
        public ObservableCollection<RawLJVDatum> RawLJVData
        {
            get { return _rawLJVData; }
            set
            {
                _rawLJVData = value;
                OnPropertyChanged();
                Debug.WriteLine("RawLJVData changed");
            }
        }
        public ObservableCollection<ELSpecDatum> ELSpecData
        {
            get { return _elSpecData; }
            set { _elSpecData = value; OnPropertyChanged(); }
        }
        public string SaveDirectory
        {
            get { return _saveDirectory; }
            set
            {
                _saveDirectory = value;
                OnPropertyChanged();
            }
        }
        public RasPiController TheRasPiController
        {
            get { return _theRasPiController; }
            set
            {
                _theRasPiController = value;
                OnPropertyChanged();
            }
        }
        public LJVScanSpec ActiveLJVScanSpec
        {
            get { return _activeSpec; }
            set
            {
                _activeSpec = value;
                OnPropertyChanged();
            }
        }

        public TCPImageReceiver TheImageReceiver
        {
            get { return theImageReceiver; }
            set
            {
                theImageReceiver = value;
                OnPropertyChanged();
            }
        }
        public PRCameraController ThePRCamera
        {
            get { return PRCamera; }
        }
        public Dictionary<string, Dictionary<string, string>> TheCoordsDictsDict
        {
            get { return _coordsDictsDict; }
            set
            {
                _coordsDictsDict = value;
                OnPropertyChanged();
            }
        }
        public Dictionary<string, string> SelectedCoordsDict
        {
            get { return _selectedCoordsDict; }
            set
            {
                _selectedCoordsDict = value;
                TheRasPiController.ActiveCNCCoordsDict = _selectedCoordsDict;
                OnPropertyChanged();
                Debug.WriteLine("SelectedCoordsDict changed");
            }
        }

        public TIS.Imaging.ICImagingControl TheImagingControl
        {
            get { return theImagingControl; }
            set
            {
                theImagingControl = value;
                OnPropertyChanged();
            }
        }
        #endregion
        private async Task<LJVScanCoordinator> InitializeAsync()
        {
            return await Task.Run(() =>
            {
                PRCamera = new PRCameraController();
                batchTestSystem = ConfigurationManager.AppSettings.Get("BatchTestSystem");
                if (batchTestSystem == "BTS1")
                {
                    PRCamera.Initialize("\x0D\x0A", "\x0D\x0A", "B3");
                    TheCoordsDictsDict = CNCCoordinates.BTS1Coords;
                    SelectedCoordsDict = TheCoordsDictsDict["XinYan"];
                    //TheRasPiController.ActiveCNCCoordsDict = TheCoordsDictsDict["XinYan"];
                    //TheRasPiController.ActiveCNCCoordsDict = CNCCoordinates.StandardXinYanBTS1;
                }
                else if (batchTestSystem == "BTS2")
                {
                    PRCamera.Initialize("MODE", "\x0D\x0A", "PHOTO", "COM10", 115200);
                    TheCoordsDictsDict = CNCCoordinates.BTS2Coords;
                    SelectedCoordsDict = TheCoordsDictsDict["XinYan"];
                    //TheRasPiController.ActiveCNCCoordsDict = TheCoordsDictsDict["XinYan"];
                    //TheRasPiController.ActiveCNCCoordsDict = CNCCoordinates.StandardXinYanBTS2;
                }
                else
                    Debug.WriteLine("BatchTestSystem needs to be properly set in App.Config");
                return this;
            }).ConfigureAwait(false);
        }
        public static Task<LJVScanCoordinator> CreateAsync()
        {
            var ret = new LJVScanCoordinator();
            return ret.InitializeAsync();
        }
        public async Task RunVoltageSweep(CancellationToken token = new CancellationToken(), LJVScanSpec spec = null)
        {
            await Task.Run(async () =>
            {
                Debug.WriteLine("RunVoltageSweep Task");
                if (spec != null)
                    ActiveLJVScanSpec = spec;
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                RawLJVData = new ObservableCollection<RawLJVDatum>();
                KE2400 = await Keithley2400Controller.CreateAsync().ConfigureAwait(false);
                KE6485 = await Keithley6485Controller.CreateAsync().ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                await KE2400.TurnOnSource(ActiveLJVScanSpec.StartVoltage);
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                bool hasTakenCameraMeasurement = false;
                PRCamera.ExceededMeasurementRange = false;
                //if we're using the PR650 need to set params:
                //1:MS-75,2:Sl-1X,0:integration time,1:SI units (cd/m^2)
                if (batchTestSystem == "BTS1")
                    await PRCamera.SendCommandAndWaitForResponse("S1,2,,,,0,,1");
                PresentVoltage = ActiveLJVScanSpec.StartVoltage;
                presentLuminance = 0;
                presentCurrent = 0;
                ELSpecCurrent = -1;
                assumedAlpha = 3.33E9;
                decimal photoCurrentA;//need separate variable from presentphotocurrent to prevent errors with calcs
                int picoRangeCounter = 0;
                int sourceRangeCounter = 0;
                int timeoutCount = 0;
                while (!token.IsCancellationRequested && StopConditionsAreNotMet())
                {
                    if (PresentVoltage > ActiveLJVScanSpec.StartVoltage)
                        await KE2400.SetNextVoltageStep(PresentVoltage).ConfigureAwait(false);
                    //1st photocurrent reading
                    photoCurrentA = await KE6485.FetchPhotocurrentMeasurement().ConfigureAwait(false);
                    presentPhotoCurrent = photoCurrentA;
                    PresentCurrent = await KE2400.FetchCurrentMeasurement().ConfigureAwait(false);
                    //take a second measurement if range changes to smooth data from unstable devices
                    if (presentPhotoCurrent > picoRanges[picoRangeCounter])
                    {
                        photoCurrentA = await KE6485.FetchPhotocurrentMeasurement();
                        presentPhotoCurrent = photoCurrentA;
                        PresentCurrent = await KE2400.FetchCurrentMeasurement();
                        picoRangeCounter++;
                    }
                    //take a second measurement if range changes to smooth data from unstable devices
                    if (PresentCurrent > sourceRanges[sourceRangeCounter])
                    {
                        photoCurrentA = await KE6485.FetchPhotocurrentMeasurement();
                        presentPhotoCurrent = photoCurrentA;
                        PresentCurrent = await KE2400.FetchCurrentMeasurement();
                        sourceRangeCounter++;
                    }
                    if (presentCurrent != -1 && presentPhotoCurrent != -1)//-1 response == measurement timeout
                    {
                        var rawDatum = new RawLJVDatum
                        {
                            Voltage = PresentVoltage,
                            Current = presentCurrent,
                            PhotoCurrentA = photoCurrentA,
                            Resistance = Math.Round(presentVoltage / presentCurrent),//R=V/I
                            TimeStamp = DateTime.Now
                        };
                        Debug.WriteLine("PresentVoltage: " + PresentVoltage);
                        Debug.WriteLine("PresentCurrent: " + PresentCurrent);
                        //2nd photocurrent reading
                        rawDatum.PhotoCurrentB = await KE6485.FetchPhotocurrentMeasurement();
                        if (Convert.ToDouble(presentPhotoCurrent) * assumedAlpha > 42)
                        {
                            if (!PRCamera.ExceededMeasurementRange)
                            {
                                var cameraDatum = await PRCamera.LuminanceMeasurement();
                                if (!PRCamera.ExceededMeasurementRange)
                                {
                                    rawDatum.CameraLuminance = cameraDatum.Luminance;
                                    rawDatum.CameraCIEx = cameraDatum.CIEx;
                                    rawDatum.CameraCIEy = cameraDatum.CIEy;
                                    //3rd photocurrent reading
                                    rawDatum.PhotoCurrentC = await KE6485.FetchPhotocurrentMeasurement();
                                    presentPhotoCurrent = rawDatum.PhotoCurrentC;
                                    presentLuminance = cameraDatum.Luminance;
                                    assumedAlpha = Convert.ToDouble(cameraDatum.Luminance / rawDatum.PhotoCurrentC);
                                    //presentLuminance = cameraDatum.Luminance;
                                    Debug.Write("Luminance: " + rawDatum.CameraLuminance);
                                    Debug.Write("CIEx: " + rawDatum.CameraCIEx);
                                    Debug.WriteLine("CIEy: " + rawDatum.CameraCIEy);
                                    hasTakenCameraMeasurement = true;
                                }
                                else
                                {
                                    var alphaAndR2 = await DataProcessingService.LEDCalculator.AlphaFromRawData(RawLJVData.ToList());
                                    assumedAlpha = alphaAndR2.Item1;
                                }
                            }
                        }
                        else
                        {
                            rawDatum.PhotoCurrentC = rawDatum.PhotoCurrentA;
                        }
                        App.Current.Dispatcher.Invoke((Action)delegate
                        {
                            RawLJVData.Add(rawDatum);
                            RawLJVData.OrderByDescending(x => x.Voltage);
                        });
                        PresentVoltage += ActiveLJVScanSpec.StepSize;
                    }
                    else if (timeoutCount < 1)//measurement timed out so try again. ONE strikes and you're out
                    {
                        timeoutCount++;
                        await Task.Delay(1111);
                    }
                    else//fuck it we're skipping this step
                    {
                        PresentVoltage += ActiveLJVScanSpec.StepSize;
                    }
                }
                if (!token.IsCancellationRequested && !PRCamera.ExceededMeasurementRange)
                {
                    Debug.WriteLine("LJVScanCoordinator calls for ELSpecMeasurement");
                    presentPhotoCurrent = await KE6485.FetchPhotocurrentMeasurement();
                    var elspecMeas = await PRCamera.ELSpecMeasurement(hasTakenCameraMeasurement).ConfigureAwait(false);
                    ELSpecData = new ObservableCollection<ELSpecDatum>(elspecMeas);
                    //account for changing current during ELSpec measurement
                    ELSpecCurrent = Convert.ToDouble((PresentCurrent + await KE2400.FetchCurrentMeasurement()) / 2.0m);
                    ELSpecPhotoCurrent = Convert.ToDouble((presentPhotoCurrent + await KE6485.FetchPhotocurrentMeasurement()) / 2.0m);
                }
                else if (PRCamera.ExceededMeasurementRange)
                    await SetCurrentAndTakeELSpec().ConfigureAwait(false);
                Debug.WriteLine("closing keithleys");
                await Task.Delay(1111).ConfigureAwait(false);
                await KE2400.CloseGPIBDevice().ConfigureAwait(false);
                await KE6485.CloseGPIBDevice().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        public async Task SetCurrentAndTakePic(decimal setCurrent)
        {
            await Task.Run(async () =>
            {
                Debug.WriteLine("SetCurrentAndTakePic");
                await Task.Delay(1111).ConfigureAwait(false);
                KE2400 = await Keithley2400Controller.CreateAsync();
                await KE2400.SetCurrent(setCurrent).ConfigureAwait(false);
                await Task.Delay(1111).ConfigureAwait(false);
                TheRasPiController.PiPictureExecute();
                while (TheImageReceiver.WaitingForImage)
                    await Task.Delay(10).ConfigureAwait(false);
                await KE2400.CloseGPIBDevice().ConfigureAwait(false);
                Debug.WriteLine("SetCurrentAndTakePic finished");
            }).ConfigureAwait(false);
        }
        private async Task SetCurrentAndTakeELSpec()
        {
            await Task.Run(async () =>
            {
                Debug.WriteLine("SetCurrentAndTakeELSpec");
                KE2400 = await Keithley2400Controller.CreateAsync();
                KE6485 = await Keithley6485Controller.CreateAsync();
                ELSpecCurrent = Convert.ToDouble(await CalculateCurrentForELSpec());
                await KE2400.SetCurrent(Convert.ToDecimal(ELSpecCurrent));
                await Task.Delay(1111).ConfigureAwait(false);
                presentPhotoCurrent = await KE6485.FetchPhotocurrentMeasurement();
                ELSpecData = new ObservableCollection<ELSpecDatum>(await PRCamera.ELSpecMeasurement().ConfigureAwait(false));
                ELSpecPhotoCurrent = Convert.ToDouble((presentPhotoCurrent + await KE6485.FetchPhotocurrentMeasurement()) / 2.0m);

                await Task.Delay(1111).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        private async Task<decimal> CalculateCurrentForELSpec()
        {
            return await Task.Run(() =>
            {
                decimal specLuminance = 1111m;//heuristic value for taking spectrum
                decimal setCurrent = InstrumentService.LJVScanCoordinator.PresentCurrent;//use final current if device is dim
                                                                                         //find the maximum luminance value and add data to lists for interpolation
                decimal maxLuminance = 0;
                List<decimal> currentList = new List<decimal>();
                List<decimal> luminanceList = new List<decimal>();
                foreach (RawLJVDatum p in RawLJVData)
                {
                    if (p.CameraLuminance != null && p.CameraLuminance > maxLuminance)
                    {
                        maxLuminance = p.CameraLuminance ?? 0;
                    }
                    currentList.Add(p.Current);
                    luminanceList.Add(p.CameraLuminance ?? 0);
                }
                //interpolate the required set current
                int indexer = 0;
                while (indexer < luminanceList.Count - 1 && luminanceList[indexer] < specLuminance)
                {
                    indexer += 1;
                }
                if (indexer == luminanceList.Count || luminanceList[indexer] == 0)
                    setCurrent = currentList.Max();
                else
                    setCurrent = ((specLuminance - luminanceList[indexer - 1]) * ((currentList[indexer] - currentList[indexer - 1]) / (luminanceList[indexer] - luminanceList[indexer - 1])) + currentList[indexer - 1]);
                ELSpecCurrent = Convert.ToDouble(setCurrent);
                return setCurrent;
            }).ConfigureAwait(false);
        }
        /// <summary>
        /// evaluate whether the voltage sweep should stop based on various user-set conditions
        /// </summary>
        /// <returns></returns>
        private bool StopConditionsAreNotMet()
        {
            bool conditionsNotMet = true;
            if (presentVoltage > ActiveLJVScanSpec.StopVoltage | presentCurrent * 1000 >= ActiveLJVScanSpec.StopCurrent | presentLuminance >= ActiveLJVScanSpec.StopLuminance)
            {
                conditionsNotMet = false;
                PresentVoltage -= ActiveLJVScanSpec.StepSize;//to prevent files/entities naming with incorrect voltage
            }
            return conditionsNotMet;
        }
        /// <summary>
        /// Prevents subscribers from staying in memory when this class is instantiated by static class InstrumentService
        /// </summary>
        public void PurgeSubscribers()
        {
            if (VoltageSweepFinished != null)
            {
                foreach (var subscriber in VoltageSweepFinished.GetInvocationList())
                {
                    VoltageSweepFinished -= (subscriber as EventHandler);//unsubscribe from event
                }
            }
        }
        private void InitializeAlphaDictionaries()
        {
            //values determined experimentally
            _redAlphaDict = new Dictionary<string, double>
            {
                { "SiteA", 9.8E8 },
                { "SiteB", 8.3E8 },
                { "SiteC", 8.33E8 },
                { "SiteD", 8.33E8 }
            };
            _greenAlphaDict = new Dictionary<string, double>
            {
                { "SiteA", 5.3E9 },
                { "SiteB", 4.3E9 },
                { "SiteC", 4.8E9 },
                { "SiteD", 4.7E9 }
            };
            _blueAlphaDict = new Dictionary<string, double>
            {
                { "SiteA", 6.6E8 },
                { "SiteB", 5.2E8 },
                { "SiteC", 5.3E8 },
                { "SiteD", 5.2E8 }
            };
        }

        #region Commands
        private RelayCommand _UpdateChannelSavePath;

        public ICommand UpdateChannelSavePath
        {
            get
            {
                if (_UpdateChannelSavePath == null)
                {
                    _UpdateChannelSavePath = new RelayCommand(param => this.UpdateChannelSavePathExecute());
                }
                return _UpdateChannelSavePath;
            }
        }
        void UpdateChannelSavePathExecute()
        {
            FolderBrowserDialog folderBrowswer = new FolderBrowserDialog();
            if (folderBrowswer.ShowDialog() == DialogResult.OK)
            {
                SaveDirectory = folderBrowswer.SelectedPath;
            }
        }
        #endregion
    }
}
