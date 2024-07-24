using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Timers;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using System.Net.Http;
using TempModbusNet.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TempModbusNet.Services;
using TempModbusNet.Helper;
using TempModbusNet.Extensions;
using System.Text;
using System.IO;
using Polly.Extensions.Http;
using Polly.Timeout;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection.Metadata;
using Microsoft.VisualBasic;
using System.Threading;

namespace TempModbusNet
{
    class Program
    {
        //定义委托事件
        public delegate bool ControlCtrlDelegate(int CtrlType);
        public static event ControlCtrlDelegate ControlCtrl;

        //获取窗口句柄
        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        extern static IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll", EntryPoint = "GetSystemMenu")]
        extern static IntPtr GetSystemMenu(IntPtr hWnd, IntPtr bRevert);
        [DllImport("user32.dll", EntryPoint = "RemoveMenu")]
        extern static IntPtr RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);
        //关闭控制台 快速编辑模式、插入模式
        const int STD_INPUT_HANDLE = -10;
        const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        const uint ENABLE_INSERT_MODE = 0x0020;
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int hConsoleHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint mode);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint mode);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);

        static void Main(string[] args)
        {
            try
            {
                DisbleQuickEditMode();
                //DisbleClosebtn();
                Console.CancelKeyPress += new ConsoleCancelEventHandler(ConsoleCloseHandler);

                //重试机制
                var retryPolicy = HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .Or<TimeoutRejectedException>() // 若超时则抛出此异常
                    .WaitAndRetryAsync(new[]
                        {
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromSeconds(5),
                            TimeSpan.FromSeconds(10),
                            TimeSpan.FromSeconds(15),
                            TimeSpan.FromSeconds(20)
                        });
                // 为每个重试定义超时策略
                var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(10);
                HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
                builder.Configuration.AddIniFile("./Config/config.ini");
                string baseUri = builder.Configuration["IP:Server"];
                builder.Logging.AddConsole().AddSimpleConsole(options => options.IncludeScopes = true);
                builder.Logging.AddLocalFileLogger(options => { options.SaveDays = 7; });
                builder.Services.AddHttpClient();
                builder.Services.AddHostedService<TemperatureService>();//注册服务
                builder.Services.AddHttpClient("RetryClient", httpclient =>
                {
                    httpclient.BaseAddress = new Uri($"http://{baseUri}");
                    httpclient.Timeout = TimeSpan.FromSeconds(60); // 默认超时时间
                })
                .AddPolicyHandler(retryPolicy)
                .AddPolicyHandler(timeoutPolicy);
                var app1 = builder.Build();
                app1.Run();

            }
            catch (Exception ex)
            {
                string logStr = "主程序发生异常："+ ex.Message + "\n" + ex.StackTrace;
                LocalFileLogger.WriteLog(logStr);
            }
        }

        //关闭控制台程序的快速编辑模式，否则会出现点击界面，程序将会变成阻塞状态，不按回车无法继续运行
        //void CloseQuickEditMode()
        //{
        //    //原文链接：https://blog.csdn.net/Guqing_f/article/details/135127189
        //    HANDLE hStdin = GetStdHandle(STD_INPUT_HANDLE); // 获取句柄
        //    DWORD mode;
        //    GetConsoleMode(hStdin, &mode); //获取当前控制台模式
        //    mode &= ~ENABLE_QUICK_EDIT_MODE;  //移除快速编辑模式
        //    SetConsoleMode(hStdin, mode); //设置修改后的控制台模式
        //}

        static void DisbleClosebtn()
        {
            //与控制台标题名一样的路径
            string fullPath = System.Environment.CurrentDirectory + "\\TempModbusNet.exe";
            IntPtr windowHandle = FindWindow(null, fullPath);
            IntPtr closeMenu = GetSystemMenu(windowHandle, IntPtr.Zero);
            uint SC_CLOSE = 0xF060;
            RemoveMenu(closeMenu, SC_CLOSE, 0x0);
        }

        #region 关闭控制台 快速编辑模式、插入模式
        

        public static bool HandlerRoutine(int CtrlType)
        {
            switch (CtrlType)
            {
                case 0:
                    Console.WriteLine("0程序被Ctrl+C关闭！"); //Ctrl+C关闭  
                                                        //相关代码执行
                    return true;
                case 2:
                    Console.WriteLine("2程序被控制台关闭按钮关闭");//按控制台关闭按钮关闭 
                                                       //相关代码执行
                    break;
            }
            return false;
        }

        /// <summary>
        /// 取消编辑事件
        /// </summary>
        public static void DisbleQuickEditMode()
        {
            //关闭控制台程序的快速编辑模式，否则会出现点击界面，程序将会变成阻塞状态，不按回车无法继续运行
            //原文链接：https://blog.csdn.net/Guqing_f/article/details/135127189
            IntPtr hStdin = GetStdHandle(STD_INPUT_HANDLE);//获取句柄
            uint mode;
            GetConsoleMode(hStdin, out mode);//获取当前控制台模式
            mode &= ~ENABLE_QUICK_EDIT_MODE;//移除快速编辑模式
            mode &= ~ENABLE_INSERT_MODE;      //移除插入模式
            SetConsoleMode(hStdin, mode);//设置修改后的控制台模式
        }

        /// <summary>
        /// 控制台关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected static void ConsoleCloseHandler(object sender, ConsoleCancelEventArgs args)
        {
            switch (args.SpecialKey)
            {
                case ConsoleSpecialKey.ControlC:
                    Console.WriteLine("0程序被Ctrl+C关闭！"); //Ctrl+C关闭
                    //相关代码执行
                    LocalFileLogger.WriteLog(DateTime.Now + "程序被Ctrl+C关闭！");
                    Thread.Sleep(500);//等待500ms让日志保存成功
                    break;
                case ConsoleSpecialKey.ControlBreak:
                    Console.WriteLine("2程序被控制台关闭按钮关闭");//按控制台关闭按钮关闭 
                    //相关代码执行
                    LocalFileLogger.WriteLog(DateTime.Now + "1程序被控制台关闭按钮关闭");
                    Thread.Sleep(500);//等待500ms让日志保存成功
                    break;
            }
        }
        #endregion
    }
}
