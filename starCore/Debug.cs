using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using Ebs.Star.Core;

namespace Ebs.Star.Core
{
    static public class Debug
    {
        public enum Level { Dev, Detail, Normal, Major };
        public enum MessageType { FatalUnHandledExeption, HandledException, Warning, AppEvent, UserAction };
        // Local members.
        private const int messageOverflowLimit = 1000;
        private const int profilingStorageTimeMs = 1000;
        private const int messageLogBreakTimeMs = 100;
        private const int trafficLogBreakTimeMs = 100;
        // Going with regular Q, event though is not thread safe. I have only  one exit. Figure it will be safe...
        private static readonly Queue<DebugMessageCollected> DebugMessageQueueForSaving = new Queue<DebugMessageCollected>();
        //private static ConcurrentDictionary<string, string> profilingValues = new ConcurrentDictionary<string, string>();
        private static ProfilingDictionary profilingValues = new ProfilingDictionary();

        // Public properties.
        public static Debug.Level debugLevel = Debug.Level.Normal;
        // Public methods.
        static public void StartDebugRegistrationEngine(Level debugEngineLevel = Level.Normal)
        {

            debugLevel = debugEngineLevel;

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);

            // Document application startup. Asuming always at start of App
            Debug.LogMsg.AddAppEvent("Application Started", ExtensionsAssembly.GetFullID(Assembly.GetEntryAssembly()), Level.Major);
            Debug.LogMsg.AddAppEvent("Application Started.Details", Util.GetAllAssemblies(false, true, false).ToDelimetedString("//"), Level.Dev);
            // Initialising Log part of debug
            typeof(Debug.LogMsg).GetMethod("init", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            // Initialising Profile part of debug
            typeof(Debug.LogVal).GetMethod("init", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            // Initialising system infor part of debug
            typeof(Debug.SystemInfo).GetMethod("init", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            // Starting the traffic message log thread.
            typeof(Debug.LogTraffic).GetMethod("init", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

            //            Debug.LogMsg.AddAppEvent("Application Started.Configuration", optionSystem.GetAllSettingsForReporting(), Level.Normal);
        }

        struct DebugMessageCollected : IcvtStruckt
        {
            MessageType MessageType;
            Level MessageLevel;
            DateTime Timestamp;
            string Message;
            string Details;
            string SourceFile;
            int SourceLine;
            string SourceMemberName;
            public DebugMessageCollected(MessageType type, Level level, DateTime timestamp, string message, string details,
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
            public string GetCVTFileHeader()
            {
                return "Time\tType\tLevel\tMessage\tDetails\tSource";
            }
            public int GetCVTNumbCols()
            {
                return 6;
            }
            public string GetCVTFileString()
            {
                return this.Timestamp.ToString(TimeFormat.standardDateTimeFormatWithMs) + "\t" +
                    this.MessageType.ToString() + "\t" + this.MessageLevel.ToString() + "\t" +
                    this.Message + "\t" + this.Details.Replace("\r\n", "//").Replace("\n", "//") + "\t" +
                    this.SourceFile + "(" + this.SourceLine.ToString() + ") " + this.SourceMemberName;
            }
        }
        class ProfilingDictionary : IcvtStruckt
        {
            private ConcurrentDictionary<string, string> actualConcurrentDictionatry;// = new ConcurrentDictionary<string, string>();

            public ProfilingDictionary()
            {
                actualConcurrentDictionatry = new ConcurrentDictionary<string, string>();
            }
            public void Add<TValue>(string Name, TValue Verdi) //where TValue : string, float, bool, int?
            {
                actualConcurrentDictionatry[Name] = Verdi.ToString();
            }
            public string GetCVTFileHeader()
            {
                StringBuilder sb = new StringBuilder("Time\t");
                foreach (KeyValuePair<string, string> KeyVerdi in this.actualConcurrentDictionatry)
                {
                    sb.Append(KeyVerdi.Key + "\t");
                }
                return sb.ToString();
            }
            public int GetCVTNumbCols()
            {
                return actualConcurrentDictionatry.Count + 1;
            }
            public string GetCVTFileString()
            {
                StringBuilder sb = new StringBuilder(DateTime.Now.ToString(TimeFormat.standardDateTimeFormatWithMs) + "\t");
                foreach (KeyValuePair<string, string> KeyVerdi in actualConcurrentDictionatry)
                {
                    sb.Append(KeyVerdi.Value + "\t");
                }
                return sb.ToString();
            }
            public string GetValuePairList()
            {
                StringBuilder sb = new StringBuilder("");
                foreach (KeyValuePair<string, string> KeyVerdi in actualConcurrentDictionatry)
                {
                    sb.AppendLine(KeyVerdi.Key + ": " + KeyVerdi.Value);
                    // do something with entry.Value or entry.Key
                }
                return sb.ToString();
            }
        }

        static public class LogVal
        {
            static private void init()
            {
                // Starting the debug profiling log thread.
                Thread profileThread = new Thread(new ThreadStart(DebugThreadProfileLog));
                profileThread.IsBackground = true;
                profileThread.Start();
            }
            // Private methods. Threads.
            static private void DebugThreadProfileLog()
            {
                if (debugLevel == Level.Dev)
                {
                    while (true)
                    {
                        PerpetualCVTLogFile profileFile = new PerpetualCVTLogFile(PostScript: "Debug Log Value", filType: FileType.Debug);
                        while (true)
                        {
                            profileFile.AddMessage(profilingValues);
                            Thread.Sleep(profilingStorageTimeMs);
                        }

                    }
                }
            }
            static public void Add<TValue>(string Name, TValue Verdi) //where TValue : string, float, bool, int?
            {
                //profilingValues[Name] = Verdi.ToString();
                profilingValues.Add(Name, Verdi);
            }
        }

        static public void AddHandledExeptionB(this Exception errorKode, string name = "", string details = "", Level level = Level.Detail,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            if (name == "")
                name = errorKode.Message;
            LogMsg.AddMessageCore(MessageType.HandledException, name, errorKode.Message + "/n/r" + errorKode.ToString() + "/n/r" + details, level,
                memberName, sourceFilePath, sourceLineNumber);
        }
        static public class LogMsg
        {
            static private void init()
            {
                // Starting the debug log thread.
                Thread debugThread = new Thread(new ThreadStart(DebugThreadMsgLog));
                debugThread.IsBackground = true;
                debugThread.Start();
            }
            static public void AddHandledExeption(string Name, Exception errorKode, string details = "", Level level = Level.Detail,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
            {
                AddMessageCore(MessageType.HandledException, Name, errorKode.Message + "/n/r" + errorKode.ToString() + "/n/r" + details, level,
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
            static public void AddFatalUnhandledExeption(Exception errorKode)
            {
                /*StringBuilder profilesSB = new StringBuilder("");
                foreach (KeyValuePair<string, string> KeyVerdi in profilingValues)
                {
                    profilesSB.AppendLine(KeyVerdi.Key + ": " + KeyVerdi.Value);
                    // do something with entry.Value or entry.Key
                }*/
                StringBuilder profilesSB = new StringBuilder(profilingValues.GetValuePairList());
                AddMessageCore(MessageType.FatalUnHandledExeption, errorKode.Message, errorKode.ToString()
                    , Level.Major, "", "", 0);
                AddAppEvent("Application Exit with error", errorKode.Message + "\r\nCurrent gathered value log values:\r\n" + profilesSB.ToString(), Level.Major);

                Directory.CreateDirectory(FileEnviroment.ProducedFilesPath);
                using (StreamWriter crashReportFile = new StreamWriter(FileEnviroment.ProducedFilesPath + FileEnviroment.GetFileName(PostScript: "Crash report", Extention: "txt")))
                {
                    crashReportFile.WriteLine("Application terminated with unhandled execption");
                    crashReportFile.WriteLine(ExtensionsAssembly.GetFullID());
                    crashReportFile.WriteLine("");
                    crashReportFile.Write("Error message:" + errorKode.Message);
                    crashReportFile.WriteLine("");
                    crashReportFile.Write(errorKode.ToString());
                    crashReportFile.WriteLine("");
                    crashReportFile.WriteLine(" ");
                    crashReportFile.WriteLine("System information gathered at start:");
                    crashReportFile.WriteLine(Debug.SystemInfo.Get());
                    crashReportFile.WriteLine("Current gathered value log values:");
                    crashReportFile.WriteLine(profilingValues.GetValuePairList());
                    crashReportFile.WriteLine("Application configuration:");
                    crashReportFile.WriteLine(optionSystem.GetAllSettingsForReporting());
                }
            }
            static private void DebugThreadMsgLog()
            {
                //DebugMessageCollected outResult;
                PerpetualCVTLogFile debugFile = new PerpetualCVTLogFile(PostScript: "Debug Log Message", filType: FileType.Debug);
                while (true)
                {
                    int numbMsgInQue = Debug.DebugMessageQueueForSaving.Count;
                    if (numbMsgInQue >= messageOverflowLimit)
                    {
                        //Empty the Q if completly full. Add error.
                        Debug.DebugMessageQueueForSaving.Clear();
                        //TOTO: Replace with error
                        Debug.LogMsg.AddAppEvent("Debug Message Queue overflow.", "Limit: " +
                            messageOverflowLimit.ToString() + ". Items found and removed: " + numbMsgInQue.ToString(), Level.Major);
                        numbMsgInQue = Debug.DebugMessageQueueForSaving.Count;
                    }
                    if (numbMsgInQue > 0)
                    {
                        for (int i = 0; i < numbMsgInQue; i++)
                        {
                            debugFile.AddMessage(DebugMessageQueueForSaving.Dequeue());
                        }
                        // TODO Figure out how to add the whole array at a time, sithout error.
                        // TODO Figure out passing a Queue ref
                    }
                    Thread.Sleep(messageLogBreakTimeMs); //
                }
            }
            // private methods.
            static public void AddMessageCore(MessageType type, string message, string details, Level level,
                string memberName, string sourceFilePath, int sourceLineNumber)
            {
                // Only put in Q if level higher or equal to teh system debug level.
                if (level >= debugLevel)
                {
                    Debug.DebugMessageQueueForSaving.Enqueue(new DebugMessageCollected(type, level, DateTime.Now, message, details,
                        Path.GetFileName(sourceFilePath), sourceLineNumber, memberName));
                }
            }
        }
        static public class LogTraffic
        {
            private static readonly Queue<TrafficMsgCollected> TrafficMessageQueueForSaving = new Queue<TrafficMsgCollected>();

            static private void init()
            {
                // Starting the traffic message log thread.
                Thread trafficThread = new Thread(new ThreadStart(TrafficMsgLogThread));
                trafficThread.Start();
                trafficThread.IsBackground = true;
            }
            struct TrafficMsgCollected : IcvtStruckt
            {
                DateTime timestamp;
                string trafficType;
                byte[] rawData;
                string sourceInterface;
                LogTraffic.Direction direction;
                TrafficMsgDecoder decodeMessage;

                public TrafficMsgCollected(string trafficType, byte[] rawData, DateTime timestamp, string sourceInterface = "",
                    LogTraffic.Direction direction = LogTraffic.Direction.Unknown, TrafficMsgDecoder decodeMessage = null)
                {
                    /*
                    if (timestamp == null)
                    {
                        this.timestamp = DateTime.Now;
                    }
                    else
                    {
                        this.timestamp = (DateTime)timestamp;
                    }*/
                    this.timestamp = timestamp;
                    this.trafficType = trafficType;
                    this.rawData = rawData;
                    this.sourceInterface = sourceInterface;
                    this.direction = direction;
                    this.decodeMessage = decodeMessage;
                }
                public string GetCVTFileHeader()
                {
                    return "Time\tType\tInterface\tDirection\tRaw\tValidity\tSource\tTarget\tCommand\tDetails";
                }
                public int GetCVTNumbCols()
                {
                    return 10;
                }
                public string GetCVTFileString()
                {

                    string sourceAdr = "";
                    string targetAdr = "";
                    string command = "";
                    string detail = "";
                    string checkSum = "";
                    LogTraffic.Validity valid = LogTraffic.Validity.Unknown;
                    StringBuilder arraStr = new StringBuilder(this.timestamp.ToString(TimeFormat.standardDateTimeFormatWithMs) + "\t");
                    arraStr.Append(this.sourceInterface + "\t");
                    arraStr.Append(this.direction.ToString() + "\t");
                    foreach (var item in this.rawData)
                    {
                        arraStr.Append(item.ToString("X2") + " ");
                    }
                    if (this.decodeMessage != null)
                    {
                        this.decodeMessage(rawData, direction, out valid, out sourceAdr, out targetAdr, out command, out detail, out checkSum);
                    }
                    arraStr.Append(valid.ToString() + "\t");
                    arraStr.Append(sourceAdr + "\t");
                    arraStr.Append(targetAdr + "\t");
                    arraStr.Append(command + "\t");
                    arraStr.Append(detail + "\t");
                    arraStr.Append(checkSum + "\t");

                    return arraStr.ToString();
                }
            }
            public enum Validity { Unknown, Invalid, Valid }
            public enum Direction { In, Out, Up, Down, A, B, Unknown };  //Direction should be an enum, as decoder might need the information

            public delegate void TrafficMsgDecoder(byte[] rawData, Debug.LogTraffic.Direction direction, out Debug.LogTraffic.Validity valid, out string sourceAdr,
                out string targetAdr, out string command, out string detail, out string checkSum);

            static public void Add(string trafficType, byte[] rawData, TrafficMsgDecoder decodeMessage = null, string sourceInterface = "", Debug.LogTraffic.Direction direction = Debug.LogTraffic.Direction.Unknown,
                  DateTime? timestamp = null)
            {
                DateTime lokaltimestamp;
                if (debugLevel == Level.Dev)
                {
                    if (timestamp == null)
                    {
                        lokaltimestamp = DateTime.Now;
                    }
                    else
                    {
                        lokaltimestamp = (DateTime)timestamp;
                    }
                    Debug.LogTraffic.TrafficMessageQueueForSaving.Enqueue(new TrafficMsgCollected(trafficType, rawData, lokaltimestamp, sourceInterface,
                        direction, decodeMessage));
                }
            }
            static private void TrafficMsgLogThread()
            {
                if (debugLevel == Level.Dev)
                {
                    PerpetualCVTLogFile trafficFile = new PerpetualCVTLogFile(PostScript: "Debug Log Traffic", filType: FileType.Debug);
                    while (true)
                    {
                        int numbMsgInQue = Debug.LogTraffic.TrafficMessageQueueForSaving.Count;
                        if (numbMsgInQue >= messageOverflowLimit)
                        {
                            //Empty the Q if completly full. Add error.
                            Debug.LogTraffic.TrafficMessageQueueForSaving.Clear();
                            Debug.LogMsg.AddAppEvent("Traffic Queue overflow.", "Limit: " +
                                messageOverflowLimit.ToString() + ". Items found and removed: " + numbMsgInQue.ToString(), Level.Major);
                            numbMsgInQue = Debug.LogTraffic.TrafficMessageQueueForSaving.Count;
                        }
                        if (numbMsgInQue > 0)
                        {
                            for (int i = 0; i < numbMsgInQue; i++)
                            {
                                trafficFile.AddMessage(TrafficMessageQueueForSaving.Dequeue());
                            }
                        }
                        Thread.Sleep(trafficLogBreakTimeMs); //
                    }
                }
            }
            /* Custom decode function excample
            //Make the function:
            public static void TestMsgDecoder(byte[] rawData, Debug.LogTraffic.Direction direction, out Debug.LogTraffic.Validity valid, out string sourceAdr, out string targetAdr, out string command, out string detail, out string checkSum)
            {
                targetAdr = "0x" + rawData[0].ToString("X2");
                sourceAdr = "0x" + rawData[1].ToString("X2");
                command = "0x" + rawData[2].ToString("X2") + "Test Command";
                checkSum = "0x" + rawData[7].ToString("X2") + rawData[8].ToString("X2");
                detail = "0x" + rawData[3].ToString("X2") + rawData[4].ToString("X2") + rawData[5].ToString("X2") + rawData[6].ToString("X2");
                valid = Debug.LogTraffic.Validity.Valid;
            }
            //Assigne to typed variable
            Debug.LogTraffic.TrafficMsgDecoder testDekoder = TestMsgDecoder;
            //Use
            Debug.LogTraffic.Add("Test", rawData, testDekoder, "Main RS485", Debug.LogTraffic.Direction.In);
            */
        }
        static public class SystemInfo
        {
            //Sys info
            public delegate void SystemInfoIsRetreivedHandler(object sender, EventArgs e);
            private static Boolean isRetrived = false;
            private static string systemInformation = "System information not retreived. Application not running for long enough.";
            public static event SystemInfoIsRetreivedHandler SystemInfoIsRetreived;
            static public Boolean GetIsRetreived()
            {
                return isRetrived;
            }
            static public string Get()   // the Name property
            {
                return systemInformation;
            }
            static private void init()
            {
                // Starting the retreive system info thread.
                Thread systemInfoThread = new Thread(new ThreadStart(SystemInfoRetreivalThread));
                systemInfoThread.IsBackground = true;//true;
                systemInfoThread.Start();
            }
            static private void SystemInfoRetreivalThread()
            {
                systemInformation = RetreiveSystemInformation();
                Debug.LogMsg.AddAppEvent("System Information Gathered", systemInformation, Level.Normal);
                isRetrived = true;
                if (SystemInfo.SystemInfoIsRetreived != null)
                    SystemInfo.SystemInfoIsRetreived(null, EventArgs.Empty);
            }
            static private string RetreiveSystemInformation()
            {
                try
                {
                    //#TODO: Error catch. Do not wan tthis to be able to block a program. If error, just replca text withe error getting system info
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
                    sb.AppendLine("AP App: " + ExtensionsAssembly.GetFullID());
                    sb.AppendLine("AP App location: " + ExtensionsAssembly.GetPath());
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
                    Debug.LogMsg.AddHandledExeption("System information not retreived. Failure during retreival", e);
                    return ("System information not retreived. Failure during retreival. Error: " + e.Message);
                }
            }
        }
        #region "System hooks"
        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Debug.LogMsg.AddFatalUnhandledExeption(e);
            // Allow time to write
            Thread.Sleep(2);
            // #TODO Gracefull exit. Connect to debug thread, make sure message is written to file
            Process.GetCurrentProcess().Kill();
        }
        #endregion
    }
}
