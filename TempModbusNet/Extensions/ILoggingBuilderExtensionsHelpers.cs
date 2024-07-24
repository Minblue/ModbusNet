using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using TempModbusNet.Entity;
using TempModbusNet.Service;
using TempModbusNet.Services;

namespace TempModbusNet.Extensions 
{

    internal static class ILoggingBuilderExtensionsHelpers
    {

        public static void AddLocalFileLogger(this ILoggingBuilder builder, Action<LoggerSettingEntity> action)
        {
            builder.Services.Configure(action);
            builder.Services.AddSingleton<ILoggerProvider, LocalFileLoggerProvider>();
            //builder.Services.AddSingleton<IHostedService, LogClearTaskService>();
        }
    }
}