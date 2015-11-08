using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Management;   
using Microsoft.Win32;    
using Steinsvik.Star;

namespace Steinsvik.Star
{
    /// <summary>
    /// provides troubleshooting and debug functionality.
    /// </summary>
    static public class Debug
    {
        public enum Level { Dev, Detail, Normal, Major };
        public enum MessageType { FatalUnHandledExeption, HandledException, Warning, AppEvent, UserAction };

        private const int messageOverflowLimit = 1000;
        private const int profilingStorageTimeMs = 1000;
        private const int messageLogBreakTimeMs = 10;
        private const int trafficLogBreakTimeMs = 10;

        public static Debug.Level DebugLevel { get; set; } = Debug.Level.Normal;
        private static bool gatherSystemInfo = true;
        
        /// <summary> Use to initiate all Debug functionality. Must be called in start of program. </summary>
        static public void StartDebugRegEngine(Level debugEngineLevel = Level.Normal, bool gatherSystemInfo = true)
        {
            DebugLevel = debugEngineLevel;
            Debug.gatherSystemInfo = gatherSystemInfo;

            // Provide an unhandled exception hook;
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);

            Debug.AddAppEvent("Application Started", ExtensionsAssembly.GetFullID(Assembly.GetEntryAssembly()), Level.Major);
            Debug.AddAppEvent("Application Started.Details", App.GetAllAssembliesStringArray(false, true, false).ToDelimetedString("//"), Level.Dev);

            InitMessageHandling();
            InitDebugValue();
            InitSystemInfoCollector();
            InitTraficLogger();
        }

        #region debug values

        public class DebugValueHolder
        {
            ConcurrentDictionary<string, object> valueHolder = new ConcurrentDictionary<string, object>();

            public void Add(string Name, object Verdi) //where TValue : string, float, bool, int?
            {
                valueHolder[Name] = Verdi;
            }

            public string GetValuePairList()
            {
                StringBuilder sb = new StringBuilder("");
                foreach (KeyValuePair<string, object> KeyVerdi in valueHolder)
                {
                    sb.AppendLine(KeyVerdi.Key + ": " + KeyVerdi.Value.ToString());
                }
                return sb.ToString();
            }
        }

        public static DebugValueHolder profilingValues { get; private set; } = new DebugValueHolder();

        static private void InitDebugValue()
        {
        }

        static public void AddDebugValue<T>(string Name, T Verdi) //where TValue : string, float, bool, int?
        {
            if (DebugLevel == Level.Dev)
            {
                profilingValues.Add(Name, Verdi);
            }
        }

        static public void AddDebugValue<T>(this T variableToAdd) //where TValue : string, float, bool, int?
        {
            if (DebugLevel == Level.Dev)
            {
                profilingValues.Add(Util.GetMemberName(() => variableToAdd), variableToAdd);
            }
        }
        #endregion

        #region debug messages
        public delegate void NewDebugMessageHandler(object sender, DebugMessage msg);
        public static event NewDebugMessageHandler NewDebugMessage;
        // Going with regular Q, event though is not thread safe. I have only  one exit. Figure it will be safe...
        private static readonly Queue<DebugMessage> DebugMessageQueueForPassingToEvent = new Queue<DebugMessage>();

        static private void InitMessageHandling()
        {
            // Starting the debug log thread.
            Thread debugThread = new Thread(new ThreadStart(DebugMsgLogThread));
            debugThread.IsBackground = true;
            debugThread.Start();
        }

        static public void AddHandledExeption(this Exception errorKode, string name = "", string details = "", Level level = Level.Detail,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            if (name == "")
                name = errorKode.Message;
            AddMessageCore(MessageType.HandledException, name, errorKode.Message + "/n/r" + errorKode.ToString() + "/n/r" + details, level,
                memberName, sourceFilePath, sourceLineNumber);
        }

        static public void AddAppEvent(string message, string details, Level level = Level.Dev,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            AddMessageCore(MessageType.AppEvent, message, details, level, memberName, sourceFilePath, sourceLineNumber);
        }

