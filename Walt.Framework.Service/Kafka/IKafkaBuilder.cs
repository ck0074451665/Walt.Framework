using Microsoft.Extensions.DependencyInjection;

namespace Walt.Framework.Service.Kafka
{
    public interface IKafkaBuilder
    {
         /// <summary>
        /// Gets the <see cref="IServiceCollection"/> where Logging services are configured.
        /// </summary>
        IServiceCollection Services { get; }
    }
}