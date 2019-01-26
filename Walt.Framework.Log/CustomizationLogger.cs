// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Walt.Framework.Service.Kafka;

namespace Walt.Framework.Log
{

    public class  EntityMessages
    {
        public string Id{ get; set; }

        public string MachineName{ get; set; }

        public DateTime DateTime{ get; set; }

        public string OtherFlag{ get; set; }



        public LogLevel LogLevel{ get; set; }

        public string Message{ get; set; }
    }
  
    public class CustomizationLogger : ILogger
    {
        private static readonly string _loglevelPadding = ": ";
        private static readonly string _messagePadding;
        private static readonly string _newLineWithMessagePadding;

        // ConsoleColor does not have a value to specify the 'Default' color 
 
        private Func<string, LogLevel, bool> _filter;

        private BlockingCollection<EntityMessages> blockColl =
        new BlockingCollection<EntityMessages>();

        [ThreadStatic]
        private static StringBuilder _logBuilder;

        private string _prix;

        private string _logStoreTopic;

         private IKafkaService _kafkaService;

        static CustomizationLogger()
        {
            var logLevelString = GetLogLevelString(LogLevel.Information);
            _messagePadding = new string(' ', logLevelString.Length + _loglevelPadding.Length);
            _newLineWithMessagePadding = Environment.NewLine + _messagePadding;
        }

        public CustomizationLogger(string name, Func<string, LogLevel, bool> filter
        , IExternalScopeProvider scopeProvider, bool includeScopes,string prix,string logStoreTopic, IKafkaService kafkaService)
            : this(name, filter, includeScopes ? new LoggerExternalScopeProvider() : null
           ,prix,logStoreTopic,kafkaService)
        {
            Task.Run(()=>{
                try
                {
                    foreach (var entityMess in blockColl.GetConsumingEnumerable())
                    {
                        var task = _kafkaService.Producer(_logStoreTopic
                        , entityMessage.Id, entityMessage);
                        if (task == null) throw new NullReferenceException("方法没有返回有效的task");

                        //System.Diagnostics.Debug.WriteLine("即将执行kafka日志Producer");
                        var result = task.Result;
                        //Console.WriteLine(_prix+"--"+ logBuilder.ToString());
                    }
                }
                catch(Exception ep)
                {
                    blockColl.CompleteAdding();
                }
            });
        }

        

        internal CustomizationLogger(string name, Func<string, LogLevel, bool> filter
        , IExternalScopeProvider scopeProvider, string prix,string logStoreTopic
        , IKafkaService kafkaService)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            _kafkaService=kafkaService;

            Name = name;
            Filter = filter ?? ((category, logLevel) => true);
            ScopeProvider = scopeProvider;
            _prix=prix;
            _logStoreTopic=logStoreTopic;
        }

        ///添加
        private AddCollMess(EntityMessages message)
        {
            if (!blockColl.IsAddingCompleted)
            {
                blockColl.Add(entityMessage);
            }
            else
            {
                var task = _kafkaService.Producer(_logStoreTopic
                      , entityMessage.Id, entityMessage);
                if (task == null) throw new NullReferenceException("方法没有返回有效的task");

                //System.Diagnostics.Debug.WriteLine("即将执行kafka日志Producer");
                var result = task.Result;
                //Console.WriteLine(_prix+"--"+ logBuilder.ToString());
            }
        }
    

        public Func<string, LogLevel, bool> Filter
        {
            get { return _filter; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _filter = value;
            }
        }

        public string Name { get; }

 

        internal IExternalScopeProvider ScopeProvider { get; set; }

        public bool DisableColors { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);

            if (!string.IsNullOrEmpty(message) || exception != null)
            {
                WriteMessage(logLevel, Name, eventId.Id, message, exception);
            }
        }

        public virtual void WriteMessage(LogLevel logLevel, string logName, int eventId, string message, Exception exception)
        {
            var logBuilder = _logBuilder;
            _logBuilder = null;

            if (logBuilder == null)
            {
                logBuilder = new StringBuilder();
            }
 
            var logLevelString = string.Empty;

            // Example:
            // INFO: ConsoleApp.Program[10]
            //       Request received
 
            logLevelString = GetLogLevelString(logLevel);
            // category and event id
            logBuilder.Append(_loglevelPadding);
            logBuilder.Append(logName);
            logBuilder.Append("[");
            logBuilder.Append(eventId);
            logBuilder.AppendLine("]");

            // scope information
            GetScopeInformation(logBuilder);

            if (!string.IsNullOrEmpty(message))
            {
                // message
                logBuilder.Append(_messagePadding);

                var len = logBuilder.Length;
                logBuilder.AppendLine(message);
                logBuilder.Replace(Environment.NewLine, _newLineWithMessagePadding, len, message.Length);
            }

            // Example:
            // System.InvalidOperationException
            //    at Namespace.Class.Function() in File:line X
            if (exception != null)
            {
                // exception message
                logBuilder.AppendLine(exception.ToString());
            }

            var hasLevel = !string.IsNullOrEmpty(logLevelString);
            // Queue log message
            string machineName=Environment.MachineName;
            var entityMessage = new EntityMessages()
            {
                Id = Guid.NewGuid.ToString("N"),
                MachineName = machineName,
                OtherFlag = _prix,
                DateTime = DateTime.Now,
                LogLevel = (hasLevel ? logLevelString : null),
                Message = logBuilder.ToString()
            };
            AddCollMess(entityMessage);

            logBuilder.Clear();
            if (logBuilder.Capacity > 1024)
            {
                logBuilder.Capacity = 1024;
            }
            _logBuilder = logBuilder;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel == LogLevel.None)
            {
                return false;
            }

            return Filter(Name, logLevel);
        }

        public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? NullScope.Instance;

        private static string GetLogLevelString(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return "trce";
                case LogLevel.Debug:
                    return "dbug";
                case LogLevel.Information:
                    return "info";
                case LogLevel.Warning:
                    return "warn";
                case LogLevel.Error:
                    return "fail";
                case LogLevel.Critical:
                    return "crit";
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel));
            }
        }

        

        private void GetScopeInformation(StringBuilder stringBuilder)
        {
            var scopeProvider = ScopeProvider;
            if (scopeProvider != null)
            {
                var initialLength = stringBuilder.Length;

                scopeProvider.ForEachScope((scope, state) =>
                {
                    var (builder, length) = state;
                    var first = length == builder.Length;
                    builder.Append(first ? "=> " : " => ").Append(scope);
                }, (stringBuilder, initialLength));

                if (stringBuilder.Length > initialLength)
                {
                    stringBuilder.Insert(initialLength, _messagePadding);
                    stringBuilder.AppendLine();
                }
            }
        }
 
      
    }
}
