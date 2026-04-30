using Microsoft.Win32;
using System.Text;

namespace MonitorBrightnessAutoAdjust
{
    /// <summary>
    /// Registry-based auto-start utility for Windows.
    /// </summary>
    public class AutoStartUtil
    {
        public static string StartupPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetExePath()
        {
            return Environment.ProcessPath ?? string.Empty;
        }

        public static string AppendQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return $"\"{value}\"";
        }

        public static string GetMD5(string str)
        {
            byte[] byteOld = Encoding.UTF8.GetBytes(str);
            byte[] byteNew = System.Security.Cryptography.MD5.HashData(byteOld);
            StringBuilder sb = new(32);
            foreach (byte b in byteNew)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Check if the application is set to auto-start.
        /// </summary>
        public static bool IsAutoRun(string autoRunRegPath, string autoRunName)
        {
            try
            {
                var autoRunNameKey = $"{autoRunName}_{GetMD5(StartupPath())}";
                var readValue = RegReadValue(autoRunRegPath, autoRunNameKey, "");
                return !string.IsNullOrEmpty(readValue);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Enable or disable auto-start via registry.
        /// </summary>
        public static void SetAutoRun(string autoRunRegPath, string autoRunName, bool run)
        {
            try
            {
                var autoRunNameKey = $"{autoRunName}_{GetMD5(StartupPath())}";

                // Delete first
                RegWriteValue(autoRunRegPath, autoRunNameKey, "");

                if (run)
                {
                    string exePath = GetExePath();
                    RegWriteValue(autoRunRegPath, autoRunNameKey, AppendQuotes(exePath));
                }
            }
            catch
            {
                // Silently fail for SetAutoRun
            }
        }

        public static string? RegReadValue(string path, string name, string def)
        {
            RegistryKey? regKey = null;
            try
            {
                regKey = Registry.CurrentUser.OpenSubKey(path, false);
                string? value = regKey?.GetValue(name) as string;
                if (string.IsNullOrEmpty(value))
                {
                    return def;
                }
                else
                {
                    return value;
                }
            }
            catch
            {
                return def;
            }
            finally
            {
                regKey?.Close();
            }
        }

        public static void RegWriteValue(string path, string name, object value)
        {
            RegistryKey? regKey = null;
            try
            {
                regKey = Registry.CurrentUser.CreateSubKey(path);
                if (string.IsNullOrEmpty(value.ToString()))
                {
                    regKey?.DeleteValue(name, false);
                }
                else
                {
                    regKey?.SetValue(name, value);
                }
            }
            catch
            {
                // Silently fail for RegWriteValue
            }
            finally
            {
                regKey?.Close();
            }
        }
    }
}
