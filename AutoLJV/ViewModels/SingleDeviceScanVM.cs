using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutoLJV.Instrument_Control;
using EFDeviceBatchCodeFirst;
using System.Windows.Forms;
using AsyncCommands;
using DeviceBatchGenerics.Support.Bases;
using DeviceBatchGenerics.Support;
using DeviceBatchGenerics.ViewModels.PlottingVMs;
using DeviceBatchGenerics.ViewModels.EntityVMs;
using System.Windows.Input;
using System.Collections.ObjectModel;
using DeviceBatchGenerics.Support.DataMapping;
using CsvHelper;
using System.Configuration;

namespace AutoLJV.ViewModels
{
    public class SingleDeviceScanVM : CrudVMBase
    {
        public SingleDeviceScanVM()
        {
            //ManualSweepInitialize();
        }
        #region Members
        ManualResetEvent[] sweepFinishedEvent = new ManualResetEvent[1] { new ManualResetEvent(false) };
        ManualResetEvent[] processedDataEvent = new ManualResetEvent[1] { new ManualResetEvent(false) };
        private List<string> procDatPaths = new List<string>();
        private List<string> elspecPaths = new List<string>();
        private List<string> imagePaths = new List<string>();
        LJVScanSummaryVM theLJVScanSummaryVM;
        DeviceVM theDeviceVM;
        LJVScanSpec theScanSpec = new LJVScanSpec
        {
            DeviceDwellTime = 1000,
            ShouldRecordSpectrumAtEachStep = false,
            StartVoltage = 0.0m,
            StopVoltage = 4.0m,
            StepSize = 0.1m,
            StopCurrent = 11,
            StopLuminance = 10000,
            ActiveArea = 4E-6m,
            TestCondition = "t1.1"
        };
        ObservableCollection<ProcessedLJVDatum> previousScanData = new ObservableCollection<ProcessedLJVDatum>();
        //string presentTestCondition = "t1.1";
        string _saveDirectory = "Path not set";
        bool isActiveScanVM = false;
        bool allPixelsWereScanned = false;
        bool shouldBeScanned = true;
        private TIS.Imaging.ICImagingControl theImagingControl;

