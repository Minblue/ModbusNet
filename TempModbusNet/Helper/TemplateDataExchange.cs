using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TempModbusNet.Entity;
using TempModbusNet.Extensions;
using TempModbusNet.Services;

namespace TempModbusNet.Helper
{
    class TemplateDataExchange
    {
        private HttpClientHelper _httpClientHelper;
        private IConfiguration _configuration;
        private ILogger _logger;
        private int[] bakeStates { get; set; } = new int[2] {0, 0 };
        private int[] currentBakes { get; set; } = new int[2] { 0, 0 };
        private string[] BatchIDs { get; set; } = new string[2];
        private readonly ConcurrentDictionary<string, string> deviceLocalData = new();//设备数据
        private static readonly Dictionary<string, DateTime> batchIdHistory = new();//批次历史记录
        private readonly string _baseUri;
        private readonly string _queryBatchUrl;
        private readonly string _uploadDataUrl;
        private readonly List<string> _bakeDoorNames = new();
        private static List<string> batchIdLocal = new();

        public TemplateDataExchange(
            HttpClientHelper httpClientHelper,
            IConfiguration configuration,
            ILogger logger)
        {
            _httpClientHelper = httpClientHelper;
            _configuration = configuration;
            _logger = logger;
            _baseUri = _configuration["IP:Server"];
            string lineName = _configuration["Graph:LineName"];
            _bakeDoorNames.AddRange(lineName.Split(","));
            //_queryBatchUrl = $"http://{_baseUri}/api/RunCardOverNew/GetRunCardBakeBatch";
            //_uploadDataUrl = $"http://{_baseUri}/api/RunCardOverNew/SaveRunCardOverBakeList";
            _queryBatchUrl = $"/api/RunCardOverNew/GetRunCardBakeBatch";
            _uploadDataUrl = $"/api/RunCardOverNew/SaveRunCardOverBakeList";
            deviceLocalData.TryAdd(_bakeDoorNames[0], _configuration["TemplateBatch:BatchNo1"]);
            deviceLocalData.TryAdd(_bakeDoorNames[1], _configuration["TemplateBatch:BatchNo2"]);
            currentBakes[0] = int.Parse(_configuration["CorrentParam:CorrectBake1"]);
            currentBakes[1] = int.Parse(_configuration["CorrentParam:CorrectBake2"]);

        }

        /// <summary>
        /// 上传数据
        /// </summary>
        /// <param name="template"></param>
        public async void UploadTemplateData(TemperatureDataEntity template)
        {
            if (template == null || string.IsNullOrEmpty(template.BatchID))
            {
                return;
            }
            string requestUrl = _uploadDataUrl + "?postData=" + template.ToJson();
            try
            {
                _logger.LogInformation("UploadTemplateData Begin：" + requestUrl);
                string strApiResult = await _httpClientHelper.HttpGetAsync(requestUrl);
                if (!string.IsNullOrEmpty(strApiResult))
                {
                    ApiResultEntity apiResult = JsonConvert.DeserializeObject<ApiResultEntity>(strApiResult);
                    if (apiResult != null)
                    {
                        if (apiResult.state != 1)
                        {
                            //备份到待上传目录
                            LocalFileLogger.WriteUploadSrcLog(template.ToJson());
                        }
                    }
                }
                _logger.LogInformation("UploadTemplateData End：" + strApiResult);
            }
            catch (Exception ex)
            {
                _logger.LogError("UploadTemplateData Error:"+ ex.Message + "\n" + ex.StackTrace);
                if (ex.Message.Contains("网络错误"))
                {
                    _logger.LogInformation("UploadTemplateData Save to bak file：" + template.ToJson());
                    //备份到待上传目录
                    LocalFileLogger.WriteUploadSrcLog(template.ToJson());
                }
            }
        }

