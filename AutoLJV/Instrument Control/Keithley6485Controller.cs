using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AutoLJV.Instrument_Control
{
    public class Keithley6485Controller : GPIBDevice
    {
        public Keithley6485Controller() : base(14)//as of 10/24/18, the single Keithley 6485 GPIB address is 14 in the NPI environment 
        {

        }
        #region Members
        double[] picoRanges = new double[] { 2E-9, 2E-8, 2E-7, 2E-6, 2E-5, 2E-4, 2E-3, 2E-2 };
        int picoRangeCounter = 0;
        #endregion
        /// <summary>
        /// Initialize the Keithley 6485 by sending a series of GPIB commands
        /// </summary>
        private async Task<Keithley6485Controller> InitializeAsync()
        {
            return await Task.Run(async () =>
            {
                Debug.WriteLine("Initializing KE6485");
                
                await this.OpenGPIBDevice();
                await SendGPIBString("*RST;"); //reset the device
                await SendGPIBString("*CLS;"); //clear all buffers
                await SendGPIBString(":SYST:ZCH OFF;"); //turn off zero check
                await SendGPIBString(":CURR:RANG:AUTO ON;"); //turn on autoranging feature
                await SendGPIBString(":SENS:CURR:NPLC 10;"); //integrate measurements over 10 power line cycles
                return this;
            }).ConfigureAwait(false);
        }
        public static Task<Keithley6485Controller> CreateAsync()
        {
            var ret = new Keithley6485Controller();
            return ret.InitializeAsync();
        }
        /// <summary>
        /// Take and fetch a photocurrent measurement
        /// </summary>
        /// <returns></returns>
        async Task<string> FetchMeasurementString()
        {
            return await Task.Run(async () =>
            {
                string measurement = "";
                await SendGPIBString(":INIT;"); //take a measurement
                await SendGPIBString(":FETC?;"); //fetch the value to the GPIB buffer
                measurement = await ReadGPIBString();
                return measurement;
            }
            ).ConfigureAwait(false);
        }
        public async Task<decimal> FetchPhotocurrentMeasurement(int timeoutSeconds = 7)
        {
            return await Task.Run(async () =>
            {
                decimal measurement = 0;
                var task = FetchMeasurementString();
                if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds))) == task)
                {
                    var response = await task;
                    string[] responseArray = response.Split(',');
                    measurement = Convert.ToDecimal(Convert.ToDouble(responseArray[0].Replace("A", string.Empty))); //take the first value in the array and delete the A then convert to double 
                }
                else
                {
                    measurement = -1;
                    System.Diagnostics.Debug.WriteLine("timed out");
                }
                return measurement;
            });
        }       
    }
}
