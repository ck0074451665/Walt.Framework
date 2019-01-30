using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Walt.Framework.Service.Elasticsearch;
using Walt.Framework.Service.Kafka;

namespace Walt.Framework.Configuration
{
    public static class ElasticsearchConfigurationExtensioncs
    {
          public static IElasticsearchBuilder AddConfiguration(this IElasticsearchBuilder builder
          ,IConfiguration configuration)
          {
               
                InitService( builder,configuration); 
                return builder;
          }


          public static void InitService(IElasticsearchBuilder builder,IConfiguration configuration)
          {
            builder.Services.TryAddSingleton<IConfigureOptions<ElasticsearchOptions>>(
                  new ElasticsearchConfigurationOptions(configuration));

            builder.Services.TryAddSingleton
            (ServiceDescriptor.Singleton<IOptionsChangeTokenSource<ElasticsearchOptions>>(
                  new ConfigurationChangeTokenSource<ElasticsearchOptions>(configuration)) );

            builder.Services
            .TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<ElasticsearchOptions>>
            (new ConfigureFromConfigurationOptions<ElasticsearchOptions>(configuration)));
            
             builder.Services.AddSingleton(new ElasticsearchConfiguration(configuration));
             
          }
    }
} 