        /// <summary>
        /// 获取烘烤批次数据
        /// </summary>
        /// <param name="queryTemplate"></param>
        /// <returns></returns>
        public async Task<string> QueryTemplateBatch(QueryTemplateEntity queryTemplate)
        {
            string requestUrl = _queryBatchUrl + "?postData=" + queryTemplate.ToJson();
            _logger.LogInformation("QueryTemplateBatch Begin：" + requestUrl);
            string apiResult = await _httpClientHelper.HttpGetAsync(requestUrl);
            _logger.LogInformation("QueryTemplateBatch End：" + apiResult);
            return apiResult;
        }

        public async Task<string> QueryTemplateBatchAndUploadDataAsync(string batchID, string equipID)
        {
            //这里可以用委托
            string responseData = await QueryTemplateBatch(new QueryTemplateEntity
            {
                BatchID = batchID,
                EquipID = equipID
            });
            if (!string.IsNullOrEmpty(responseData))
            {
                ApiResultEntity apiResult = JsonConvert.DeserializeObject<ApiResultEntity>(responseData);
                if (apiResult != null)
                {
                    if (apiResult.state == 1)
                    {
                        string[] resArray = apiResult.data.Split("|");
                        return resArray[0];
                    }
                }
            }
            return string.Empty;
        }