        static public void AddUserAction(string message, string details, Level level = Level.Dev,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            AddMessageCore(MessageType.UserAction, message, details, level, memberName, sourceFilePath, sourceLineNumber);
        }

        static private void AddFatalUnhandledExeption(Exception errorKode)
        {
            var errorDumpSB = new StringBuilder();
            var profilesSB = new StringBuilder(profilingValues.GetValuePairList());
            AddMessageCore(MessageType.FatalUnHandledExeption, errorKode.Message, errorKode.ToString()
                , Level.Major, "", "", 0);
            AddAppEvent("Application Exit with error", errorKode.Message + "\r\nCurrent gathered value log values:\r\n" + profilesSB.ToString(), Level.Major);

            errorDumpSB.AppendLine("Application terminated with unhandled exception");
            errorDumpSB.AppendLine(App.GetEntryAssembly().GetFullID());
            errorDumpSB.AppendLine("");
            errorDumpSB.Append("Error message:" + errorKode.Message);
            errorDumpSB.AppendLine("");
            errorDumpSB.Append(errorKode.ToString());
            errorDumpSB.AppendLine("");
            errorDumpSB.AppendLine(" ");
            errorDumpSB.AppendLine("Assembly information:");
            errorDumpSB.AppendLine(App.GetAllAssembliesStringArray().ToDelimetedString(Environment.NewLine));
            errorDumpSB.AppendLine(" ");
            errorDumpSB.AppendLine("System information gathered at start:");
            errorDumpSB.AppendLine(Debug.GetSystemInfo());
            errorDumpSB.AppendLine("Current gathered value log values:");
            errorDumpSB.AppendLine(profilingValues.GetValuePairList());
            //errorBumpSB.AppendLine("Application configuration:");
            //errorBumpSB.AppendLine(optionSystem.GetAllSettingsForReporting());
        }

        static private void DebugMsgLogThread()
        {
            // DebugMessageCollected outResult;
            // PerpetualCVTLogFile debugFile = new PerpetualCVTLogFile(PostScript: "Debug Log Message", filType: FileType.Debug);
            while (true)
            {
                int numbMsgInQue = Debug.DebugMessageQueueForPassingToEvent.Count;
                if (numbMsgInQue >= messageOverflowLimit)
                {
                    Debug.DebugMessageQueueForPassingToEvent.Clear();
                    Debug.AddHandledExeption(new InternalBufferOverflowException(), "Debug message queue overflow.", "Limit: " +
                            messageOverflowLimit.ToString() + ". Items found and removed: " + numbMsgInQue.ToString(), Level.Major);
                    numbMsgInQue = Debug.DebugMessageQueueForPassingToEvent.Count;
                }
                if (NewDebugMessage != null)
                {
                    if (numbMsgInQue > 0)
                    {
                        for (int i = 0; i < numbMsgInQue; i++)
                        {
                            NewDebugMessage(null, DebugMessageQueueForPassingToEvent.Dequeue());
                        }
                    }
                }
                Thread.Sleep(messageLogBreakTimeMs); 
            }
        }

        static private void AddMessageCore(MessageType type, string message, string details, Level level,
            string memberName, string sourceFilePath, int sourceLineNumber)
        {
            if (level >= DebugLevel)
            {
                Debug.DebugMessageQueueForPassingToEvent.Enqueue(new DebugMessage(type, level, DateTime.Now, message, details,
                    Path.GetFileName(sourceFilePath), sourceLineNumber, memberName));
            }
        }
        #endregion

        #region traffic messages
        public enum TrafficValidity { Unknown, Invalid, Valid }
        public enum TrafficDirection { In, Out, Up, Down, A, B, Unknown };  //Direction should be an enum, as decoder might need the information
        public delegate void NewTrafficMessageHandler(object sender, TrafficMessage msg);
        public static event NewTrafficMessageHandler NewTrafficMessage;
        public static readonly Queue<TrafficMessage> TrafficMessageQueueForSaving = new Queue<TrafficMessage>();
        public delegate void TrafficMsgDecoder(byte[] rawData, TrafficDirection direction, out TrafficValidity valid, out string sourceAdr,
            out string targetAdr, out string command, out string detail, out string checkSum);

