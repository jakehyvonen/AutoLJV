using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeviceBatchGenerics.Support.DataMapping;

namespace AutoLJV.Instrument_Control
{
    public class PRCameraController : IDisposable
    {
        bool isInitializing = true;
        bool isRecordingELSpec = false;
        ManualResetEvent[] dataReceivedEvent = new ManualResetEvent[1] { new ManualResetEvent(false) };
        SerialPort serialPort;
        public EventHandler DataToParse;
        public bool DataReceivedBool = false;
        public bool ExceededMeasurementRange = false;
        public string ReceivedData;
        public List<ELSpecDatum> PresentELSpec = new List<ELSpecDatum>();
        public string InitialSerialResponseTerminator;
        public string SerialResponseTerminator;
        public string InitialCommand;
        public string TimeOutResponse = "timed out";

        public void Initialize(string initResponse, string response, string initCommand, string comport = "COM2", int baud = 9600)
        {
            SetupSerialPort(comport, baud);
            InitialSerialResponseTerminator = initResponse;
            SerialResponseTerminator = response;
            InitialCommand = initCommand;
            EstablishConnection();
        }
        private void SetupSerialPort(string comport, int baud)
        {
            try
            {
                serialPort = new SerialPort(comport, baud, Parity.None, 8, StopBits.One);
                serialPort.Open();
                serialPort.DtrEnable = true;
                serialPort.RtsEnable = true;
                serialPort.DataReceived += TheSerialPort_DataReceived;
                //serialPort.ReadTimeout = 22222;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }
        public async void EstablishConnection()
        {
            string firstresponse = await SendCommandAndWaitForResponse(InitialCommand, 1111);
            Debug.WriteLine("EstablishConnection firstresponse: " + firstresponse);
            await Task.Delay(1111);
            string secondresponse = await SendCommandAndWaitForResponse(InitialCommand, 1111);
            Debug.WriteLine("EstablishConnection secondresponse: " + secondresponse);
            await Task.Delay(1111);
            //third time's the charm to get ancient equipment to talk gud
            string thirdresponse = await SendCommandAndWaitForResponse(InitialCommand, 1111);
            Debug.WriteLine("EstablishConnection thirdresponse: " + thirdresponse);
            if (thirdresponse == TimeOutResponse)
            {
                System.Windows.MessageBox.Show("Please turn on the PhotoResearch Camera");
                EstablishConnection();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("PRCamera ACKed");
            }
        }
        #region Measurement Tasks
        public async Task<PRCamRawLuminanceDatum> LuminanceMeasurement()
        {
            return await Task.Run(async () =>
            {
                return ParseM1String(await SendCommandAndWaitForResponse("M1").ConfigureAwait(false));
            }).ConfigureAwait(false);
        }
        public async Task<List<ELSpecDatum>> ELSpecMeasurement(bool usingM1Reading = false)
        {
            return await Task.Run(async () =>
            {
                isRecordingELSpec = true;
                if (usingM1Reading)
                    await SendCommandAndWaitForResponse("D5").ConfigureAwait(false);//D5 doesn't take a new measurement, only fetches Radiance data
                else
                    await SendCommandAndWaitForResponse("M5").ConfigureAwait(false);//take measurement and return radiance curve
                return PresentELSpec;
            }).ConfigureAwait(false);
        }
        public async Task<string> SendCommandAndWaitForResponse(string command, int timeoutms = 33333)
        {
            return await Task.Run(async () =>
            {
                if (command.Substring(command.Length - 1) == "5")//if the last character is 5, we should expect a spectrum
                {

                    Debug.WriteLine("Recording EL Spec");
                    //serialPort.DiscardInBuffer();
                    await Task.Delay(111);
                }
                Debug.WriteLine("Sending command to PR camera: " + command);
                string response = TimeOutResponse;
                dataReceivedEvent = new ManualResetEvent[1] { new ManualResetEvent(false) };
                Debug.WriteLine("isRecordingELSpec1 = " + isRecordingELSpec);
                await SendCommand(command);
                var eventResponse = WaitHandle.WaitAny(dataReceivedEvent, timeoutms);
                if (eventResponse != WaitHandle.WaitTimeout)
                    response = ReceivedData;
                Debug.WriteLine("response to " + command + ": " + response);
                return response;
            }).ConfigureAwait(false);
        }
        private Task SendCommand(string command)
        {
            return Task.Run(() =>
            {
                DataReceivedBool = false;
                byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                commandBytes = addByteToEndOfArray(commandBytes, 0x0D);//0x0D=carriage return in ASCII
                serialPort.Write(commandBytes, 0, commandBytes.Count());
            }
            );
        }
        #endregion
        private void TheSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Debug.WriteLine("isRecordingELSpec2 = " + isRecordingELSpec);
            if (isInitializing)
            {
                ReceivedData = serialPort.ReadTo(InitialSerialResponseTerminator); //read until MODE after initialization command
                Debug.WriteLine("Initial ReceivedData: " + ReceivedData);
                serialPort.ReadExisting();//clear the buffer
                serialPort.DiscardInBuffer();//maybe this is more appropriate
                if (ReceivedData.Count() > 2)
                {
                    isInitializing = false;
                    Debug.WriteLine("done initializing");
                }
            }
            else if (isRecordingELSpec)
            {
                Debug.WriteLine("Began recording EL Spectrum");
                PresentELSpec = new List<ELSpecDatum>();
                ReceivedData = serialPort.ReadTo(SerialResponseTerminator); //read until CR LF (0x0D 0x0A)
                ReceivedData = serialPort.ReadTo(SerialResponseTerminator); //read until CR LF (0x0D 0x0A)
                bool reached780nm = false;
                while (!reached780nm)
                {
                    string specPoint = serialPort.ReadTo(SerialResponseTerminator);
                    Debug.WriteLine("specPoint: " + specPoint);
                    ELSpecDatum datum = ParsedSpecString(specPoint);
                    PresentELSpec.Add(datum);
                    if (datum.Wavelength == 780)
                    {
                        reached780nm = true;
                        isRecordingELSpec = false;
                        DataReceivedBool = true;
                        dataReceivedEvent[0].Set();
                        Debug.WriteLine("Successfully recorded EL Spectrum");
                    }
                }
            }
            else
            {
                if (serialPort.BytesToRead > 1)//ignorant workaround for weird issue where PR650 is sending 1 byte after ELSpec measurement
                {
                    try
                    {
                        ReceivedData = serialPort.ReadTo(SerialResponseTerminator); //read until CR LF (0x0D 0x0A)
                        DataReceivedBool = true;
                        dataReceivedEvent[0].Set();
                        Debug.WriteLine("ReceivedData: " + ReceivedData);
                    }
                    catch (TimeoutException te)
                    {
                        Debug.WriteLine("serialPort.ReadTo() timed out: " + te.Message);
                    }
                }
            }
            //DataReceivedBool = true;
            //DataToParse?.Invoke(this, EventArgs.Empty);
            //dataReceivedEvent[0].Set();
            serialPort.DiscardInBuffer();
        }
        private byte[] addByteToEndOfArray(byte[] bArray, byte newByte)
        {
            byte[] newArray = new byte[bArray.Length + 1];
            bArray.CopyTo(newArray, 0);
            newArray[bArray.Length] = newByte;
            return newArray;
        }
        #region Data Processing
        private PRCamRawLuminanceDatum ParseM1String(string s)
        {
            PRCamRawLuminanceDatum datum = new PRCamRawLuminanceDatum();

            string[] data = s.Split(',');
            if (data[0].Contains("19"))
            {
                ExceededMeasurementRange = true;
                Debug.WriteLine("Exceeded camera measurement range");
            }
            else if (s != TimeOutResponse)
            {
                datum.Luminance = Convert.ToDecimal(Convert.ToDouble(data[2]));//can't directly convert to decimal because reasons
                datum.CIEx = Convert.ToDecimal(data[3]);
                datum.CIEy = Convert.ToDecimal(data[4]);
            }

            return datum;
        }
        private ELSpecDatum ParsedSpecString(string specstring)
        {
            ELSpecDatum datum = new ELSpecDatum();
            string[] array = specstring.Split(',');
            datum.Wavelength = Convert.ToDouble(array[0]);
            datum.Intensity = Convert.ToDouble(array[1]);
            return datum;
        }
        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                //get rid of managed resources
                serialPort.Close();
            }
        }
    }
}
