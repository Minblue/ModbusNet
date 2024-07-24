using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempModbusNet.Services
{
    public class LocalFileLogger : ILogger
    {
        private readonly string categoryName;
        private readonly static object RetryUploadLock = new();
        private readonly static object fileSyncLock = new();
        private readonly string basePath;

        public delegate void DelegateWriteLogInfo(string filePath, string content);
        public event DelegateWriteLogInfo ProcessEvent;//声明一个日志事件

        public LocalFileLogger(string categoryName)
        {
            this.categoryName = categoryName;

            basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Logs/";

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }

            this.ProcessEvent += WriteLogFile;

        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return default!;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel != LogLevel.None)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                if (state != null && state.ToString() != null)
                {
                    var logContent = state.ToString();

                    if (logContent != null)
                    {
                        if (exception != null)
                        {
                            var logMsg = new
                            {
                                message = logContent,
                                error = new
                                {
                                    exception?.Source,
                                    exception?.Message,
                                    exception?.StackTrace
                                }
                            };

                            logContent = JsonConvert.SerializeObject(logMsg);
                        }

                        var log = new
                        {
                            CreateTime = DateTime.Now,
                            Category = categoryName,
                            Level = logLevel.ToString(),
                            Content = logContent
                        };

                        string logStr = JsonConvert.SerializeObject(log);
                        string fileName = "Log" + DateTime.Now.ToString("yyyyMMdd") + ".log";
                        ProcessEvent(fileName, logStr);
                        //WriteLog(logStr);
                    }
                }
            }
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="content"></param>
        public static void WriteLog(string content)
        {
            string basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Logs/";

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }

            lock (fileSyncLock)
            {
                try
                {
                    var logPath = basePath + "Log" + DateTime.Now.ToString("yyyyMMdd") + ".log";
                    File.AppendAllText(logPath, content + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    var logErrPath = basePath + "Err" + DateTime.Now.ToString("yyyyMMdd") + ".log";
                    File.AppendAllText(logErrPath, content + "\n" + ex.Message + "\n" + ex.StackTrace + Environment.NewLine, Encoding.UTF8);
                }
            }
        }

        /// <summary>
        /// 写入待上传数据
        /// </summary>
        /// <param name="content"></param>
        public static void WriteUploadSrcLog(string content)
        {
            string basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Uploads/src/";

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }
            var logPath = basePath + "Info" +DateTime.Now.ToString("yyyyMMdd") + "_src.log";
            lock (RetryUploadLock)
            {
                File.AppendAllText(logPath, content + Environment.NewLine, Encoding.UTF8);
            }
        }

        /// <summary>
        /// 写入已上传日志
        /// </summary>
        /// <param name="content"></param>
        public static void WriteUploadBakLog(string content)
        {
            string basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Uploads/bak/";

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }
            var logPath = basePath + "Info" + DateTime.Now.ToString("yyyyMMdd") + "_bak.log";
            File.AppendAllText(logPath, DateTime.Now.ToString()+Environment.NewLine+content + Environment.NewLine, Encoding.UTF8);
        }


        /// <summary>
        /// 读取待上传日志
        /// </summary>
        /// <returns></returns>
        public static List<string> ReadUploadLog()
        {
            string basePath = Directory.GetCurrentDirectory().Replace("\\", "/") + "/Uploads/src/";

            if (Directory.Exists(basePath) == false)
            {
                Directory.CreateDirectory(basePath);
            }
            List<string> uploadData = new();
            lock (RetryUploadLock)
            {
                string[] files = Directory.GetFiles(basePath);
                if (files == null || files.Length == 0)
                {
                    return uploadData;
                }
                foreach (string file in files)
                {
                    if (File.Exists(file))
                    {
                        uploadData.AddRange(File.ReadAllLines(file, Encoding.UTF8));
                        File.Delete(file);
                    }
                }
            }
            return uploadData;
        }

        void WriteLogFile(string fileName, string content)
        {
            lock (fileSyncLock)
            {
                var logPath = basePath + fileName;
                File.AppendAllText(logPath, content + Environment.NewLine, Encoding.UTF8);
            }
        }
    }
}
