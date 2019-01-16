using Microsoft.Extensions.DependencyInjection;

namespace Walt.Framework.Service.Zookeeper
{
    public class ZookeeperBuilder : IZookeeperBuilder
    {
        public IServiceCollection Services {get;}

        public ZookeeperBuilder(IServiceCollection services)
        {
            Services=services;
        }
    }
}