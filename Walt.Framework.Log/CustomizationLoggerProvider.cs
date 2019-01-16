// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console.Internal;
using Microsoft.Extensions.Options;
using Walt.Framework.Service.Kafka;
using Microsoft.Extensions.DependencyInjection;

namespace Walt.Framework.Log
{
// IConsoleLoggerSettings is obsolete
#pragma warning disable CS0618 // Type or member is obsolete

    [ProviderAlias("KafkaLog")]
    public class CustomizationLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        public IServiceProvider _services { get; }

         
        private readonly ConcurrentDictionary<string, CustomizationLogger> _loggers =
         new ConcurrentDictionary<string, CustomizationLogger>();

        private readonly Func<string, LogLevel, bool> _filter;  
        private static readonly Func<string, LogLevel, bool> trueFilter = (cat, level) => true;
        private static readonly Func<string, LogLevel, bool> falseFilter = (cat, level) => false;
        private IDisposable _optionsReloadToken;
        private bool _includeScopes; 

        private string _prix;
        private string _logStoreTopic;

        private IExternalScopeProvider _scopeProvider;

 
        public CustomizationLoggerProvider(IServiceProvider services,IOptionsMonitor<CustomizationLoggerOptions> options)
        {
            // Filter would be applied on LoggerFactory level
            _services=services;
            _filter = trueFilter;
            _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
            ReloadLoggerOptions(options.CurrentValue);
        }

        private void ReloadLoggerOptions(CustomizationLoggerOptions options)
        {
            _includeScopes = options.IncludeScopes; 
              _prix=options.Prix;
            _logStoreTopic=options.LogStoreTopic;
            var scopeProvider = GetScopeProvider();
            foreach (var logger in _loggers.Values)
            {
                logger.ScopeProvider = scopeProvider; 
            }
        }

  
        private IEnumerable<string> GetKeyPrefixes(string name)
        {
            while (!string.IsNullOrEmpty(name))
            {
                yield return name;
                var lastIndexOfDot = name.LastIndexOf('.');
                if (lastIndexOfDot == -1)
                {
                    yield return "Default";
                    break;
                }
                name = name.Substring(0, lastIndexOfDot);
            }
        }

        private IExternalScopeProvider GetScopeProvider()
        {
            if (_includeScopes && _scopeProvider == null)
            {
                _scopeProvider = new LoggerExternalScopeProvider();
            }
            return _includeScopes ? _scopeProvider : null;
        }

        public void Dispose()
        {
            _optionsReloadToken?.Dispose(); 
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        public ILogger CreateLogger(string name)
        {
            return _loggers.GetOrAdd(name, CreateLoggerImplementation);
        }

        private CustomizationLogger CreateLoggerImplementation(string name)
        {
            var includeScopes =  _includeScopes; 
            IKafkaService kafkaService=_services.GetService<IKafkaService>();
            return new  CustomizationLogger(name,null
            ,includeScopes? _scopeProvider: null,_prix,_logStoreTopic,kafkaService);
        }
    }
#pragma warning restore CS0618
}
