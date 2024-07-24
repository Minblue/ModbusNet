
//using Modbus.Device;
using Microsoft.Extensions.Logging;
using NModbus;
using NModbus.Serial;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace TempModbusNet.Helper
{
    /// <summary>
    /// Modbus帮助类
    /// </summary>
    class ModbusHelper
    {
        private SerialPort spTemp;
        private SerialPort spBake;
        private IModbusMaster tempMaster;
        private IModbusMaster bakeMaster;
        public string TempName { set; get; }
        private string BakeName { set; get; }

        // 创建 Modbus RTU 客户端实例  
        private readonly ModbusFactory factory = new();
        public ILogger Logger { get; set; }

        public ModbusHelper() { }
        public ModbusHelper(string spTempName, string spBakeName) 
        {
            TempName = spTempName;
            BakeName = spBakeName;
        }

        /// <summary>
        /// 获取温度串口
        /// </summary>
        /// <returns></returns>
        public SerialPort GetSpTemp()
        {
            return spTemp;
        }
        /// <summary>
        /// 获取烤箱串口
        /// </summary>
        /// <returns></returns>
        public SerialPort GetSpBake()
        {
            return spBake;
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        /// <param name="portName"></param>
        /// <returns></returns>
        public SerialPort OpenSerialPort(string portName)
        {
            try
            {
                SerialPort serialPort = new(portName)
                {
                    //设置串口基本参数
                    BaudRate = 9600,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None
                };
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                }
                serialPort.Open();
                return serialPort;
            }
            catch
            {
                throw;
            }
        }

        public void Dispose()
        {
            if (spTemp != null && spTemp.IsOpen)
            {
                tempMaster.Dispose();
                spTemp.Close();
                spTemp.Dispose();
            }

            if (spBake != null && spBake.IsOpen)
            {
                bakeMaster.Dispose();
                spBake.Close();
                spBake.Dispose();
            }
        }

        /// <summary>
        /// 创建对象Modbus-Rtu
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public IModbusMaster CreateRtuMaster(SerialPort port)
        {
            IModbusMaster master = factory.CreateRtuMaster(port);
            master.Transport.ReadTimeout = 2000;
            master.Transport.Retries = 10;
            return master;
        }

        /// <summary>
        /// 读取寄存器数据-异步
        /// </summary>
        /// <param name="master">modbus对象</param>
        /// <param name="slaveAddress">从站地址</param>
        /// <param name="startAddress">开始地址</param>
        /// <param name="numberOfPoints">截止点</param>
        /// <returns></returns>
        public Task<ushort[]> ReadHoldingRegistersAsync(IModbusMaster master, byte slaveAddress, ushort startAddress, ushort numberOfPoints)
        {
            return master.ReadHoldingRegistersAsync(slaveAddress, startAddress, numberOfPoints);
        }

        public void CreateModbusRtuConnect()
        {
            try
            {
                spTemp = OpenSerialPort(TempName);
                spBake = OpenSerialPort(BakeName);
                tempMaster = CreateRtuMaster(spTemp);
                bakeMaster = CreateRtuMaster(spBake);
            }
            catch (Exception ex)
            {
                Logger.LogError("打开COM失败："+ ex.Message + "\n" + ex.StackTrace);
            }
        }


        public async Task<Tuple<ushort[], ushort[]>> ModbusSerialRtuMasterReadRegisters(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Logger.LogInformation("读取温度数据" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                if (spTemp == null)
                {
                    spTemp = OpenSerialPort(TempName);
                    tempMaster = CreateRtuMaster(spTemp);
                }
                else
                {
                    if (!spTemp.IsOpen)
                    {
                        spTemp.Open();
                        tempMaster = CreateRtuMaster(spTemp);
                    }
                }
                ushort[] tempValues = await ReadHoldingRegistersAsync(tempMaster, 1, 0, 2);
                Logger.LogInformation("读取开关数据" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                if (spBake == null)
                {
                    spBake = OpenSerialPort(BakeName);
                    bakeMaster = CreateRtuMaster(spBake);
                }
                else
                {
                    if (!spBake.IsOpen)
                    {
                        spBake.Open();
                        bakeMaster = CreateRtuMaster(spBake);
                    }
                }
                ushort[] bakeValues = await ReadHoldingRegistersAsync(bakeMaster, 1, 16, 2);
                return Tuple.Create(tempValues, bakeValues);
            }
            catch (Exception ex)
            {
                Logger.LogError("读取Modbus失败:"+ ex.Message + "\n" + ex.StackTrace);
            }
            return null;
        }
    }
}