        public struct DebugMessage
        {
            MessageType MessageType;
            Level MessageLevel;
            DateTime Timestamp;
            string Message;
            string Details;
            string SourceFile;
            int SourceLine;
            string SourceMemberName;
            public DebugMessage(MessageType type, Level level, DateTime timestamp, string message, string details,
                    string filePath, int sourceLineNumber, string memberName)
            {
                this.MessageType = type;
                this.MessageLevel = level;
                this.Timestamp = timestamp;
                this.Message = message;
                this.Details = details;
                this.SourceFile = filePath;
                this.SourceLine = sourceLineNumber;
                this.SourceMemberName = memberName;
            }
        }

        public struct TrafficMessage 
        {
            DateTime timestamp;
            string trafficType;
            byte[] rawData;
            string sourceInterface;
            TrafficDirection direction;
            TrafficMsgDecoder decodeMessage;
            string additionalInformation;

            public TrafficMessage(string trafficType, byte[] rawData, DateTime ?timestamp = null, string sourceInterface = "",
                TrafficDirection direction = TrafficDirection.Unknown, TrafficMsgDecoder decodeMessage = null, string additionalInfo = "")
            {
                timestamp = timestamp ?? DateTime.Now;
                this.timestamp = timestamp.Value;
                this.trafficType = trafficType;
                this.rawData = rawData;
                this.sourceInterface = sourceInterface;
                this.direction = direction;
                this.decodeMessage = decodeMessage;
                this.additionalInformation = additionalInfo;
            }
        }

        static private void InitTraficLogger()
        {
            Thread trafficThread = new Thread(new ThreadStart(TrafficMsgLogThread));
            trafficThread.Start();
            trafficThread.IsBackground = true;
        }

        /// <summary>
        /// Add a messate to the log. For best use, a function should be created for TrafficMsgDecoder delegate to decode the message.
        /// </summary>
        /// <example>
        /// public static void TestMsgDecoder(byte[] rawData, Debug.LogTraffic.Direction direction, out Debug.LogTraffic.Validity valid, out string sourceAdr, out string targetAdr, out string command, out string detail, out string checkSum)
        /// {
        ///     targetAdr = "0x" + rawData[0].ToString("X2");
        ///     sourceAdr = "0x" + rawData[1].ToString("X2");
        ///     command = "0x" + rawData[2].ToString("X2") + "Test Command";
        ///     checkSum = "0x" + rawData[7].ToString("X2") + rawData[8].ToString("X2");
        ///     detail = "0x" + rawData[3].ToString("X2") + rawData[4].ToString("X2") + rawData[5].ToString("X2") + rawData[6].ToString("X2");
        ///     valid = Debug.LogTraffic.Validity.Valid;
        /// }
        /// Debug.LogTraffic.TrafficMsgDecoder testDekoder = TestMsgDecoder;
        /// Debug.LogTraffic.Add("Test", rawData, testDekoder, "Main RS485", Debug.LogTraffic.Direction.In);
        /// </example>

        static public void AddTrafficMessage(string trafficType, byte[] rawData, TrafficMsgDecoder decodeMessage = null, string sourceInterface = "", TrafficDirection direction = TrafficDirection.Unknown,
                DateTime? timestamp = null, string additionalInfo = "")
        {
            if (DebugLevel == Level.Dev)
            {
                timestamp = timestamp ?? DateTime.Now;

                Debug.TrafficMessageQueueForSaving.Enqueue(new TrafficMessage(trafficType, rawData, timestamp.Value, sourceInterface,
                    direction, decodeMessage, additionalInfo));
            }
        }

