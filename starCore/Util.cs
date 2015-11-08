using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Globalization;
using System.Linq.Expressions;
using System.Drawing;
using Steinsvik.Star;

namespace Steinsvik.Star
{
    public static class Time
    {
        public static string compactTimeFormat { get; set; } = "HHmmss";
        public static string compactDateFormat { get; set; } = "yyyyMMdd";
        public static string compactDateTimeFormat { get; set; } = compactDateFormat + " " + compactTimeFormat;
        public static string standardDateTimeFormat { get; set; } = "yyyy.MM.dd HH:mm:ss";
        public static string standardDateTimeFormatWithMs { get; set; } = "yyyy.MM.dd HH:mm:ss.fff";
        public static string fileNameDateTimeFormat { get; set; } = compactDateTimeFormat;
        
        public static int WaitForInterval(int intervalMS, DateTime startTimestamp)
        {
            int actualWaitRequestInterval = (((DateTime.Now - startTimestamp).Milliseconds / intervalMS) + 1) * intervalMS -
                ((DateTime.Now - startTimestamp).Milliseconds);
            Thread.Sleep(actualWaitRequestInterval);
            return actualWaitRequestInterval;
        }
    }

    public static class App
    {
        public static bool IsAnotherAppInstanceRunning()
        {
            return (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1);
        }

        public static Assembly GetEntryAssembly()
        {
            return Assembly.GetEntryAssembly();
        }

        public static Assembly GetAssembly(Type type = null)
        {
            if (type == null)
                return Assembly.GetCallingAssembly();
            else
                return Assembly.GetAssembly(type);
        }

        public static string GetsuggestedMainHeaderA()
        {
            return GetEntryAssembly().GetName() + " " + GetEntryAssembly().GetInformationalVersion();
        }

        public static string GetApplicationFullID()
        {
            return GetEntryAssembly().GetName() + " " + GetEntryAssembly().GetFullID(false, false);
        }

        public static Assembly[] GetAllAssemblies(Boolean includeEntryAssembly = false, Boolean includeMicrosoftAssemblies = false)
        {
            Assembly[] assems = AppDomain.CurrentDomain.GetAssemblies();
            //var assemsResult = new List<Assembly>();
            Assembly execAssembly = Assembly.GetEntryAssembly();
            var listOfAssemblies = new List<Assembly>();
            //StringBuilder sb = new StringBuilder();
            foreach (Assembly assem in assems)
            {
                if ((includeEntryAssembly == false) & (assem == execAssembly))
                { }
                else if ((includeMicrosoftAssemblies == false) & (ExtensionsAssembly.GetCompany(assem) == "Microsoft Corporation"))
                { }
                else
                {
                    listOfAssemblies.Add(assem);
                }
            }
            return listOfAssemblies.ToArray();
        }

        public static string[] GetAllAssembliesStringArray(Boolean includeEntryAssembly = false, Boolean includeMicrosoftAssemblies = false, Boolean includeCopyright = false)
        {
            Assembly[] assems = GetAllAssemblies(includeEntryAssembly, includeMicrosoftAssemblies); //AppDomain.CurrentDomain.GetAssemblies();
            var listOfAssemblies = new List<string>();
            //StringBuilder sb = new StringBuilder();
            foreach (Assembly assem in assems)
            {
                if (includeCopyright)
                {
                    listOfAssemblies.Add(ExtensionsAssembly.GetFullID(assem) + "\t" + ExtensionsAssembly.GetCopyright(assem)); //+ Environment.NewLine + "\t" +
                }
                else
                {
                    listOfAssemblies.Add(ExtensionsAssembly.GetFullID(assem));
                }
            }
            return listOfAssemblies.ToArray();
        }

        public static Assembly This
        {
            get { return GetEntryAssembly(); }
        }

