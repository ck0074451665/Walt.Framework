using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Walt.Framework.Service.Kafka;

namespace Walt.Framework.Configuration
{
    public static class KafkaConfigurationExtensioncs
    {
          public static IKafkaBuilder AddConfiguration(this IKafkaBuilder builder
          ,IConfiguration configuration)
          {
               
                InitService( builder,configuration); 
                return builder;
          }


          public static void InitService(IKafkaBuilder builder,IConfiguration configuration)
          {
            builder.Services.TryAddSingleton<IConfigureOptions<KafkaOptions>>(
                  new KafkaConfigurationOptions(configuration));

            builder.Services.TryAddSingleton
            (ServiceDescriptor.Singleton<IOptionsChangeTokenSource<KafkaOptions>>(
                  new ConfigurationChangeTokenSource<KafkaOptions>(configuration)) );

            builder.Services
            .TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KafkaOptions>>
            (new ConfigureFromConfigurationOptions<KafkaOptions>(configuration)));
            
             builder.Services.AddSingleton(new KafkaConfiguration(configuration));
          }
    }
} 