        #endregion
        #region Properties        
        public ObservableCollection<ProcessedLJVDatum> PreviousScanData
        {
            get { return previousScanData; }
            set
            {
                previousScanData = value;
                OnPropertyChanged();
                Debug.WriteLine("PreviousScanData changed");
            }
        }
        public LJVScanSummaryVM TheLJVScanSummaryVM
        {
            get { return theLJVScanSummaryVM; }
            set
            {
                theLJVScanSummaryVM = value;
                OnPropertyChanged();
            }
        }
        public DeviceVM TheDeviceVM
        {
            get { return theDeviceVM; }
            set
            {
                theDeviceVM = value;
                OnPropertyChanged();
            }
        }
        public event EventHandler AllPixelsScanned;
        public Dictionary<string, Pixel> PixelsDict
        {
            get { return theDeviceVM.PixelsDict; }
        }
        public Pixel SelectedPixel
        {
            get { return theDeviceVM.SelectedPixel; }
            set
            {
                theDeviceVM.SelectedPixel = value;
                OnPropertyChanged();
                InstrumentService.LJVScanCoordinator.TheRasPiController.SelectedPixel = SelectedPixel.Site;
            }
        }
        public LJVScanSpec TheScanSpec
        {
            get { return theScanSpec; }
            set
            {
                theScanSpec = value;
                OnPropertyChanged();
                Debug.WriteLine("TheScanSpec Changed");
            }
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
        public bool IsActiveScanVM
        {
            get { return isActiveScanVM; }
            set
            {
                isActiveScanVM = value;
                OnPropertyChanged();
            }
        }
        public bool AllPixelsWereScanned
        {
            get { return allPixelsWereScanned; }
            set
            {
                allPixelsWereScanned = value;
                OnPropertyChanged();
            }
        }
        public bool ShouldBeScanned
        {
            get { return shouldBeScanned; }
            set
            {
                shouldBeScanned = value;
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
        public static Task<SingleDeviceScanVM> CreateAsync(Device dev, DeviceBatchContext context)
        {
            var ret = new SingleDeviceScanVM();
            return ret.InitializeAsync(dev, context);
        }
        public static Task<SingleDeviceScanVM> ManualSweepCreateAsync()
        {
            var ret = new SingleDeviceScanVM();
            return ret.ManualSweepInitializeAsync();
        }
        private async Task<SingleDeviceScanVM> InitializeAsync(Device dev, DeviceBatchContext context)
        {
            return await Task.Run(() =>
            {
                TheDeviceVM = new DeviceVM(dev, ctx);
                RunVoltageSweepCommand = AsyncCommand.Create(token => ScanPixelAndProcessData(token));
                TheLJVScanSummaryVM = new LJVScanSummaryVM(ctx);
                TheLJVScanSummaryVM.TheLJVScanSummary.Device = ctx.Devices.Where(x => x.DeviceId == TheDeviceVM.TheDevice.DeviceId).First();
                TheLJVScanSummaryVM.TheLJVScanSummary.TestCondition = TheScanSpec.TestCondition;
                string bts = ConfigurationManager.AppSettings.Get("BatchTestSystem");
                if (bts == "BTS2")
                {
                    TheImagingControl = InstrumentService.LJVScanCoordinator.TheImagingControl;
                }
                return this;
            });
        }
        private async Task<SingleDeviceScanVM> ManualSweepInitializeAsync()
        {
            return await Task.Run(() =>
            {
                InstrumentService.LJVScanCoordinator.PurgeSubscribers();
                TheDeviceVM = new DeviceVM();
                TheLJVScanSummaryVM = new LJVScanSummaryVM();
                TheLJVScanSummaryVM.TheLJVScanSummary.Device = TheDeviceVM.TheDevice;
                RunVoltageSweepCommand = AsyncCommand.Create(token => ScanPixelAndProcessData(token));
                ScanAllPixelsCommand = AsyncCommand.Create(token => ScanAllPixels(token));
                //InstrumentService.LJVScanCoordinator.VoltageSweepFinished += InstrumentService_VoltageSweepFinished;
                SelectedPixel = theDeviceVM.PixelsDict["SiteA"];
                string bts = ConfigurationManager.AppSettings.Get("BatchTestSystem");
                if (bts == "BTS2")
                {
                    TheImagingControl = InstrumentService.LJVScanCoordinator.TheImagingControl;
                }
                return this;
            });
        }
        public async Task ScanAllPixels(CancellationToken token = new CancellationToken())
        {
            await Task.Run(async () =>
            {
                AllPixelsWereScanned = false;
                SelectedPixel = theDeviceVM.PixelsDict["SiteA"];
                while (!AllPixelsWereScanned && !token.IsCancellationRequested)
                {
                    await ScanPixelAndProcessData(token);
                    await GoToNextPixel();
                }
            });
        }
        private async Task ScanPixelAndProcessData(CancellationToken token = new CancellationToken())
        {
            await Task.Run(async () =>
            {
                Debug.WriteLine("ScanPixelAndProcessData");
                await InstrumentService.LJVScanCoordinator.RunVoltageSweep(token, TheScanSpec);
                await ProcessSweepData();
                /*not sure why we were looping through all pixels in this task
                AllPixelsWereScanned = false;
                SelectedPixel = theDeviceVM.PixelsDict["SiteA"];
                while (!AllPixelsWereScanned && !token.IsCancellationRequested)
                {
                    await InstrumentService.LJVScanCoordinator.RunVoltageSweep(token, TheScanSpec).ConfigureAwait(false);
                    await ProcessSweepData().ConfigureAwait(false);
                    await GoToNextPixel().ConfigureAwait(false);
                }
                */
            }).ConfigureAwait(false);
        }
        private async Task ProcessSweepData()
        {
            await Task.Run(async () =>
            {
                Debug.WriteLine("ProcessSweepData");

                var rawdatList = InstrumentService.LJVScanCoordinator.RawLJVData.ToList();
                var elspecList = InstrumentService.LJVScanCoordinator.ELSpecData.ToList();
                var processedData = await DataProcessingService.LEDCalculator.ProcessRawData(rawdatList,
                    InstrumentService.LJVScanCoordinator.ELSpecCurrent,
                    InstrumentService.LJVScanCoordinator.ELSpecPhotoCurrent,
                    elspecList).ConfigureAwait(false);
                PreviousScanData = new ObservableCollection<ProcessedLJVDatum>(processedData);
                var setCurr = await CalculateCurrentForPicture().ConfigureAwait(false);
                await InstrumentService.LJVScanCoordinator.SetCurrentAndTakePic(setCurr).ConfigureAwait(false);
                Debug.WriteLine("got past SetCurrentAndTakePic");
                await SaveDataToCSVs().ConfigureAwait(false);
                Debug.WriteLine("got past SaveDataToCSVs");
                if (TheDeviceVM.TheDevice.BatchIndex > 0)
                    await UpdateEntities().ConfigureAwait(false);
            }).ConfigureAwait(false);
            //note to self: need to unsubscribe from VoltageSweepFinished event after last pixel is scanned  
            //to prevent instances of this class from building up in memory (look into WeakReference?)
        }
        /// <summary>
        /// Find the current at which the device emits the desired luminance for a picture
        /// </summary>
        /// <returns></returns>
        private async Task<decimal> CalculateCurrentForPicture()
        {
            Debug.WriteLine("CalculateCurrentForPicture");
            return await Task.Run(() =>
            {
                Debug.WriteLine("CalculateCurrentForPicture");
                decimal pictureLuminance = 77m;//heuristic value for ideal PiCam images
                decimal setCurrent = InstrumentService.LJVScanCoordinator.PresentCurrent;//use final current if device is dim
                                                                                         //find the maximum luminance value and add data to lists for interpolation
                decimal maxLuminance = 0;
                List<decimal> currentDensityList = new List<decimal>();
                List<decimal> luminanceList = new List<decimal>();
                foreach (ProcessedLJVDatum p in PreviousScanData)
                {
                    if (p.Luminance > maxLuminance)
                    {
                        maxLuminance = p.Luminance;
                    }
                    currentDensityList.Add(p.CurrentDensity);
                    luminanceList.Add(p.Luminance);
                }
                //if the device is bright enough, take a picture
                if (maxLuminance > pictureLuminance)
                {
                    //interpolate the required set current
                    int indexer = 0;
                    while (luminanceList[indexer] < pictureLuminance)
                    {
                        indexer += 1;
                    }
                    var setCurrentDensity = ((pictureLuminance - luminanceList[indexer - 1]) * ((currentDensityList[indexer] - currentDensityList[indexer - 1]) / (luminanceList[indexer] - luminanceList[indexer - 1])) + currentDensityList[indexer - 1]);
                    setCurrent = setCurrentDensity * TheScanSpec.ActiveArea * 10;//(100 cm/1 m)^2 = 10,000, 1000 mA/A => CF=10
                }
                return setCurrent;
            });

        }
        public async Task GoToNextPixel()
        {
            await Task.Run(() =>
            {
                if (SelectedPixel.Site == "SiteA")
                    SelectedPixel = PixelsDict["SiteB"];
                else if (SelectedPixel.Site == "SiteB")
                    SelectedPixel = PixelsDict["SiteC"];
                else if (SelectedPixel.Site == "SiteC")
                    SelectedPixel = PixelsDict["SiteD"];
                else if (SelectedPixel.Site == "SiteD")
                {
                    AllPixelsScanned?.Invoke(this, EventArgs.Empty);//tell listeners that all pixels have been measured
                    AllPixelsWereScanned = true;
                }
                else
                    System.Windows.Forms.MessageBox.Show("Detected unknown naming scheme. Needs to be fixed");
            });
        }
        Task SaveDataToCSVs()
        {
            Debug.WriteLine("SaveDataToCSVs");
            return Task.Run(async () =>
            {
                Debug.WriteLine("SaveDataToCSVs");
                if (SaveDirectory != "Path not set")
                {
                    await CreateSubFoldersThenWriteCSVs(SaveDirectory);
                }
                else
                {
                    SaveDirectory = string.Concat(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"\LJVDefault");
                    await CreateSubFoldersThenWriteCSVs(SaveDirectory);
                    //System.Windows.Forms.MessageBox.Show(@"Saved to \User\Documents\LJVDefault since no path was chosen.");
                }
            });
        }
        public async Task CreateSubFoldersThenWriteCSVs(string paramDirectory)
        {
            await Task.Run(() =>
            {
                Debug.WriteLine("CreateSubFoldersThenWriteCSVs");
                string rawDataPath = string.Concat(paramDirectory, @"\Raw Data\");
                Directory.CreateDirectory(rawDataPath);
                string elSpecPath = string.Concat(paramDirectory, @"\EL Spectra\");
                Directory.CreateDirectory(elSpecPath);
                string procDataPath = string.Concat(paramDirectory, @"\Processed Data\");
                Directory.CreateDirectory(procDataPath);
                string imagePath = string.Concat(paramDirectory, @"\Images\");
                Directory.CreateDirectory(imagePath);
                string summaryPath = string.Concat(paramDirectory, @"\Scan Summaries\");
                Directory.CreateDirectory(summaryPath);
                try
                {
                    string label = TheDeviceVM.TheDevice.Label;
                    TheLJVScanSummaryVM.TheLJVScanSummary.SpreadsheetReportPath = string.Concat(summaryPath, label, ".xlsx");
                    string devCompletionDay = label.Substring(0, label.IndexOf("-"));
                    Debug.WriteLine("devCompletionDay: " + devCompletionDay);
                    string qdBatchString = label.Substring(label.IndexOf("-") + 1, label.Length - label.IndexOf("-") - 1);
                    qdBatchString = qdBatchString.Substring(0, qdBatchString.IndexOf("-"));
                    Debug.WriteLine("qdBatchString: " + qdBatchString);
                    string batchIndexString = label.Substring(label.LastIndexOf("-") + 1);
                    Debug.WriteLine("batchIndexString: " + batchIndexString);
                    string pixelDataString = string.Concat(devCompletionDay, "-", qdBatchString, "_", TheScanSpec.TestCondition, "-", batchIndexString, "_", SelectedPixel.Site);//to conform to OriginLab data processing scripts
                    Debug.WriteLine("pixelDataString: " + pixelDataString);
                    rawDataPath = string.Concat(rawDataPath, pixelDataString, ".rawDAT");
                    DataProcessingService.WriteIENumberableToCSV(InstrumentService.LJVScanCoordinator.RawLJVData, rawDataPath);
                    procDataPath = string.Concat(procDataPath, pixelDataString, ".procDAT");
                    procDatPaths.Add(procDataPath);
                    DataProcessingService.WriteIENumberableToCSV(PreviousScanData, procDataPath);
                    elSpecPath = string.Concat(elSpecPath, pixelDataString, "@", InstrumentService.LJVScanCoordinator.PresentVoltage, "V.ELSpectrum");
                    elspecPaths.Add(elSpecPath);
                    Debug.WriteLine("ELSpecData.Count: " + InstrumentService.LJVScanCoordinator.ELSpecData.Count);
                    DataProcessingService.WriteIENumberableToCSV(InstrumentService.LJVScanCoordinator.ELSpecData, elSpecPath);
                    //do this for images                
                    imagePath = string.Concat(imagePath, pixelDataString, ".jpg");
                    imagePaths.Add(imagePath);
                    InstrumentService.LJVScanCoordinator.TheImageReceiver.SaveImage(imagePath);

                }
                catch (Exception e)
                {
                    MessageBox.Show("Device Label in unexpected format?: " + e.ToString());
                }
            }).ConfigureAwait(false);
        }

        public async Task UpdateEntities()
        {
            await Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("UpdateEntities");
                    //add the DeviceLJVScanSummary to dbcontext
                    if (ctx.DeviceLJVScanSummaries.Find(TheLJVScanSummaryVM.TheLJVScanSummary.DeviceLJVScanSummaryId) == null)
                        ctx.DeviceLJVScanSummaries.Add(TheLJVScanSummaryVM.TheLJVScanSummary);
                    TheLJVScanSummaryVM.TheLJVScanSummary.TestCondition = TheScanSpec.TestCondition;
                    await UpdateLJVScanEntities();
                    if (PreviousScanData.Select(x => x.Luminance).Max() > 10)
                        await UpdateELSpecEntities();
                    await UpdateImageEntities();
                    TheLJVScanSummaryVM.PopulateEntityPropertiesFromChildren();
                    ctx.SaveChanges();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }
            }).ConfigureAwait(false);
        }
        private async Task UpdateLJVScanEntities()
        {
            await Task.Run(() =>
            {
                //check to see if LJVScans exist for each filepath and create them if not
                List<string> procDatPathsFromEntities = new List<string>();
                foreach (LJVScan scan in TheLJVScanSummaryVM.TheLJVScanSummary.LJVScans)
                {
                    procDatPathsFromEntities.Add(scan.ProcDATFilePath);//fucking linq, how does it work?
                }
                List<string> newProcDatPaths = procDatPaths.Except(procDatPathsFromEntities).ToList();
                foreach (string path in newProcDatPaths)
                {
                    Debug.WriteLine("Adding new LJVScan for newProcDatPath: " + path);
                    LJVScanVM newLJVScanVM = new LJVScanVM(path);
                    newLJVScanVM.PopulatePropertiesFromPath(path);
                    newLJVScanVM.TheLJVScan.DeviceLJVScanSummary = TheLJVScanSummaryVM.TheLJVScanSummary;
                    newLJVScanVM.TheLJVScan.Pixel = ctx.Pixels.Where(x => x.PixelId == SelectedPixel.PixelId).First();
                    //newLJVScanVM.TheLJVScan.LJVScanSpec = TheScanSpec;
                    ctx.LJVScans.Add(newLJVScanVM.TheLJVScan);
                }
            }).ConfigureAwait(false);
        }
        private async Task UpdateELSpecEntities()
        {
            await Task.Run(() =>
            {
                List<string> elspecPathsFromEntities = new List<string>();
                foreach (ELSpectrum spec in TheLJVScanSummaryVM.TheLJVScanSummary.ELSpectrums)
                {
                    elspecPathsFromEntities.Add(spec.FilePath);
                }
                List<string> newSpecPaths = elspecPaths.Except(elspecPathsFromEntities).ToList();
                foreach (string path in newSpecPaths)
                {
                    ELSpecVM newSpecVM = new ELSpecVM(path);
                    newSpecVM.TheELSpectrum.DeviceLJVScanSummary = TheLJVScanSummaryVM.TheLJVScanSummary;
                    newSpecVM.TheELSpectrum.Pixel = ctx.Pixels.Where(x => x.PixelId == SelectedPixel.PixelId).First();
                    ctx.ELSpectra.Add(newSpecVM.TheELSpectrum);
                }
            }).ConfigureAwait(false);
        }
        private async Task UpdateImageEntities()
        {
            await Task.Run(() =>
            {
                List<string> imagePathsFromEntities = new List<string>();
                foreach (Image image in TheLJVScanSummaryVM.TheLJVScanSummary.Images)
                {
                    imagePathsFromEntities.Add(image.FilePath);
                }
                List<string> newImagePaths = imagePaths.Except(imagePathsFromEntities).ToList();
                foreach (string path in newImagePaths)
                {
                    Image newImage = new Image();
                    newImage.FilePath = path;
                    newImage.Luminance = 77m;//image entity should be updated to remove this from required
                    newImage.DeviceLJVScanSummary = TheLJVScanSummaryVM.TheLJVScanSummary;
                    newImage.Pixel = ctx.Pixels.Where(x => x.PixelId == SelectedPixel.PixelId).First();
                    ctx.Images.Add(newImage);
                }

            }).ConfigureAwait(false);
        }
        public void ActivateThisVM()
        {
            //InstrumentService.LJVScanCoordinator.VoltageSweepFinished += InstrumentService_VoltageSweepFinished;
            IsActiveScanVM = true;
        }
        public void DeactivateThisVM()
        {
            //InstrumentService.LJVScanCoordinator.VoltageSweepFinished -= InstrumentService_VoltageSweepFinished;
            IsActiveScanVM = false;
        }
        public Task TakePicture()
        {
            return Task.Run(() =>
            {

            });
        }
        private Task<decimal> CalculateVoltageForPicture()
        {
            return Task.Run(() =>
            {
                decimal pictureLuminance = 42.0m;
                decimal setVoltage = InstrumentService.LJVScanCoordinator.PresentVoltage;
                //find the maximum luminance value and add data to lists for interpolation
                decimal maxLuminance = 0;
                List<decimal> voltageList = new List<decimal>();
                List<decimal> luminanceList = new List<decimal>();
                foreach (ProcessedLJVDatum p in PreviousScanData)
                {
                    if (p.Luminance > maxLuminance)
                    {
                        maxLuminance = p.Luminance;
                    }
                    voltageList.Add(p.Voltage);
                    luminanceList.Add(p.Luminance);
                }
                //if the device is bright enough, take a picture
                if (maxLuminance > pictureLuminance)
                {
                    //interpolate the required set current
                    int indexer = 0;
                    while (luminanceList[indexer] < pictureLuminance)
                    {
                        indexer += 1;
                    }
                    setVoltage = ((pictureLuminance - luminanceList[indexer - 1]) * ((voltageList[indexer] - voltageList[indexer - 1]) / (luminanceList[indexer] - luminanceList[indexer - 1])) + voltageList[indexer - 1]);
                }
                Debug.WriteLine("setVoltage is: " + setVoltage);
                return setVoltage;
            });
        }
        #region Commands
        public IAsyncCommand ScanAllPixelsCommand { get; set; }
        public IAsyncCommand RunVoltageSweepCommand { get; set; }

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