        public static string DefaultAppDataPath(DefaultAppPathType type = App.DefaultAppPathType.Common, string subFolder = "")
        {
            string comp = (App.GetEntryAssembly().GetCompany() == "") ? "NSE" : App.GetEntryAssembly().GetCompany();
            string appName = (App.GetEntryAssembly().GetApplicationName() == "") ? "NSE test app" : App.GetEntryAssembly().GetApplicationName();
            string sub = (subFolder == "") ? "" : (subFolder + @"\");
            string core;
            switch (type)
            {
                case DefaultAppPathType.Common:
                    core = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    break;
                case DefaultAppPathType.User:
                    core = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    break;
                case DefaultAppPathType.RoamingUser:
                    core = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    break;
                default:
                    core = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    break;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\", comp + @"\" + appName + @"\" + sub);
        }

        public enum DefaultAppPathType
        {
            Common,
            User,
            RoamingUser
        }
    }

    public static class Util
    {
        /// <summary> Get name of a member. </summary>
        /// <excample> string nameOfParam1 = Util.GetMemberName(() => param1); </excample>
        /// <remarks>
        /// Source: http://stackoverflow.com/questions/9801624/get-name-of-a-variable-or-parameter 
        /// </remarks>
        public static string GetMemberName<T>(Expression<Func<T>> memberExpression)
        {
            MemberExpression expressionBody = (MemberExpression)memberExpression.Body;
            return expressionBody.Member.Name;
        }
    }

    public static class ExtensionsGeneric
    {
        static public bool IsNullable<T>(this T obj)
        {
            if (obj == null) return true; // obvious
            Type type = typeof(T);
            if (!type.IsValueType) return true; // ref-type
            if (Nullable.GetUnderlyingType(type) != null) return true; // Nullable<T>
            return false; // value-type
        }
    }

    public static class ExtensionsListAndArray
    {
        public static string ToDelimetedString<T>(this List<T> listToPrint, string delimiter = "\t")
        {
            var sb = new StringBuilder();
            foreach (var item in listToPrint)
            {
                sb.Append(item.ToString() + delimiter);
            }
            return sb.ToString();
        }

        public static string ToDelimetedString<T>(this T[] listToPrint, string delimiter = "\t")
        {
            var sb = new StringBuilder();
            foreach (var item in listToPrint)
            {
                sb.Append(item.ToString() + delimiter);
            }
            return sb.ToString();
        }

        public static string ToHexBytesString(this char[] data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var b in data)
            {
                sb.Append(" ").Append(((byte)b).ToString("X2")).Append(" ");
            }
            return sb.ToString();
        }

        public static string ToHexBytesString(this byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var b in data)
            {
                sb.Append(" ").Append(b.ToString("X2")).Append(" ");
            }
            return sb.ToString();
        }
    }

    public static class ExtensionsString
    {
        public static string ToHexBytesString(this string data)
        {
            StringBuilder sb = new StringBuilder();
            if (data != null)
            {
                char[] tempData = data.ToCharArray();
                foreach (var b in tempData)
                {
                    sb.Append(" ").Append(((byte)b).ToString("X2")).Append(" ");
                }
            }
            return sb.ToString();
        }

        public static UInt32? UInt32FromHexOrDec(this string text)
        {
            UInt32 parsedHolder;
            string testText = text;
            NumberStyles style = NumberStyles.Integer;
            if (text.StartsWith("0x") | text.StartsWith("0X"))
            {
                testText = text.Substring(2, text.Length - 2);
                style = NumberStyles.HexNumber;
            }
            if (UInt32.TryParse(testText, style, CultureInfo.InvariantCulture, out parsedHolder))
            {
                return parsedHolder;
            }
            return null;
        }

        public static string GetDelimitedValueAfterLeader(this string fileContent, string Leader, string delimiter = "\t")
        {
            try
            {
                int startIndex = fileContent.IndexOf(Leader + delimiter) + (Leader + delimiter).Length;
                int endIndexAlt1 = fileContent.IndexOf(Environment.NewLine, startIndex);
                int endIndexAlt2 = fileContent.IndexOf("\t", startIndex);
                int endIndex = (endIndexAlt1 < endIndexAlt2) ? endIndexAlt1 : endIndexAlt2;
                return fileContent.Substring(startIndex, endIndex - startIndex).Trim();
            }
            catch
            {
                return "";
            }

        }

