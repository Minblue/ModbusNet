using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TempModbusNet.Entity;
using TempModbusNet.Helper;
using TempModbusNet.Services;

namespace TempModbusNet.Service
{
    /// <summary>
    /// 温度读取数据
    /// </summary>
    internal class TemperatureService : IHostedService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly string _templateComName;
        private readonly string _bakeComName;
        private readonly System.Timers.Timer _timer = new();
        private readonly ModbusHelper _modbusHelper;
        private readonly HttpClientHelper _httpClientHelper;
        private readonly TemplateDataExchange _templateExchange;
        private static DateTime _lastUpdateTime = DateTime.Now;
        private static DateTime _retryUploadTime = DateTime.Now;

        public TemperatureService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TemperatureService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _templateComName = configuration["ComName:TempCom"];
            _bakeComName = configuration["ComName:BakeDoorCom"];
            _modbusHelper = new ModbusHelper(_templateComName, _bakeComName);
            _modbusHelper.Logger = _logger;
            _httpClientHelper = new HttpClientHelper(_httpClientFactory, _configuration, _logger);
            _templateExchange = new TemplateDataExchange(_httpClientHelper, _configuration, _logger);
        }

        /// <summary>
        /// 开始服务
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StartAsync Service...");
            _logger.LogInformation($"打开COM, TempCOM={_templateComName}, BakeCOM={_bakeComName}");
            _modbusHelper.CreateModbusRtuConnect();

            _timer.Interval = 10 * 1000;//10s读取一次
            _timer.Elapsed += OnTimer;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();

            OnTimer(null, null);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 结束服务
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StopAsync Service.");
            _modbusHelper.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 定时器执行事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnTimer(object sender, ElapsedEventArgs e)
        {
            if ((DateTime.Now - _lastUpdateTime).Minutes >= 10)
            {
                Console.Clear();//10min清除一次输出
                _lastUpdateTime = DateTime.Now;
            }
            try
            {
                Task<Tuple<ushort[], ushort[]>> task = _modbusHelper.ModbusSerialRtuMasterReadRegisters(sender, e);
                Tuple<ushort[], ushort[]> res = await task;
                if (task == null || res == null)
                {
                    _logger.LogError("读取Modbus数据为空");
                    return;
                }
                ushort[] tempValues = res.Item1;
                ushort[] bakeValues = res.Item2;
                #region 本地调试使用
                //本地调试使用
                //_logger.LogInformation("读取温度数据" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                //_logger.LogInformation("读取开关数据" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                //ushort[] tempValues = new ushort[2] { 252, 526 };
                //ushort[] bakeValues = new ushort[2] { 1, 1 };
                #endregion
                _templateExchange.ReceiveTemperatureData(tempValues, bakeValues);
            }
            catch (Exception ex)
            {
                _logger.LogError("读取Modbus数据异常:"+ ex.Message + "\n" + ex.StackTrace);
            }

            try
            {
                //5min重试一次上传
                if ((DateTime.Now - _retryUploadTime).Minutes >= 5)
                {
                    _retryUploadTime = DateTime.Now;
                    _templateExchange.RetryUploadTemperatureData();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("重试上传温度数据异常:" + ex.Message + "\n" + ex.StackTrace);
            }
        }

    }

}
