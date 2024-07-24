using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempModbusNet.Extensions
{
    public static class DecimalExtensions
    {
        public static decimal TruncateEx(this decimal value, int decimalPlaces)
        {
            if (decimalPlaces < 0)
                throw new ArgumentException("decimalPlaces must be greater than or equal to 0.");

            var modifier = Convert.ToDecimal(0.5 / Math.Pow(10, decimalPlaces));
            return Math.Round(value >= 0 ? value - modifier : value + modifier, decimalPlaces);
        }

        public static double TruncatePre2(this double value)
        {
            return double.Parse(value.ToString("0.00"));
        }

        /// <summary>
        /// 相差时间(秒)
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns></returns>
        public static double DiffSeconds(this DateTime currentTime, DateTime endTime)
        {
            TimeSpan secondSpan = new(endTime.Ticks - currentTime.Ticks);
            return secondSpan.TotalSeconds;
        }
    }
}