        public static List<string> GetDelimitedValuesAfterLeader(this string fileContent, string Leader, string delimiter = "\t", int skipNumb = 0)
        {
            var listen = new List<string>();
            int startIndex = fileContent.IndexOf(Leader + delimiter) + (Leader + delimiter).Length;
            int endIndexFinal = fileContent.IndexOf(Environment.NewLine, startIndex);
            int indexTab = 0;
            int skipcounter = skipNumb;

            endIndexFinal = (endIndexFinal == -1) ? fileContent.Length - 1 : endIndexFinal;
            while (true)
            {
                skipcounter--;
                indexTab = fileContent.IndexOf(delimiter, startIndex + 1);
                if ((endIndexFinal > indexTab) & (indexTab != -1))
                {

                    string cell = fileContent.Substring(startIndex, indexTab - startIndex + 1).Trim();
                    if ((cell != "") & (skipcounter < 0))
                        listen.Add(cell);
                    startIndex = indexTab;
                }
                else
                {
                    string cell = fileContent.Substring(startIndex, endIndexFinal - startIndex + 1).Trim();
                    if ((cell != "") & (skipcounter < 0))
                        listen.Add(cell);
                    return listen;
                }
            }
        }

        public static string GetValidEndOfLineFromStringContent(this string asciiFileContent)
        {
            int nCount = asciiFileContent.Count(f => f == '\n');
            int rCount = asciiFileContent.Count(f => f == '\r');
            if ((nCount == 0) & (rCount == 0))
                return Environment.NewLine;
            if (rCount > nCount * 2)
                return "\r";
            else if (nCount > rCount * 2)
                return "\n";
            else
                return "\r\n";
        }

        public static bool IsValidFolderPath(this string path)
        {
            try
            {
                return Path.IsPathRooted(path);
            }
            catch
            {
                return false;
            }
        }
    }

    public static class ExtensionsInvoke
    {
        public static void SynchronizedInvoke(this ISynchronizeInvoke sync, Action action)
        {
            // If the invoke is not required, then invoke here and get out.
            if (!sync.InvokeRequired)
            {
                // Execute action.
                action();
                // Get out.
                return;
            }
            // Marshal to the required context.
            sync.Invoke(action, new object[] { });
            //sync.BeginInvoke(action, new object[] { });
        }

        public static void SInvoke(this ISynchronizeInvoke sync, Action action)
        {
            // If the invoke is not required, then invoke here and get out.
            if (!sync.InvokeRequired)
            {
                // Execute action.
                action();
                // Get out.
                return;
            }
            // Marshal to the required context.
            sync.Invoke(action, new object[] { });
            //sync.BeginInvoke(action, new object[] { });
        }

        public static void BInvoke(this ISynchronizeInvoke sync, Action action)
        {
            // If the invoke is not required, then invoke here and get out.
            if (!sync.InvokeRequired)
            {
                // Execute action.
                action();
                // Get out.
                return;
            }
            // Marshal to the required context.
            //sync.Invoke(action, new object[] { });
            sync.BeginInvoke(action, new object[] { });
        }
    }

    public static class ExtensionsAssembly
    {
        public static string GetFullID(this Assembly refIn, Boolean includeBuildTime = false, Boolean includeByCompany = true)
        {
            DateTime BuildDateTime;
            Version Version = GetVersion(refIn);
            string Name = GetApplicationName(refIn);
            string Company = GetCompany(refIn);
            string ConfigInfo = GetConfiguration(refIn);
            string company_text = "";
            string config_text = "";
            if (Company != "")
            {
                company_text = " By " + Company;
            }
            if (ConfigInfo != "")
            {
                config_text = " " + ConfigInfo;
            }
            string filePath = refIn.Location; //System.Reflection.Assembly.GetCallingAssembly().Location;
            BuildDateTime = GetBuildTime(filePath);

            return (Name + " " + Version.ToString() + config_text + (includeBuildTime ? (" Build time: " + BuildDateTime.ToString("yyyyMMdd")) : "") + (includeByCompany ? ("." + company_text) : ""));
        }