        public async void ReceiveTemperatureData(ushort[] tempDatas, ushort[] bakeDatas)
        {
            try
            {

                if (bakeDatas != null && bakeDatas.Length > 0)
                {
                    for (int bakeIndex = 0; bakeIndex < bakeDatas.Length; bakeIndex++)
                    {
                        int newDoorState = (int)bakeDatas[bakeIndex];//烤箱门状态-新
                        int oldDoorState = bakeStates[bakeIndex];//烤箱门状态-旧
                        int curCurrentValue = currentBakes[bakeIndex];//校准值
                        double curTemplateValue = ((double)tempDatas[bakeIndex] / 10).TruncatePre2() + curCurrentValue;//烤箱温度
                        string curDoorName = _bakeDoorNames[bakeIndex];//烤箱编号
                        string diviceBatchId = BatchIDs[bakeIndex];//烘烤批次
                        bool isClosed = newDoorState == (int)BakeDoorState.CLOSED;
                        if (newDoorState != oldDoorState)
                        {
                            bakeStates[bakeIndex] = newDoorState;
                        }
                        _logger.LogInformation($"烤箱:{curDoorName}{(isClosed ? "关闭" : "打开")}，温度:{curTemplateValue}");

                        #region 关闭运行
                        if (newDoorState == (int)BakeDoorState.CLOSED)
                        {
                            //有烘烤批次则进行上传温度数据
                            if (!string.IsNullOrEmpty(diviceBatchId))
                            {
                                int workState = (int)WorkState.WORK_RUNNING;
                                DateTime curDateTime = DateTime.Now;
                                if (batchIdHistory.ContainsKey(diviceBatchId))
                                {
                                    curDateTime = batchIdHistory[diviceBatchId];
                                }
                                //间隔3分钟上传一次
                                if (curDateTime.DiffSeconds(DateTime.Now) >= 180)
                                {
                                    _logger.LogInformation($"烤箱门:{curDoorName}关闭，上传温度数据中，BatchID={diviceBatchId}, DateTime={DateTime.Now::yyyy-MM-dd HH:mm:ss.fff}, TemperatureValue={curTemplateValue}");
                                    //上传数据，更新上传时间
                                    batchIdHistory[diviceBatchId] = DateTime.Now;
                                    UploadTemplateData(new TemperatureDataEntity
                                    {
                                        BatchID = diviceBatchId,
                                        TemperatureValue = curTemplateValue,
                                        UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                        WorkState = workState
                                    });
                                }
                            }
                            else
                            {
                                //获取批次为空，说明没有烘烤批次，可以获取批次数据
                                string newBatchID = await QueryTemplateBatchAndUploadDataAsync(diviceBatchId, curDoorName);
                                if (!string.IsNullOrEmpty(newBatchID))
                                {
                                    _logger.LogInformation($"烤箱门:{curDoorName}关闭，获取烘烤批次成功，开始上传温度数据，BatchID={newBatchID}, DateTime={DateTime.Now::yyyy-MM-dd HH:mm:ss.fff}, TemperatureValue={curTemplateValue}");
                                    UploadTemplateData(new TemperatureDataEntity
                                    {
                                        BatchID = newBatchID,
                                        TemperatureValue = curTemplateValue,
                                        UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                        WorkState = (int)WorkState.WORK_STARTING
                                    });
                                    BatchIDs[bakeIndex] = newBatchID;
                                    batchIdHistory.TryAdd(newBatchID, DateTime.Now);
                                }
                                else
                                {
                                    _logger.LogInformation($"烤箱门:{curDoorName}关闭，获取烘烤批次为空");
                                }
                            }
                        }
                        #endregion
                        #region 打开结束
                        else
                        {
                            if (newDoorState == (int)BakeDoorState.OPEN)
                            {
                                if (!string.IsNullOrEmpty(diviceBatchId))
                                {
                                    _logger.LogInformation($"烤箱门:{curDoorName}打开，结束上传温度数据：BatchID={diviceBatchId}");
                                    //结束上传
                                    UploadTemplateData(new TemperatureDataEntity
                                    {
                                        BatchID = diviceBatchId,
                                        TemperatureValue = curTemplateValue,
                                        UploadTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                        WorkState = (int)WorkState.WORK_END
                                    });
                                    BatchIDs[bakeIndex] = string.Empty;
                                    batchIdHistory.Remove(diviceBatchId);
                                }
                            }
                        }
                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("数据交互异常：" + ex.Message + "\n"+ ex.StackTrace);
            }
        }

        //void UpdateDeviceLocalData(int batchIndex, string newValue) 
        //{
        //    string configKey = string.Empty;
        //    if (batchIndex == 0)
        //    {
        //        configKey = "TemplateBatch:BatchNo1";
        //        _configuration["TemplateBatch:BatchNo1"] = newValue;
        //    }
        //    else if (batchIndex == 1)
        //    {
        //        configKey = "TemplateBatch:BatchNo2";
        //    }
        //    lock (_lock)
        //    {
        //        _configuration[configKey] = newValue;
        //        //_configuration.build
        //        _configuration.Bind(configKey);
        //    }
        //}

        /// <summary>
        /// 重试数据上传
        /// </summary>
        public async void RetryUploadTemperatureData()
        {
            List<string> uploadInfos = LocalFileLogger.ReadUploadLog();
            if (uploadInfos == null || uploadInfos.Count == 0) { return; }
            HashSet<string> retryInfos = new(uploadInfos);
            if (retryInfos.Count == 0) { return; }
            TemperatureDataEntity entity = null;
            foreach (var item in retryInfos)
            {
                try
                {
                    Thread.Sleep(200);
                    entity = JsonConvert.DeserializeObject<TemperatureDataEntity>(item);
                    if (entity != null && !string.IsNullOrEmpty(entity.BatchID))
                    {
                        LocalFileLogger.WriteUploadBakLog(item);
                        string requestUrl = _uploadDataUrl + "?postData=" + entity.ToJson();
                        _logger.LogInformation("RetryUploadTemperatureData Begin：" + requestUrl);
                        string strApiResult = await _httpClientHelper.HttpGetAsync(requestUrl);
                        if (!string.IsNullOrEmpty(strApiResult))
                        {
                            ApiResultEntity apiResult = JsonConvert.DeserializeObject<ApiResultEntity>(strApiResult);
                            if (apiResult != null)
                            {
                                if (apiResult.state != 1)
                                {
                                    //备份到待上传目录
                                    LocalFileLogger.WriteUploadSrcLog(item);
                                }
                            }
                        }
                        _logger.LogInformation("RetryUploadTemperatureData End：" + strApiResult);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("RetryUploadTemperatureData Error:"+ex.Message+"\n" + ex.StackTrace);
                    if (ex.Message.Contains("网络错误"))
                    {
                        _logger.LogInformation("RetryUploadTemperatureData Save to bak file：" + item);
                        //备份到待上传目录
                        LocalFileLogger.WriteUploadSrcLog(item);
                    }
                }
            }
        }
    }
}
