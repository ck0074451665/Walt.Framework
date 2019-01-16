using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace Walt.Framework.Log
{
    internal class CustomizationLoggerOptionsSetup : ConfigureFromConfigurationOptions<CustomizationLoggerOptions>
    {
        public CustomizationLoggerOptionsSetup(ILoggerProviderConfiguration<CustomizationLoggerProvider> 
        providerConfiguration)
            : base(providerConfiguration.Configuration)
        {
        }
    }
}