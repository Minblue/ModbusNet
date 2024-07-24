using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempModbusNet.Services
{
    //瞬时注入服务接口
    public interface ITransient
    { }

    //作用域注入服务接口
    public interface IScoped
    { }

    //单例注入服务接口
    public interface ISingleton
    { }
}
