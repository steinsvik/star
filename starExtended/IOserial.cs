using Steinsvik.Star;
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

namespace Steinsvik.Star
{
    public class SerialIO 
    {
        private SerialPort serialPort = new SerialPort();

        private int dataBits = 8;
        private Handshake handshake = Handshake.None;
        private Parity parity = Parity.None;
        private string portName = "";
        private StopBits stopBits = StopBits.One;
        private bool userSettingsSet = false;
        private bool nameSet = false;
        private int baudRate = 9600;

        public string Name
        {
            get
            {
                return portName;
            }
            set
            {
                this.portName = value;
                nameSet = true;
            }
        }

        public bool IsIdSet
        {
            get
            {
                return nameSet;
            }
        }

        public bool IsSettingsSet
        {
            get { return userSettingsSet; }
        }

        public void SetSerialSettings(int baudrate, int databits = 8, StopBits stopBits = StopBits.One,
             Parity parity = Parity.None, Handshake handshake = Handshake.None)
        {
            this.baudRate = baudrate;
            this.dataBits = databits;
            this.handshake = handshake;
            this.parity = parity;
            this.stopBits = stopBits;
            this.userSettingsSet = true;
        }

        public bool TryOpen()
        {
            try
            {
                Open();
            }
            catch (Exception e)
            {
                e.AddHandledExeption($"Could not open serial port {portName}.", level: Debug.Level.Detail);
                return false;
            }
            return true;
        }

        public virtual void Open()
        {
            serialPort.DataBits = this.dataBits;
            serialPort.Handshake = this.handshake;
            serialPort.Parity = this.parity;
            serialPort.PortName = this.portName;
            serialPort.StopBits = this.stopBits;
            serialPort.BaudRate = this.baudRate;
            serialPort.Open();
            $"Serial port {portName} opened.".AddAppEvent($"Baudrate: {baudRate} Databits: {dataBits} Handshake: {handshake} Parity: {parity} Stopbits: {stopBits}.", Debug.Level.Detail);
        }

        public bool TryWriteStringReadStringFixedSize(List<byte> sendBytes, int numbBytesToReceive, out List<byte> receiveBytes, int timeoutms = 50)
        {
            if (!TryWriteData(sendBytes, true, true, timeoutms))
            {
                receiveBytes = new List<byte>();
                return false;
            }
            return (TryReadDataFixedLength(numbBytesToReceive, out receiveBytes, timeoutms));
        }

        public bool WriteStringReadStringFixedSize(List<byte> sendBytes, int numbBytesToReceive, out List<byte> receiveBytes, int timeoutms = 50)
        {
            if (!WriteData(sendBytes, true, true, timeoutms))
            {
                receiveBytes = new List<byte>();
                return false;
            }
            return (ReadDataFixedLength(numbBytesToReceive, out receiveBytes, timeoutms));
        }

        public bool TryWriteData(List<byte> sendBytes, bool clearTxBuffer = true, bool clearRxBuffer = true, int timeoutms = 50)
        {
            try
            {
                WriteData(sendBytes, clearTxBuffer, clearRxBuffer, timeoutms);
            }
            catch (Exception e)
            {
                e.AddHandledExeption("Error while writing to serial port.", "Data tried " + sendBytes.ToArray().ToHexBytesString(), level: Debug.Level.Normal);
                return false;
            }
            return true;
        }

        private bool WriteData(List<byte> sendBytes, bool clearTxBuffer = true, bool clearRxBuffer = true, int timeoutms = 50)
        {
            serialPort.WriteTimeout = timeoutms;
            if (clearTxBuffer)
                serialPort.DiscardOutBuffer();
            if (clearRxBuffer)
                serialPort.DiscardInBuffer();
            serialPort.Write(sendBytes.ToArray(), 0, sendBytes.Count());
            return true;
        }

        public bool TryReadDataFixedLength(int numbBytes, out List<byte> receiveBytes, int timeoutms = 50)
        {
            try
            {
                return ReadDataFixedLength(numbBytes, out receiveBytes, timeoutms);
            }
            catch (Exception e)
            {
                e.AddHandledExeption($"Error while reading fixed length data ({numbBytes}) serial port.", level: Debug.Level.Normal);
                receiveBytes = new List<byte>();
                return false;
            }
        }