        static private void TrafficMsgLogThread()
        {
            while (true)
            {
                if (DebugLevel == Level.Dev)
                {
                    int numbMsgInQue = Debug.TrafficMessageQueueForSaving.Count;
                    if (numbMsgInQue >= messageOverflowLimit)
                    {
                        Debug.TrafficMessageQueueForSaving.Clear();
                        Debug.AddAppEvent("Traffic Queue overflow.", "Limit: " +
                            messageOverflowLimit.ToString() + ". Items found and removed: " + numbMsgInQue.ToString(), Level.Major);
                        numbMsgInQue = Debug.TrafficMessageQueueForSaving.Count;
                    }
                    if (NewTrafficMessage != null)
                    {
                        if (numbMsgInQue > 0)
                        {
                            for (int i = 0; i < numbMsgInQue; i++)
                            {
                                NewTrafficMessage(null, TrafficMessageQueueForSaving.Dequeue());
                            }
                        }
                    }
                    Thread.Sleep(trafficLogBreakTimeMs);
                }
                else
                    Thread.Sleep(trafficLogBreakTimeMs*10); 
            }
        }
        #endregion

        #region Systeminfo
        public delegate void SystemInfoIsRetreivedHandler(object sender, EventArgs e);
        public static event SystemInfoIsRetreivedHandler SystemInfoIsRetreived;
        public static bool SystemInfoRetreived { get; private set; } = false;
        private static string systemInformation;

        static public string GetSystemInfo()   // the Name property
        {
            return systemInformation;
        }

        static private void InitSystemInfoCollector()
        {
            if (gatherSystemInfo)
            {
                systemInformation = "System information not retrieved.";
                Thread systemInfoThread = new Thread(new ThreadStart(SystemInfoRetreivalThread));
                systemInfoThread.IsBackground = true;//true;
                systemInfoThread.Start();
            }
            else
                systemInformation = "System information not retrieved. Application not running for long enough.";
        }

        static private void SystemInfoRetreivalThread()
        {
            systemInformation = RetreiveSystemInformation();
            Debug.AddAppEvent("System Information Gathered", systemInformation, Level.Dev);
            SystemInfoRetreived = true;
            if (SystemInfoIsRetreived != null)
                SystemInfoIsRetreived(null, EventArgs.Empty);
        }

