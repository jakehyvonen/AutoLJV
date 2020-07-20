using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AsyncCommands;
using AutoLJV.Instrument_Control;
using DeviceBatchGenerics.Support.Bases;
using DeviceBatchGenerics.Support.OriginSupport;
using DeviceBatchGenerics.Support;
using DeviceBatchGenerics.ViewModels.EntityVMs;
using EFDeviceBatchCodeFirst;
using System.Windows.Input;
using System.Windows;

namespace AutoLJV.ViewModels
{
    public class DeviceBatchScanVM : CrudVMBase
    {
        /*
        public DeviceBatchScanVM(DeviceBatch batch)
        {
            Initialize(batch);
        }
        */
        #region Members
        LJVScanSpec batchScanSpec = new LJVScanSpec
        {
            DeviceDwellTime = 1000,
            ShouldRecordSpectrumAtEachStep = false,
            StartVoltage = 0.0m,
            StopVoltage = 6.0m,
            StepSize = 0.1m,
            StopCurrent = 11.0m,
            StopLuminance = 7000,
            ActiveArea = 4E-6m,
            TestCondition = "t1.1"
        };
        SingleDeviceScanVM activeScanVM;
        ObservableCollection<SingleDeviceScanVM> scanVMs = new ObservableCollection<SingleDeviceScanVM>();
        DeviceBatch theDeviceBatch;
        Dictionary<string, int> delaysDict = new Dictionary<string, int>()
        {
            { "Initialize", -1 },
            {"Swap", -1}
        };
        private TIS.Imaging.ICImagingControl theImagingControl;

