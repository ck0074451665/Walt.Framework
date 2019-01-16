using Microsoft.Extensions.DependencyInjection;

namespace Walt.Framework.Service.Kafka
{
    public class KafkaBuilder : IKafkaBuilder
    {
        public IServiceCollection Services {get;}

        public KafkaBuilder(IServiceCollection services)
        {
            Services=services;
        }
    }
}