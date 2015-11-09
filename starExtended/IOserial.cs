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

        public void setSerialSettings(int baudrate, int databits = 8, StopBits stopBits = StopBits.One,
             Parity parity = Parity.None, Handshake handshake = Handshake.None)
        {
            this.baudRate = baudrate;
            this.dataBits = databits;
            this.handshake = handshake;
            this.parity = parity;
            this.stopBits = stopBits;
            this.userSettingsSet = true;
        }

        public bool Open()
        {
            try
            {
                serialPort.DataBits = this.dataBits;
                serialPort.Handshake = this.handshake;
                serialPort.Parity = this.parity;
                serialPort.PortName = this.portName;
                serialPort.StopBits = this.stopBits;
                serialPort.BaudRate = this.baudRate;
                serialPort.Open();
                $"Serial port {portName} opened.".AddAppEvent($"Baudrate: {baudRate} Databits: {dataBits} Handshake: {handshake} Parity: {parity} Stopbits: {stopBits}.",Debug.Level.Detail);
            }
            catch(Exception e)
            {
                e.AddHandledExeption($"Could not open serial port {portName}.", level: Debug.Level.Detail);
                return false;
            }
            return true;
        }

        public bool WriteStringReadStringFixedSize(List<byte> sendBytes, int numbBytesToReceive, out List<byte> receiveBytes, int timeoutms = 50)
        {

            // receiveBytes = "";
            //char[] test = sendString.ToCharArray();
            if (!WriteString(sendBytes, true, true, timeoutms))
            {
                receiveBytes = new List<byte>();
                return false;
            }
            return (ReadStringFixedSize(numbBytesToReceive, out receiveBytes, timeoutms));
        }

        public bool WriteString(List<byte> sendBytes, bool clearTxBuffer = true, bool clearRxBuffer = true, int timeoutms = 50)
        {
            try
            {
                serialPort.WriteTimeout = timeoutms;

                if (clearTxBuffer)
                    serialPort.DiscardOutBuffer();
                if (clearRxBuffer)
                    serialPort.DiscardInBuffer();
                serialPort.Write(sendBytes.ToArray(), 0, sendBytes.Count());
            }
            catch
            { 
                return false;
            }
            return true;
        }

        public bool ReadStringFixedSize(int numbBytes, out List<byte> receiveBytes, int timeoutms = 50)
        {
            int numbRes;
            DateTime endTime;
            int actualTimeoutMS = timeoutms;

            receiveBytes = new List<byte>();

            //char[] tempData = new char[numbBytes];
            byte[] tempData = new byte[numbBytes];
            //char[] tempCharData;
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
            catch(Exception e)
            {
                receiveBytes.Clear();
                return false;
            }
        }

        public bool Close()
        {
            try
            {
                serialPort.Close();
                $"Serial port {portName} closed.".AddAppEvent(level: Debug.Level.Detail);
            }
            catch (Exception e)
            {
                e.AddHandledExeption($"Could not close serial port {portName}.", level: Debug.Level.Normal);
                return false;
            }
            return true;
        }

        public bool IsOpen
        {
            get
            {
                return serialPort.IsOpen;
            }
        }
    }
}