        public static string GetNameAndShortVersion(this Assembly refIn)
        {
            return GetApplicationName(refIn) + " " + GetVersion(refIn).ToString(2);
        }

        public static string GetCompany(this Assembly refIn)
        {
            refIn = refIn ?? Assembly.GetEntryAssembly();
            string company = "";
            object[] attribs = refIn.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
            if (attribs.Length > 0)
            {
                company = ((AssemblyCompanyAttribute)attribs[0]).Company;
            }
            return company;
        }

        public static string GetCopyright(this Assembly refIn)
        {
            string copyright = "";
            object[] attribs = refIn.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            if (attribs.Length > 0)
            {
                copyright = ((AssemblyCopyrightAttribute)attribs[0]).Copyright;
            }
            return copyright;
        }

        public static string GetConfiguration(this Assembly refIn)
        {
            string config = "";
            object[] attribs = refIn.GetCustomAttributes(typeof(AssemblyConfigurationAttribute), true);
            if (attribs.Length > 0)
            {
                config = ((AssemblyConfigurationAttribute)attribs[0]).Configuration;
            }
            return config;
        }

        public static string GetDescription(this Assembly refIn)
        {
            string description = "";
            object[] attribs = refIn.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), true);
            if (attribs.Length > 0)
            {
                description = ((AssemblyDescriptionAttribute)attribs[0]).Description;
            }
            return description;
        }

        public static string GetApplicationName(this Assembly refIn)
        {
            return refIn.GetName().Name;
        }

        public static bool InvalidBuildNumber(this Assembly refIn)
        {
            Version versjonen = GetVersion(refIn);
            return ((versjonen.Revision >= 90000000) | (versjonen.Revision == 0));
        }

        public static Version GetVersion(this Assembly refIn)
        {
            try
            {
                Version fileVersion = new Version(System.Diagnostics.FileVersionInfo.GetVersionInfo(refIn.Location).FileVer‌​sion);

                return fileVersion;
            }
            catch
            {
                return refIn.GetName().Version;
            }
            //return refIn.GetName().Version;  //Assembly version

            // Assembly file version

        }

        public static string GetInformationalVersion(this Assembly refIn)
        {
            try
            {
                return System.Diagnostics.FileVersionInfo.GetVersionInfo(refIn.Location).ProductVersion;

            }
            catch
            {
                return "Version unavailable";
            }
            //return refIn.GetName().Version;  //Assembly version

            // Assembly file version

        }

        public static DateTime GetBuildTime(this Assembly refIn)
        {
            string filePath = refIn.Location;
            return GetBuildTime(filePath);
        }

        public static DateTime GetBuildTime(string filePath)
        {
            //Ser ut som om denne bare kalles en gang, selv om bruker App mye
            //By Jeff Atwood. Via Stack overflow

            const int c_PeHeaderOffset = 60;
            const int c_LinkerTimestampOffset = 8;
            byte[] b = new byte[2048];
            System.IO.Stream s = null;
            try
            {
                s = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
                s.Read(b, 0, 2048);
            }
            finally
            {
                if (s != null)
                {
                    s.Close();
                }
            }
            int i = System.BitConverter.ToInt32(b, c_PeHeaderOffset);
            int secondsSince1970 = System.BitConverter.ToInt32(b, i + c_LinkerTimestampOffset);
            DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dt = dt.AddSeconds(secondsSince1970);
            dt = dt.ToLocalTime();
            return dt;
        }

        public static string GetPath(this Assembly refIn)
        {
            return refIn.Location;
        }

        public static string GetPathWithoutFilename(this Assembly refIn)
        {
            return Path.GetDirectoryName(refIn.Location);
        }

        public static string GetDriveLetter(this Assembly refIn)
        {
            return Path.GetPathRoot(refIn.Location).Remove(1);
        }
    }
}