        static private string RetreiveSystemInformation()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string[] Win32_OS_strings = {"Caption", "OSArchitecture", "CSDVersion", "BuildNumber", "CountryCode",
                                            "FreePhysicalMemory", "FreeSpaceInPagingFiles", "FreeVirtualMemory",
                                            "InstallDate", "LastBootUpTime", "NumberOfProcesses", "OperatingSystemSKU",
                                            "OSLanguage","RegisteredUser", "SystemDirectory", "TotalVirtualMemorySize",
                                            "TotalVisibleMemorySize", "WindowsDirectory"};
                string[] Win32_ComputerSystem_strings = {"Caption", "EnableDaylightSavingsTime", "Manufacturer", "Model",
                                                        "Name", "NumberOfLogicalProcessors", "NumberOfProcessors",
                                                        "PCSystemType", "Status", "SystemType", "ThermalState",
                                                        "TotalPhysicalMemory", "UserName", "Domain"};
                string[] Win32_Processor_string = {"Caption", "Description", "AddressWidth", "Architecture", "DeviceID", "Family",
                                                    "MaxClockSpeed", "CurrentClockSpeed", "Name", "NumberOfCores", "Revision",
                                                    "L2CacheSize", "L2CacheSpeed", "NumberOfLogicalProcessors"};
                string[] Win32_System_Enclosure = { "PartNumber", "SerialNumber", "SMBIOSAssetTag" };
                string[] Win32_Network_Adapters = { "Caption", "Description", "MACAddress" };
                string[] Win32_Network_Adapters_Sub = { "IPAddress", "DefaultIPGateway", "IPSubnet" };
                string[] Win32_PnP_With_Problem = { "Caption", "Description", "ConfigManagerErrorCode", "ClassGuid", "DeviceID", "Manufacturer",
                                                    "Name", "PNPDeviceID", "Service"};
                string[] Win32_Disk = { "MediaType", "Model", "SerialNumber", "InterfaceType", "Size", "Partitions",
                                        "Signature", "FirmwareRevision"};
                sb.AppendLine("AP App: " + App.GetEntryAssembly().GetFullID());
                sb.AppendLine("AP App location: " + App.GetEntryAssembly().GetPath());
                ManagementObjectSearcher managementObjSearch = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
                int i = 0;
                foreach (ManagementObject managementObject in managementObjSearch.Get())
                {

                    foreach (string name in Win32_OS_strings)
                    {
                        if (managementObject[name] != null)
                        {
                            sb.AppendLine("OS " + i + "  " + name + " : " + managementObject[name].ToString());
                        }
                    }
                    i++;
                }
                managementObjSearch = new ManagementObjectSearcher("select * from Win32_ComputerSystem");
                i = 0;
                foreach (ManagementObject managementObject in managementObjSearch.Get())
                {
                    foreach (string name in Win32_ComputerSystem_strings)
                    {
                        if (managementObject[name] != null)
                        {
                            sb.AppendLine("CS " + i + "  " + name + " : " + managementObject[name].ToString());
                        }
                    }
                    i++;
                }
                managementObjSearch = new ManagementObjectSearcher("select * from Win32_Processor");
                i = 0;
                foreach (ManagementObject managementObject in managementObjSearch.Get())
                {
                    foreach (string name in Win32_Processor_string)
                    {
                        if (managementObject[name] != null)
                        {
                            sb.AppendLine("Pr " + i + "  " + name + " : " + managementObject[name].ToString());
                        }
                    }
                    i++;
                }
                managementObjSearch = new ManagementObjectSearcher("select * from Win32_SystemEnclosure");
                i = 0;
                foreach (ManagementObject managementObject in managementObjSearch.Get())
                {
                    foreach (string name in Win32_System_Enclosure)
                    {
                        if (managementObject[name] != null)
                        {
                            sb.AppendLine("Se " + i + "  " + name + " : " + managementObject[name].ToString());
                        }
                    }
                    i++;
                }
                managementObjSearch = new ManagementObjectSearcher("select * from Win32_NetworkAdapterConfiguration");
                i = 0;
                foreach (ManagementObject managementObject in managementObjSearch.Get())
                {
                    if (managementObject["MACAddress"] != null)
                    {
                        foreach (string name in Win32_Network_Adapters)
                        {
                            if (managementObject[name] != null)
                            {
                                sb.AppendLine("Na " + i + "  " + name + " : " + managementObject[name].ToString());
                            }
                        }
                        if (managementObject["IPAddress"] != null)
                        {
                            for (int y = (managementObject["IPAddress"] as string[]).GetLowerBound(0); y < (managementObject["IPAddress"] as string[]).GetUpperBound(0); y++)
                            {
                                foreach (string name in Win32_Network_Adapters_Sub)
                                {
                                    if (managementObject[name] != null)
                                    {
                                        if ((managementObject[name] as string[])[y] != null)
                                        {
                                            sb.AppendLine("Na " + i + "" + y + " " + name + " : " + (managementObject[name] as string[])[y].ToString());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    i++;
                }
                managementObjSearch = new ManagementObjectSearcher("select * from Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
                i = 0;
                foreach (ManagementObject managementObject in managementObjSearch.Get())
                {
                    foreach (string name in Win32_PnP_With_Problem)
                    {
                        if (managementObject[name] != null)
                        {
                            sb.AppendLine("PN " + i + "  " + name + " : " + managementObject[name].ToString());
                        }
                    }
                    i++;
                }
                managementObjSearch = new ManagementObjectSearcher("select * from Win32_DiskDrive");
                i = 0;
                foreach (ManagementObject managementObject in managementObjSearch.Get())
                {
                    foreach (string name in Win32_Disk)
                    {
                        if (managementObject[name] != null)
                        {
                            sb.AppendLine("HD " + i + "  " + name + " : " + managementObject[name].ToString());
                        }
                    }
                    i++;
                }
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.AddHandledExeption(e, "System information not retrieved. Failure during retrieval");
                return ("System information not retrieved. Failure during retrieval. Error: " + e.Message);
            }
        }
        #endregion

        #region system hooks
        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Debug.AddFatalUnhandledExeption(e);
            // Allow time to write
            Thread.Sleep(2);

            Process.GetCurrentProcess().Kill();
        }
        #endregion
    }
}
