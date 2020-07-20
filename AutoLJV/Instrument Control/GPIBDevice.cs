using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Threading.Tasks;

namespace AutoLJV.Instrument_Control
{
    public class GPIBDevice
    {
        public GPIBDevice(int devAddress)
        {
            _devAddress = devAddress;
            //OpenGPIBDevice().RunSynchronously();
        }
        #region Members
        int GPIBAddr = 0;
        int _devAddress = 0;
        int _deviceName; //not really sure why this is needed
        int ibsta, iberr, ibcnt, ibcntl;
        //bool _instrumentIsReady = true;

        #endregion
        #region Properties
            public int DevAddress { get { return _devAddress; } set { _devAddress = value; } }
        #endregion
        #region Methods

        public async Task OpenGPIBDevice()
        {
            await Task.Run(() =>
            {
                //Initialize device with 0s timeout 
                _deviceName = GPIB.ibdev(GPIBAddr, _devAddress, 0, 0, 1, 0);
                if ((ibsta & (int)GPIB.ibsta_bits.ERR) != 0)
                {
                    MessageBox.Show("Error in initializing GPIB device with address: " + _devAddress);
                    return;
                }
                //clear the specific GPIB instruments
                GPIB.ibclr(_deviceName);
                GPIB.gpib_get_globals(out ibsta, out iberr, out ibcnt, out ibcntl);
                if ((ibsta & (int)GPIB.ibsta_bits.ERR) != 0)
                {
                    MessageBox.Show("Error in clearing GPIB device with address: " + _devAddress);
                    return;
                }
            }
            ).ConfigureAwait(false);
        }
        /*
        public static Task<GPIBDevice> CreateAsync(int address)
        {
            var ret = new GPIBDevice(address);
            return ret.InitializeAsync();
        }
        public virtual async Task<GPIBDevice> InitializeAsync()
        {
            await OpenGPIBDevice();
            return this;
        }  
        */
        /// <summary>
        /// Send a command through KE-488 usb to GPIB converter to device
        /// </summary>
        /// <param name="sendString"></param>
        public async Task SendGPIBString(string sendString)
        {
            await Task.Run(() =>
            {
                GPIB.ibwrt(_deviceName, sendString, sendString.Length);
                //this causes instrument errors for unknown reasons that don't seem critical
                GPIB.gpib_get_globals(out ibsta, out iberr, out ibcnt, out ibcntl);
                if ((ibsta & (int)GPIB.ibsta_bits.ERR) != 0)
                {
                    MessageBox.Show("Error in writing the string command to the GPIB instrument.");
                    //Close();
                    return;
                }
            }
            ).ConfigureAwait(false);
            
        }
        public async Task<string> ReadGPIBString()
        {
            return await Task.Run(() =>
            {
                System.Diagnostics.Debug.WriteLine("ReadGPIBString");
                StringBuilder strBuild = new StringBuilder(600000);
                GPIB.ibrd(_deviceName, strBuild, 600000);
                if (_devAddress != 14)//ignorant workaround to prevent the KE6485 throwing an error
                {
                    GPIB.gpib_get_globals(out ibsta, out iberr, out ibcnt, out ibcntl);
                    if ((ibsta & (int)GPIB.ibsta_bits.ERR) != 0)
                    {
                        byte lowByte = (byte)(ibsta & 0xff);
                        byte highByte = (byte)((ibsta >> 8) & 0xff);
                        MessageBox.Show("Error in reading the response string from the device with address: " + _devAddress);
                    }
                }
                return strBuild.ToString();
            }).ConfigureAwait(false);
        }
        public async Task ResetDevice()
        {
            await SendGPIBString("*RST;");
        }
        public async Task CloseGPIBDevice()
        {
            await Task.Run(async () =>
            {
                System.Diagnostics.Debug.WriteLine("closing gpibdevice with addr: " + DevAddress);
                await SendGPIBString("*RST;"); //reset the device
                await SendGPIBString(":SYST:LOC;"); //switch control back to local
                //Offline the GPIB interface 
                GPIB.ibonl(_deviceName, 0);
                GPIB.gpib_get_globals(out ibsta, out iberr, out ibcnt, out ibcntl);
                if ((ibsta & (int)GPIB.ibsta_bits.ERR) != 0)
                {
                    MessageBox.Show("Error in closing the GPIB interface for device with address: " + _devAddress);
                    return;
                }
            }).ConfigureAwait(false);           
        }
        #endregion
    }
}
