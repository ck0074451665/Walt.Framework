using System;
using Microsoft.Extensions.Logging;
using Quartz.Logging;

namespace  Walt.Framework.Quartz.Host
 {
     public class ConsoleLogProvider : ILogProvider
    {
            private ILoggerFactory _logFactory;

            public ConsoleLogProvider(ILoggerFactory logFactory)
            {
                _logFactory=logFactory;
            }
            public Logger GetLogger(string name)
            {
                return (level, func, exception, parameters) =>
                {
                    if (func != null)
                    {
                        string logInfo=string.Format(func(), parameters);
                        var log=_logFactory.CreateLogger<ConsoleLogProvider>();
                        log.LogDebug(logInfo);
                    }
                    return true;
                };
            }

            public IDisposable OpenNestedContext(string message)
            {
                throw new NotImplementedException();
            }

            public IDisposable OpenMappedContext(string key, string value)
            {
                throw new NotImplementedException();
            }
    }
 }
 
 