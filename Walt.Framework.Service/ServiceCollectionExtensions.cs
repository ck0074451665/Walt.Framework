 using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Walt.Framework.Service.Kafka;
using Walt.Framework.Service.Zookeeper;

namespace Walt.Framework.Service
{
  
  
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds logging services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public static IServiceCollection AddKafka(this IServiceCollection services)
        {
            return AddKafka(services, builder => { });
        }
 
        public static IServiceCollection AddKafka(this IServiceCollection services
        , Action<IKafkaBuilder> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions(); 
            services.TryAddSingleton<IKafkaService,KafkaService>(); 
            configure(new KafkaBuilder(services));
            
            return services;
        }


         public static IServiceCollection AddZookeeper(this IServiceCollection services
        , Action<IZookeeperBuilder> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions();  
            services.TryAddSingleton<IZookeeperService,ZookeeperService>(); 
            configure(new ZookeeperBuilder(services));
            return services;
        }
    }
}