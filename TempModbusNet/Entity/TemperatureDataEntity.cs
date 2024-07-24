using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempModbusNet.Entity
{

    public enum WorkState
    {
        WORK_STARTING = 0, //开始
        WORK_RUNNING = 1, //运行中
        WORK_END = 2//结束
    }

    public enum BakeDoorState
    {
        OPEN = 0, //打开
        CLOSED = 1 //关闭
    }

    /// <summary>
    /// 温度数据实体
    /// </summary>
    public class TemperatureDataEntity
    {
        public string BatchID { get; set; }
        public double TemperatureValue { get; set; }
        public string UploadTime { get; set; }
        public int WorkState { get; set; }
    }

    /// <summary>
    /// 温度数据实体
    /// </summary>
    public class QueryTemplateEntity
    {
        /// <summary>
        /// 烤箱号ID
        /// </summary>
        public string EquipID { get; set; }
        /// <summary>
        /// 批次ID
        /// </summary>
        public string BatchID { get; set; }
    }

    /// <summary>
    /// API实体
    /// </summary>
    public class ApiResultEntity
    {
        public int? state { get; set; }//1成功 2失败
        public string message { get; set; }//返回的消息，成功的或者失败
        public string data { get; set; }//数据
    }
}
