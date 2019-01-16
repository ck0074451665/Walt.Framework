using Microsoft.Extensions.Configuration;

namespace Walt.Framework.Configuration
{
    public class ZookeeperConfiguration
    { 
        public IConfiguration Configuration { get; }

        public ZookeeperConfiguration(IConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
    
}