        #endregion
        #region Properties
        public LJVScanSpec BatchScanSpec
        {
            get { return batchScanSpec; }
            set
            {
                batchScanSpec = value;
                OnPropertyChanged();
                OnBatchScanSpecChanged();
                Debug.WriteLine("BatchScanSpec changed");
            }
        }
        public SingleDeviceScanVM ActiveScanVM
        {
            get { return activeScanVM; }
            set
            {
                activeScanVM = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<SingleDeviceScanVM> ScanVMs
        {
            get { return scanVMs; }
            set
            {
                scanVMs = value;
                OnPropertyChanged();
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
        #region Methods
        public static Task<DeviceBatchScanVM> CreateAsync(DeviceBatch batch)
        {
            var ret = new DeviceBatchScanVM();
            return ret.InitializeAsync(batch);
        }
        private async Task<DeviceBatchScanVM> InitializeAsync(DeviceBatch batch)
        {
            return await Task.Run(async () =>
            {
                InstrumentService.LJVScanCoordinator.PurgeSubscribers();
                InstrumentService.LJVScanCoordinator.SelectedCoordsDict = InstrumentService.LJVScanCoordinator.TheCoordsDictsDict["XinYan"];
                theDeviceBatch = batch;
                //calculate presentTestCondition
                HashSet<string> testConditionsFromChildren = new HashSet<string>();
                foreach (Device d in theDeviceBatch.Devices)
                {
                    foreach (DeviceLJVScanSummary summary in d.DeviceLJVScanSummaries)
                    {
                        testConditionsFromChildren.Add(summary.TestCondition);
                    }
                }
                int numberOfScans = testConditionsFromChildren.Count;
                int daysSinceFabrication = (DateTime.Now - theDeviceBatch.FabDate).Days;
                string presentTestCondition = string.Concat("t", numberOfScans + 1, ".", daysSinceFabrication);
                BatchScanSpec.TestCondition = presentTestCondition;
                Debug.WriteLine("presentTestCondition: " + presentTestCondition);
                //create sdsvms for each device
                foreach (Device d in theDeviceBatch.Devices)
                {
                    var newSDSVM = await SingleDeviceScanVM.CreateAsync(d, ctx);
                    newSDSVM.SaveDirectory = string.Concat(theDeviceBatch.FilePath, @"\", presentTestCondition);
                    ScanVMs.Add(newSDSVM);
                }
                UpdateBatchScanSpec();
                //CopyPreviousScanSpecs();
                ScanVMs.OrderBy(x => x.TheDeviceVM.TheDevice.BatchIndex);
                ScanSelectedDevicesCommand = AsyncCommand.Create(token => ScanSelectedDevices(token));
                string prCamModel = ConfigurationManager.AppSettings.Get("BatchTestSystem");
                if (prCamModel == "BTS1")
                {
                    delaysDict["Initialize"] = 50000;
                    delaysDict["Swap"] = 42000;
                }
                else if (prCamModel == "BTS2")
                {
                    delaysDict["Initialize"] = 17000;
                    delaysDict["Swap"] = 23000;
                    TheImagingControl = InstrumentService.LJVScanCoordinator.TheImagingControl;
                }
                else
                    Debug.WriteLine("BatchTestSystem needs to be properly set in App.Config");
                return this;
            }).ConfigureAwait(false);
        }
        /// <summary>
        /// workaround to avoid learning how to properly update the UI
        /// </summary>
        private void UpdateBatchScanSpec()
        {
            LJVScanSpec newSpec = new LJVScanSpec();
            Reflection.CopyScanSpecProperties(BatchScanSpec, newSpec);
            BatchScanSpec = newSpec;
        }
        /// <summary>
        /// copy batchScanSpec props to each sdsvm
        /// </summary>
        private void OnBatchScanSpecChanged()
        {
            foreach (SingleDeviceScanVM sdsvm in ScanVMs)
            {
                //LJVScanSpec spec;
                LJVScanSpec newSpec = new LJVScanSpec();
                Reflection.CopyScanSpecProperties(BatchScanSpec, newSpec);
                sdsvm.TheScanSpec = newSpec;
                sdsvm.SaveDirectory = string.Concat(theDeviceBatch.FilePath, @"\", BatchScanSpec.TestCondition);
            }
        }
        private void CopyPreviousScanSpecs()
        {
            foreach (SingleDeviceScanVM sdsvm in ScanVMs)
            {
                if (sdsvm.TheDeviceVM.TheDevice.DeviceLJVScanSummaries.Count > 0)
                {
                    //if device has been scanned use previous scan spec
                    LJVScanSpec newSpec = new LJVScanSpec();
                    var previousSpec = sdsvm.TheDeviceVM.TheDevice.DeviceLJVScanSummaries.First().LJVScans.First().LJVScanSpec;
                    if (previousSpec != null)
                    {
                        Reflection.CopyScanSpecProperties(previousSpec, newSpec);
                        sdsvm.TheScanSpec = newSpec;
                        sdsvm.SaveDirectory = string.Concat(theDeviceBatch.FilePath, @"\", BatchScanSpec.TestCondition);
                    }
                }
            }
        }
        #endregion
        public async Task ScanSelectedDevices(CancellationToken token = new CancellationToken())
        {
            await Task.Run(async () =>
            {
                InstrumentService.LJVScanCoordinator.TheRasPiController.PiInitializeExecute();
                //hardcoding delays because too lazy to develop RasPi TCP update messaging
                await Task.Delay(delaysDict["Initialize"]).ConfigureAwait(false);
                //await Task.Delay(new TimeSpan(13, 0, 0));
                int numberOfDevicesToScan = 0;
                foreach (SingleDeviceScanVM sdsvm in ScanVMs)
                {
                    if (sdsvm.ShouldBeScanned)
                    {
                        numberOfDevicesToScan++;
                    }
                }
                int scannedDeviceCounter = 1;
                foreach (SingleDeviceScanVM sdsvm in ScanVMs)
                {
                    if (sdsvm.ShouldBeScanned && !token.IsCancellationRequested)
                    {
                        ActiveScanVM = sdsvm;
                        sdsvm.ActivateThisVM();
                        await sdsvm.ScanAllPixels(token).ConfigureAwait(false);
                        if (scannedDeviceCounter < numberOfDevicesToScan && !token.IsCancellationRequested)
                        {
                            InstrumentService.LJVScanCoordinator.TheRasPiController.PiSwapExecute();
                            await Task.Delay(delaysDict["Swap"]).ConfigureAwait(false);
                        }
                        else if (scannedDeviceCounter == numberOfDevicesToScan || token.IsCancellationRequested)
                            InstrumentService.LJVScanCoordinator.TheRasPiController.PiIdleExecute();
                        else
                            Debug.WriteLine("Something screwy happened");
                        sdsvm.DeactivateThisVM();
                        scannedDeviceCounter++;
                    }
                }
                if (!token.IsCancellationRequested)
                {
                    try
                    {
                        var dbvm = new DeviceBatchVM(theDeviceBatch);
                        await OriginService.OriginController.GenerateSingleTCComparePixOPJ(dbvm, BatchScanSpec.TestCondition);
                        await OriginService.OriginController.GenerateProcDATOPJForSingleTestCondition(dbvm, BatchScanSpec.TestCondition);
                        await OriginService.OriginController.GenerateSingleTCCompareStatsOPJ(dbvm, BatchScanSpec.TestCondition);
                        Application.Current.Dispatcher.Invoke((Action)delegate
                        {
                            Debug.WriteLine("updating oxyplots for devbatch: " + dbvm.TheDeviceBatch.Name);
                            dbvm.UpdatePlotBitmapsExecute(dbvm);
                            //dbvm.OrganizeDataByDevicePixel();
                        });
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("DeviceBatchScanVM ScanSelectedDevices exception: " + e.ToString());
                    }
                }
            }).ConfigureAwait(false);
        }
        #region Commands
        public IAsyncCommand ScanSelectedDevicesCommand { get; set; }
        private RelayCommand _updateScanSpecs;
        public ICommand UpdateScanSpecs
        {
            get
            {
                if (_updateScanSpecs == null)
                {
                    _updateScanSpecs = new RelayCommand(param => this.UpdateScanSpecsExecute());
                }
                return _updateScanSpecs;
            }
        }
        void UpdateScanSpecsExecute()
        {
            UpdateBatchScanSpec();
        }
        #endregion
    }
}
