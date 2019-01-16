using Microsoft.Extensions.DependencyInjection;

namespace Walt.Framework.Service.Zookeeper
{
    public interface IZookeeperBuilder
    {
         /// <summary>
        /// Gets the <see cref="IServiceCollection"/> where Logging services are configured.
        /// </summary>
        IServiceCollection Services { get; }
    }
}