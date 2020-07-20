using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoLJV.Instrument_Control
{
    public class Keithley2400Controller : GPIBDevice
    {
        public Keithley2400Controller() : base(24) //as of 10/24/18, the single Keithley 2400 GPIB address is 24 in the NPI environment 
        {

        }
        #region Members
        double _complianceLevel = 0.1; //this prevents the KE2400 from ever supplying more than 0.1A 
        double _NPLC = 2.0; //number of power line cycles per measurement      
        #endregion
        #region Properties
        public double ComplianceLevel
        {
            get { return _complianceLevel; }
            set { ComplianceLevel = value; }
        }
        public double NPLC
        {
            get { return _NPLC; }
            set { _NPLC = value; }
        }
        #endregion
        /// <summary>
        /// Initialize the Keithley 2400 by sending a series of GPIB commands
        /// </summary>
        private async Task<Keithley2400Controller> InitializeAsync()
        {
            return await Task.Run(async () =>
            {

                Debug.WriteLine("Initializing Keithley2400Controller");
                await OpenGPIBDevice();
                await SendGPIBString("*RST"); //reset the instrument
                await SendGPIBString("*ESE 1;*SRE 32;*CLS;"); //clear the status byte
                await SendGPIBString(":SOUR:DEL 0.0;"); //set the delay for setting the source voltage to 0s
                await SendGPIBString(":SOUR:VOLT:MODE FIXED;"); //set source mode to fixed voltage (we perform the sweep by stepping this command via the software)
                await SendGPIBString(":SYSTEM:AZER:STAT ON;"); //turn on autozeroing
                await SendGPIBString(":SENS:AVER:COUN 10;"); //average 10 current measurements
                await SendGPIBString(":SENS:AVER:STAT ON;"); //turn on the averaging function in the firmware
                await SendGPIBString(string.Concat(":SENS:CURR:NPLC ", NPLC, ";")); //set the number of power line cycles over which to integrate the measurement        
                await SendGPIBString(string.Concat(":CURR:PROT ", ComplianceLevel, ";"));//set the maximum current supply limit

                return this;
            }).ConfigureAwait(false);
        }
        public static Task<Keithley2400Controller> CreateAsync()
        {
            var ret = new Keithley2400Controller();
            return ret.InitializeAsync();
        }
        /// <summary>
        /// Turn on the Keithley 2400 output with the specified voltage
        /// </summary>
        /// <param name="startVolt"></param>
        public async Task TurnOnSource(decimal startVolt)
        {
            await Task.Run(async () =>
            {
                await SendGPIBString(string.Concat("SOUR:VOLT:LEV ", startVolt, ";")); //set the source voltage level to startVolt
                await SendGPIBString(":OUTP ON;"); //turn on the output
            }
            ).ConfigureAwait(false);
        }
        public async Task SetNextVoltageStep(decimal volt)
        {
            await SendGPIBString(string.Concat("SOUR:VOLT:LEV ", volt, ";")).ConfigureAwait(false);
        }
        async Task<string> FetchMeasurementString()
        {
            return await Task.Run(async () =>
            {
                Debug.WriteLine("FetchMeasurementString");
                string measurementString = "";
                await SendGPIBString(":INIT;").ConfigureAwait(false); //initialize the measurement
                await SendGPIBString(":SENS:DATA:LAT?;").ConfigureAwait(false); //fetch it from the instrument
                measurementString = await ReadGPIBString().ConfigureAwait(false);
                return measurementString;
            }
            ).ConfigureAwait(false);
        }
        public async Task<decimal> FetchCurrentMeasurement(int timeoutSeconds = 77)
        {
            return await Task.Run(async () =>
            {
                Debug.WriteLine("FetchCurrentMeasurement");
                decimal measurement;
                var task = FetchMeasurementString();
                Debug.WriteLine("awaiting task");
                var response = await task.ConfigureAwait(false);
                Debug.WriteLine("done waiting");
                string[] responseArray = response.Split(',');
                measurement = Convert.ToDecimal(Convert.ToDouble(responseArray[1]));//this is the current measurement in amps       
                /*fuck timeouts
                if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) == task)
                {
                            
                }
                else
                {
                    measurement = -1;
                    Debug.WriteLine("timed out");
                }
                */
                return measurement;
            }).ConfigureAwait(false);
        }
        public async Task SetCurrent(decimal current = 0.0001m)
        {
            await Task.Run(async () =>
            {
                Debug.WriteLine("setCurrent: " + current);
                string level = current.ToString("0.000E0");
                await SendGPIBString(":SOUR:FUNC CURR;").ConfigureAwait(false);
                await SendGPIBString(":SOUR:CURR:MODE FIXED;").ConfigureAwait(false);
                await SendGPIBString(":SENS:FUNC 'VOLT:DC'").ConfigureAwait(false);
                //SendGPIBString(":SOUR:CURR:RANG MIN;");
                await SendGPIBString(string.Concat(":SOUR:CURR:LEV ", level, ";")).ConfigureAwait(false);
                //SendGPIBString(":SENS:VOLT:PROT 25;");
                //SendGPIBString(":SENS:VOLT:RANG 20;");
                //SendGPIBString(":FORM:ELEM VOLT;");
                await SendGPIBString(":OUTP ON;").ConfigureAwait(false);
                Debug.WriteLine("finished SetCurrent");
            }
           ).ConfigureAwait(false);

        }
    }
}
