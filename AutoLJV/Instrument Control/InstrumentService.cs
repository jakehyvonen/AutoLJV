using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using DeviceBatchGenerics.Instruments;
using DeviceBatchGenerics.ViewModels.EntityVMs;
using EFDeviceBatchCodeFirst;
using CsvHelper;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoLJV.Instrument_Control
{
    public static class InstrumentService
    {
        /// <summary>
        /// this class controls various instruments to acquire and transmit raw data to other classes for further processing
        /// </summary>
        static public LJVScanCoordinator LJVScanCoordinator { get; set; }
        static public Task CreateCoordinatorAsync()
        {
            return Task.Run(async () =>
            {
                LJVScanCoordinator = await LJVScanCoordinator.CreateAsync();
            });
        }
        /*this was all migrated to LJVScanCoordinator
        #region Members
        static decimal presentLuminance = 0;
        static decimal presentVoltage = 0;
        static decimal presentCurrent = 0;
        static decimal presentPhotoCurrent = 0;
        static double assumedAlpha = 8E8;
        static Keithley2400Controller KE2400;
        static Keithley6485Controller KE6485;
        static PR670Controller PR670 = new PR670Controller();
        static ObservableCollection<RawLJVDatum> _rawLJVData = new ObservableCollection<RawLJVDatum>();
        static ObservableCollection<ELSpecDatum> _specData = new ObservableCollection<ELSpecDatum>();
        static Dictionary<string, double> _redAlphaDict = new Dictionary<string, double>
            {
                { "SiteA", 9.8E8 },
                { "SiteB", 8.3E8 },
                { "SiteC", 8.33E8 },
                { "SiteD", 8.33E8 }
            };
        static Dictionary<string, double> _greenAlphaDict = new Dictionary<string, double>
            {
                { "SiteA", 5.3E9 },
                { "SiteB", 4.3E9 },
                { "SiteC", 4.8E9 },
                { "SiteD", 4.7E9 }
            };
        static Dictionary<string, double> _blueAlphaDict = new Dictionary<string, double>
            {
                { "SiteA", 6.6E8 },
                { "SiteB", 5.2E8 },
                { "SiteC", 5.3E8 },
                { "SiteD", 5.2E8 }
            };
        static LJVScanSpec _activeSpec = new LJVScanSpec
        {
            DeviceDwellTime = 1000,
            ShouldRecordSpectrumAtEachStep = false,
            StartVoltage = 0,
            StopVoltage = 5,
            StepSize = 0.1m,
            StopCurrent = 10,
            StopLuminance = 10000
        };
        #endregion
        #region Properties
        static public event EventHandler VoltageSweepFinished;
        static public ObservableCollection<RawLJVDatum> RawLJVData
        {
            get { return _rawLJVData; }
            set
            {
                _rawLJVData = value;
                Debug.WriteLine("RawLJVData changed");
            }
        }
        static public ObservableCollection<ELSpecDatum> ELSpecData
        {
            get { return _specData; }
            set { _specData = value; }
        }
        static public RasPiController TheRasPiController { get; set; } = new RasPiController();
        static public LJVScanSpec ActiveLJVScanSpec
        {
            get { return _activeSpec; }
            set
            {
                _activeSpec = value;
            }
        }
        #endregion
        #region Methods
        /// <summary>
        /// Perform a voltage sweep from LJVScanSpec parameters
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        static public async Task RunVoltageSweep(CancellationToken token = new CancellationToken(), LJVScanSpec spec = null)
        {
            if (spec != null)
                ActiveLJVScanSpec = spec;
            Debug.WriteLine("RunVoltageSweep Task");
            RawLJVData = new ObservableCollection<RawLJVDatum>();
            KE2400 = new Keithley2400Controller();
            KE6485 = new Keithley6485Controller();
            KE2400.Initialize();
            KE6485.Initialize();
            KE2400.TurnOnSource(ActiveLJVScanSpec.StartVoltage);
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            presentVoltage = ActiveLJVScanSpec.StartVoltage;
            presentLuminance = 0;
            presentCurrent = 0;
            while (!token.IsCancellationRequested && StopConditionsAreNotMet())
            {
                KE2400.SetNextVoltageStep(presentVoltage);
                presentCurrent = await KE2400.FetchCurrentMeasurement();
                presentPhotoCurrent = await KE6485.FetchPhotocurrentMeasurement();
                var rawDatum = new RawLJVDatum
                {
                    Voltage = presentVoltage,
                    Current = presentCurrent,
                    PhotoCurrent = presentPhotoCurrent,
                    Resistance = Math.Round(presentVoltage / presentCurrent)//R=V/I
                };
                if (Convert.ToDouble(presentPhotoCurrent) * assumedAlpha > 42)
                {
                    var cameraDatum = await PR670.LuminanceMeasurement();
                    rawDatum.CameraLuminance = cameraDatum.Luminance;
                    rawDatum.CameraCIEx = cameraDatum.CIEx;
                    rawDatum.CameraCIEy = cameraDatum.CIEy;
                    presentLuminance = cameraDatum.Luminance;
                    Debug.WriteLine("Luminance: " + rawDatum.CameraLuminance);
                    Debug.WriteLine("CIEx: " + rawDatum.CameraCIEx);
                    Debug.WriteLine("CIEy: " + rawDatum.CameraCIEy);
                }
                RawLJVData.Add(rawDatum);
                RawLJVData = new ObservableCollection<RawLJVDatum>(RawLJVData);
                presentVoltage += ActiveLJVScanSpec.StepSize;
            }
            KE2400.CloseGPIBDevice();
            KE6485.CloseGPIBDevice();
            VoltageSweepFinished.Invoke(null, EventArgs.Empty);
        }
        /// <summary>
        /// evaluate whether the voltage sweep should stop based on various user-set conditions
        /// </summary>
        /// <returns></returns>
        static private bool StopConditionsAreNotMet()
        {
            bool conditionsNotMet = true;
            if (presentVoltage > ActiveLJVScanSpec.StopVoltage | presentCurrent >= ActiveLJVScanSpec.StopCurrent | presentLuminance >= ActiveLJVScanSpec.StopLuminance)
                conditionsNotMet = false;
            return conditionsNotMet;
        }
        #endregion
        */
    }
}
