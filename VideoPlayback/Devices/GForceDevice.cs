using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace mPUObserver
{
    public class GForceDevice
    {
        public event EventHandler<GForceChangedEventArgs> GForceChanged;

        //gforce items
        private Buffer buffer;

       List <Vector3> values = new List<Vector3>();

        private List<double> XValues = new List<double>(); 
        private List<double> YValues = new List<double>();
        private List<double> ZValues = new List<double>();

        public double maxX;
        public double maxY;
        public double maxZ;

        public double centerX;
        public double centerY;
        public double centerZ;

        DataReader DataReaderObject = null;
        private CancellationTokenSource ReadCancellationTokenSource;
        private Object ReadCancelLock = new Object();


        public GForceDevice()
        {
            buffer = new Buffer();
            buffer.MessageReceived += Buffer_MessageReceived;
        }


        //this should be broken out to a new class, "gmeter device"
        public async Task OpenGMeter(String com)
        {
            Close();

            try
            {
                ReadCancellationTokenSource = new CancellationTokenSource();

                string aqs = SerialDevice.GetDeviceSelector("COM3");
                var dis = await DeviceInformation.FindAllAsync(aqs);
                string deviceId = string.Empty;
                if (dis.Any())
                {
                    deviceId = dis.First().Id;
                }

                 SerialDevice serialPort = await SerialDevice.FromIdAsync(deviceId);

                if (serialPort != null)
                {
                    serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                    serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                    serialPort.BaudRate = 500000;
                    serialPort.Parity = SerialParity.None;
                    serialPort.StopBits = SerialStopBitCount.One;
                    serialPort.DataBits = 8;
                    serialPort.Handshake = SerialHandshake.None;

                    DataReaderObject = new DataReader(serialPort.InputStream);
           
                    while (true)
                    {
                        try
                        {
                          
                            await ReadAsync(ReadCancellationTokenSource.Token);

                           
                        }
                        catch (Exception err)
                        {
                            var t = "";
                        }
                    }
                }
            }
            catch (OperationCanceledException /*exception*/)
            {
                
            }
            catch (Exception err) 
            {
         
            }
        }

        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            uint bufferSize = 1024;
            Task<UInt32> loadAsyncTask;
            lock (ReadCancelLock)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
                loadAsyncTask = DataReaderObject.LoadAsync(bufferSize).AsTask(cancellationToken);
            }

            UInt32 bytesRead = await loadAsyncTask;

            byte[] readBytes =new byte[bytesRead];


            if (bytesRead > 0)
            {
                DataReaderObject.ReadBytes(readBytes);

                foreach (var item in readBytes)
                {
                    var datas = Convert.ToChar(item);
                    buffer.AppendText(datas.ToString());
                }
            }
        }

        //private void _serialSrcPortGMeter_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    if (e.EventType == SerialData.Chars)
        //    {
        //            SerialPortStream sp = (SerialPortStream)sender;
        //            string indata = sp.ReadExisting();
        //            buffer.AppendText(indata);
        //    }
        //}

        public void Close()
        {
            try
            {
                if (ReadCancellationTokenSource != null)
                {
                    ReadCancellationTokenSource.Dispose();
                    ReadCancellationTokenSource = null;
                }
            }
            catch (Exception)
            { 
            }
        }

      
        public void Buffer_MessageReceived(string message)
        {
            try
            {
                String result = message.Substring(1, message.Length - 2);
                String[] split = result.Split(',');

                Double rz = Double.Parse(split[0]);
                Double rx = Double.Parse(split[1]);
                Double ry = Double.Parse(split[2]);

                Double x = Double.Parse(split[3]);
                Double y = Double.Parse(split[4]);
                Double z = Double.Parse(split[5]);

                Double qx = Double.Parse(split[6]);
                Double qy = Double.Parse(split[7]);
                Double qz = Double.Parse(split[8]);
                Double qw = Double.Parse(split[9]);

                Vector3 accel = new Vector3((float)y, (float)x, (float)z);
                Vector3 rotation = new Vector3((float)rx, (float)ry, (float)rz);
                Quaternion quaternion = new Quaternion((float)qy, (float)qx, (float)qz, -(float)qw);
             
                GForceChangedEventArgs e = new GForceChangedEventArgs();
                e.accel = accel;
                e.rotation = rotation;
                e.quaternion = quaternion;
                OnGForceChanged(e);
            }
            catch (Exception)
            {

            }
        }

        protected virtual void OnGForceChanged(GForceChangedEventArgs e)
        {
            EventHandler<GForceChangedEventArgs> handler = GForceChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }

    public class GForceChangedEventArgs
    {
        public Vector3 accel { get; set; }
        public Vector3 rotation { get; set; }
        public Quaternion quaternion { get; set; }
        public bool HasOBDData { get; set; }
        public double RPM { get; set; }
        public double Speed { get; set; }
    }

    public class Buffer
    {
        private readonly char _startOfText = 'X';
        private readonly char _endOfText = 'E';

        public event MessageReceivedEventHandler MessageReceived;

        public delegate void MessageReceivedEventHandler(string message);

        public event DataIgnoredEventHandler DataIgnored;

        public delegate void DataIgnoredEventHandler(string text);

        private StringBuilder _buffer = new StringBuilder();

        public void AppendText(string text)
        {
            _buffer.Append(text);
            while (processBuffer(_buffer))
            {
            }
        }

        private bool processBuffer(StringBuilder buffer)
        {
            try
            {
                while (buffer.Length > 0 && buffer[0].CompareTo(_startOfText) != 0)
                {
                    buffer.Remove(0, 1);
                }
            }
            catch (Exception)
            {
            }

            bool foundSomethingToProcess = false;
            string current = buffer.ToString();

            int stxPosition = current.IndexOf(_startOfText);
            int etxPosition = current.IndexOf(_endOfText);
            if ((stxPosition >= 0) & (etxPosition >= 0) & (etxPosition > stxPosition))
            {
                string messageText = current.Substring(0, etxPosition + 1);
                buffer.Remove(0, messageText.Length);
                if (stxPosition > 0)
                {
                    DataIgnored?.Invoke(messageText.Substring(0, stxPosition));
                    messageText = messageText.Substring(stxPosition);
                }
                MessageReceived?.Invoke(messageText);
                foundSomethingToProcess = true;
            }
            else if ((stxPosition == -1) & (current.Length != 0))
            {
                buffer.Remove(0, current.Length);
                DataIgnored?.Invoke(current);
                foundSomethingToProcess = true;
            }
            return foundSomethingToProcess;
        }

        public void Flush()
        {
            if (_buffer.Length != 0)
                DataIgnored?.Invoke(_buffer.ToString());
        }
    }
}
