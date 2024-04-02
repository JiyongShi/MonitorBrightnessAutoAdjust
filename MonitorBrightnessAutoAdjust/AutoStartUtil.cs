using Microsoft.Win32;
//using Microsoft.Win32.TaskScheduler;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace MonitorBrightnessAutoAdjust
{
    /// <summary>
    /// from v2RayN Utils.cs, thanks 2dust
    /// https://github.com/2dust/v2rayN/blob/master/v2rayN/v2rayN/Common/Utils.cs
    /// </summary>
    public class AutoStartUtil
    {
        #region TempPath

        /// <summary>
        /// 获取启动了应用程序的可执行文件的路径
        /// </summary>
        /// <returns></returns>
        public static string GetPath(string fileName)
        {
            string startupPath = StartupPath();
            if (string.IsNullOrEmpty(fileName))
            {
                return startupPath;
            }
            return Path.Combine(startupPath, fileName);
        }

        /// <summary>
        /// 获取启动了应用程序的可执行文件的路径及文件名
        /// </summary>
        /// <returns></returns>
        public static string GetExePath()
        {
            return Environment.ProcessPath ?? string.Empty;
        }

        public static string StartupPath()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string GetTempPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiTemps");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string UnGzip(byte[] buf)
        {
            using MemoryStream sb = new();
            using GZipStream input = new(new MemoryStream(buf), CompressionMode.Decompress, false);
            input.CopyTo(sb);
            sb.Position = 0;
            return new StreamReader(sb, Encoding.UTF8).ReadToEnd();
        }

        public static string GetBackupPath(string filename)
        {
            string _tempPath = Path.Combine(StartupPath(), "guiBackups");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            return Path.Combine(_tempPath, filename);
        }

        public static string GetConfigPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiConfigs");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string GetBinPath(string filename, string? coreType = null)
        {
            string _tempPath = Path.Combine(StartupPath(), "bin");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (coreType != null)
            {
                _tempPath = Path.Combine(_tempPath, coreType.ToString()!);
                if (!Directory.Exists(_tempPath))
                {
                    Directory.CreateDirectory(_tempPath);
                }
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string GetLogPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiLogs");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        public static string GetFontsPath(string filename = "")
        {
            string _tempPath = Path.Combine(StartupPath(), "guiFonts");
            if (!Directory.Exists(_tempPath))
            {
                Directory.CreateDirectory(_tempPath);
            }
            if (string.IsNullOrEmpty(filename))
            {
                return _tempPath;
            }
            else
            {
                return Path.Combine(_tempPath, filename);
            }
        }

        #endregion TempPath

        #region 开机自动启动等

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
            byte[] byteNew = MD5.HashData(byteOld);
            StringBuilder sb = new(32);
            foreach (byte b in byteNew)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// IsAdministrator
        /// </summary>
        /// <returns></returns>
        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity current = WindowsIdentity.GetCurrent();
                WindowsPrincipal windowsPrincipal = new WindowsPrincipal(current);
                //WindowsBuiltInRole可以枚举出很多权限，例如系统用户、User、Guest等等
                return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                //Logging.SaveLog(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 是否开机自启动
        /// </summary>
        /// <param name="AutoRunRegPath"></param>
        /// <param name="AutoRunName"></param>
        /// <returns></returns>
        public static bool IsAutoRun(string AutoRunRegPath, string AutoRunName)
        {
            try
            {
                var autoRunName = $"{AutoRunName}_{GetMD5(StartupPath())}";

                //read reg
                var readValue = RegReadValue(AutoRunRegPath, autoRunName, "");

                return !string.IsNullOrEmpty(readValue);
            }
            catch (Exception ex)
            {
                //Logging.SaveLog(ex.Message, ex);
                return false;
            }
        }

        /// <summary>
        /// 开机自动启动
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
        public static void SetAutoRun(string AutoRunRegPath, string AutoRunName, bool run)
        {
            try
            {
                var autoRunName = $"{AutoRunName}_{GetMD5(StartupPath())}";

                //delete first
                RegWriteValue(AutoRunRegPath, autoRunName, "");
                //if (IsAdministrator())
                //{
                //    AutoStart(autoRunName, "", "");
                //}

                if (run)
                {
                    string exePath = GetExePath();
                    //if (IsAdministrator())
                    //{
                    //    AutoStart(autoRunName, exePath, "");
                    //}
                    //else
                    //{
                    RegWriteValue(AutoRunRegPath, autoRunName, AppendQuotes(exePath));
                    //}
                }
            }
            catch (Exception ex)
            {
                //Logging.SaveLog(ex.Message, ex);
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
            catch (Exception ex)
            {
                //Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
            return def;
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
            catch (Exception ex)
            {
                //Logging.SaveLog(ex.Message, ex);
            }
            finally
            {
                regKey?.Close();
            }
        }

        /*
        <PackageReference Include="TaskScheduler" Version="2.10.1" />   

        /// <summary>
        /// Auto Start via TaskService
        /// </summary>
        /// <param name="taskName"></param>
        /// <param name="fileName"></param>
        /// <param name="description"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AutoStart(string taskName, string fileName, string description)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                return;
            }
            string TaskName = taskName;
            var logonUser = WindowsIdentity.GetCurrent().Name;
            string taskDescription = description;
            string deamonFileName = fileName;

            using var taskService = new TaskService();
            var tasks = taskService.RootFolder.GetTasks(new Regex(TaskName));
            foreach (var t in tasks)
            {
                taskService.RootFolder.DeleteTask(t.Name);
            }
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            var task = taskService.NewTask();
            task.RegistrationInfo.Description = taskDescription;
            task.Settings.DisallowStartIfOnBatteries = false;
            task.Settings.StopIfGoingOnBatteries = false;
            task.Settings.RunOnlyIfIdle = false;
            task.Settings.IdleSettings.StopOnIdleEnd = false;
            task.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            task.Triggers.Add(new LogonTrigger { UserId = logonUser, Delay = TimeSpan.FromSeconds(10) });
            task.Principal.RunLevel = TaskRunLevel.Highest;
            task.Actions.Add(new ExecAction(AppendQuotes(deamonFileName), null, Path.GetDirectoryName(deamonFileName)));

            taskService.RootFolder.RegisterTaskDefinition(TaskName, task);
        }
        */

        #endregion 开机自动启动等

    }
}
