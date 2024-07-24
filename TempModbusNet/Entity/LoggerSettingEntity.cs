using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempModbusNet.Entity
{
    internal class LoggerSettingEntity
    {
        /// <summary>
        /// 保存天数
        /// </summary>
        public int SaveDays { get; set; } = 7;
    }
}