        public bool ReadDataFixedLength(int numbBytes, out List<byte> receiveBytes, int timeoutms = 50)
        {
            int numbRes;
            DateTime endTime;
            int actualTimeoutMS = timeoutms;

            receiveBytes = new List<byte>();

            byte[] tempData = new byte[numbBytes];
            try
            {
                serialPort.ReadTimeout = actualTimeoutMS - 20;
                serialPort.DiscardNull = false;
                serialPort.ReceivedBytesThreshold = numbBytes;

                endTime = DateTime.Now.AddMilliseconds((double)actualTimeoutMS);

                while (DateTime.Now < endTime)
                {
                    if (serialPort.BytesToRead >= numbBytes)
                    {
                        numbRes = serialPort.Read(tempData, 0, numbBytes);
                        if (numbRes != 0)
                        {
                            receiveBytes.AddRange(tempData);
                            return true;
                        }
                        break;
                    }
                    Thread.Sleep(10);  //Removing the sleep did not give faster resend.
                }
                return false;
            }
            catch(TimeoutException e)
            {
                return false;
            }
        }

        public List<byte> ReadAnyAvailableData()
        {
            int numbRes;
            var receiveBytes = new List<byte>();

            serialPort.ReadTimeout = 1;
            serialPort.DiscardNull = false;
            serialPort.ReceivedBytesThreshold = 1;
            int numbRead = serialPort.BytesToRead;
            if (numbRead >= 0)
            {
                byte[] tempData = new byte[numbRead];
                numbRes = serialPort.Read(tempData, 0, numbRead);
                receiveBytes.AddRange(tempData);
                return receiveBytes;
            }
            return receiveBytes;
            // Should not be any timeout in that numb bytes is checked beforehand.
        }

        public bool ReadAnyAvailableData(out List<byte> receiveBytes)
        {
            receiveBytes = ReadAnyAvailableData();
            return (receiveBytes.Count >= 1);
        }

        public bool TryReadAnyAvailableData(out List<byte> receiveBytes)
        {
            try
            {
                receiveBytes = new List<byte>();
                return ReadAnyAvailableData(out receiveBytes);
            }
            catch (Exception e)
            {
                e.AddHandledExeption("Error while reading all available data from serial port.", level: Debug.Level.Normal);
                receiveBytes = new List<byte>();
                return false;
            }
        }

        public bool TryClose()
        {
            try
            {
                Close();
            }
            catch (Exception e)
            {
                e.AddHandledExeption($"Could not close serial port {portName}.", level: Debug.Level.Normal);
                return false;
            }
            return true;
        }

        public virtual void Close()
        {
            serialPort.Close();
            $"Serial port {portName} closed.".AddAppEvent(level: Debug.Level.Detail);
        }

        public bool IsOpen
        {
            get
            {
                return serialPort.IsOpen;
            }
        }
    }
    public class SerialIOwithListener : SerialIO
    { 

        const int serialListenerSleeptimeMS = 1;
        bool serialListenerThreadShouldStop = false;
        Thread serialListenerThread;
        public delegate bool ExiseRelevantStringDelegate(List<byte> sourceBytes, out List<byte> resultBytes, out List<byte> remainingBytes, out List<byte> discarededBytes);
        private ExiseRelevantStringDelegate exiseRelevantStringFunction;

        public int MaxBufferSize { get; set; } = 100;

        override public void Open()
        {
            base.Open();

            serialListenerThreadShouldStop = false;
            serialListenerThread = new Thread(new ThreadStart(SerialListenerThread));
            serialListenerThread.IsBackground = true;
            serialListenerThread.Start();
        }

        public void Open(ExiseRelevantStringDelegate exiseRelevantStringFunction)
        {
            base.Open();

            serialListenerThreadShouldStop = false;
            serialListenerThread = new Thread(new ThreadStart(SerialListenerThread));
            serialListenerThread.IsBackground = true;
            serialListenerThread.Start();
        }

        private bool DefaultExiseRelevantStringFunction(List<byte> sourceBytes, out List<byte> resultBytes, out List<byte> remainingBytes, out List<byte> discarededBytes)
        {
            resultBytes = new List<byte>();
            discarededBytes = new List<byte>();
            remainingBytes = new List<byte>();

            //Add custiom info for decode. Length, start, stop checksum, 
        }

        override public void Close()
        {
            serialListenerThreadShouldStop = true;
            // base.Close();
        }

        private void SerialListenerThread()
        {
            try
            {
                List<byte> innBuffer = new List<byte>();
                while (true)
                {
                    List<byte> receiveBytes = new List<byte>();
                    if(ReadAnyAvailableData(out receiveBytes))
                    {
                        innBuffer.AddRange(receiveBytes);
                    }
                    if (serialListenerThreadShouldStop)
                    {
                        break;
                    }
                    if (innBuffer.Count >= MaxBufferSize)
                    {
                        innBuffer.RemoveRange(0, innBuffer.Count - MaxBufferSize);
                    }
                    Thread.Sleep(serialListenerSleeptimeMS);
                }
            }
            catch
            {
                serialListenerThreadShouldStop = false;
            }
            serialListenerThreadShouldStop = false;
        }
    }
}