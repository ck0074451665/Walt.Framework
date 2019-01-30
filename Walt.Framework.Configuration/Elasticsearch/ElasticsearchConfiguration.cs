using Microsoft.Extensions.Configuration;

namespace Walt.Framework.Configuration
{
    public class ElasticsearchConfiguration
    { 
        public IConfiguration Configuration { get; }

        public ElasticsearchConfiguration(IConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
    
}