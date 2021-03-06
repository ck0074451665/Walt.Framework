using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Walt.Framework.Service.Zookeeper;

namespace Walt.Framework.Configuration
{
    public static class ZookeeperConfigurationExtensioncs
    {
          public static IZookeeperBuilder AddConfiguration(this IZookeeperBuilder builder
          ,IConfiguration configuration)
          {
               
                InitService( builder,configuration); 
                return builder;
          }


          public static void InitService(IZookeeperBuilder builder,IConfiguration configuration)
          {
            builder.Services.TryAddSingleton<IConfigureOptions<ZookeeperOptions>>(
                  new ZookeeperConfigurationOptions(configuration));

            builder.Services.TryAddSingleton
            (ServiceDescriptor.Singleton<IOptionsChangeTokenSource<ZookeeperOptions>>(
                  new ConfigurationChangeTokenSource<ZookeeperOptions>(configuration)) );

            builder.Services
            .TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<ZookeeperOptions>>
            (new ConfigureFromConfigurationOptions<ZookeeperOptions>(configuration)));
            
             builder.Services.AddSingleton(new ZookeeperConfiguration(configuration));
          }
    }
} 