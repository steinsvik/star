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
            //byte[] byteString = new byte[sendString.Length];
            try
            {
                serialPort.WriteTimeout = timeoutms;

                if (clearTxBuffer)
                    serialPort.DiscardOutBuffer();
                if (clearRxBuffer)
                    serialPort.DiscardInBuffer();
                //for (int i = 0; i < sendString.Count; i++)
                //    byteString[i] = Convert.ToByte(sendString[i]);
                serialPort.Write(sendBytes.ToArray(), 0, sendBytes.Count());

                //serialPort.Write(sendString);

            }
            catch
            {//TODO: Add handled exception handler
                return false;
            }
            return true;
        }

        public bool ReadStringFixedSize(int numbBytes, out List<byte> receiveBytes, int timeoutms = 50)
        {
            int numbRes;
            DateTime endTime;
            int actualTimeoutMS = timeoutms; //500;   //TODO:??????????????

            receiveBytes = new List<byte>();

            //char[] tempData = new char[numbBytes];
            byte[] tempData = new byte[numbBytes];
            //char[] tempCharData;
            try
            {
                //Thread.Sleep(50);  //TODO: Find betterway to get all 7 chars! Event og pingpong?
                serialPort.ReadTimeout = actualTimeoutMS - 20;//timeoutms;
                serialPort.DiscardNull = false;
                serialPort.ReceivedBytesThreshold = numbBytes;

                //receiveString = "";
                endTime = DateTime.Now.AddMilliseconds((double)actualTimeoutMS);

                while (DateTime.Now < endTime)
                {
                    if (serialPort.BytesToRead >= numbBytes)
                    {
                        numbRes = serialPort.Read(tempData, 0, numbBytes);
                        if (numbRes != 0)
                        {
                            //tempCharData = new char[numbRes];
                            //for (int i = 0; i < numbRes; i++)
                            //{
                            //    tempCharData[i] = Convert.ToChar(tempData[i]);
                            //}
                            //receiveString = new string(tempCharData, 0, numbRes);//tempData.ToString();
                            //receive
                            receiveBytes.AddRange(tempData);
                            return true;
                        }
                        break;
                    }
                    Thread.Sleep(10);  //Even removing the sleep did not give faster resend.
                }

                return false;
            }
            catch
            {//TODO: Add handled exception reporter
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
            catch
            { //TODO: Add handled exception handler
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

        //void serialIO_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    ReceivedBytesThreshold

        //    //Initialize a buffer to hold the received data
        //    byte[] buffer = new byte[_serialPort.ReadBufferSize];

        //    //There is no accurate method for checking how many bytes are read
        //    //unless you check the return from the Read method
        //    int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);

        //    //For the example assume the data we are received is ASCII data.
        //    tString += Encoding.ASCII.GetString(buffer, 0, bytesRead);
        //    //Check if string contains the terminator
        //    if (tString.IndexOf((char)_terminator) > -1)
        //    {
        //        //If tString does contain terminator we cannot assume that it is the last character received
        //        string workingString = tString.Substring(0, tString.IndexOf((char)_terminator));
        //        //Remove the data up to the terminator from tString
        //        tString = tString.Substring(tString.IndexOf((char)_terminator));
        //        //Do something with workingString
        //        Console.WriteLine(workingString);
        //    }
        //}
    }
}