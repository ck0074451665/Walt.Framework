using Microsoft.Extensions.Configuration;

namespace Walt.Framework.Configuration
{
    public class KafkaConfiguration
    { 
        public IConfiguration Configuration { get; }

        public KafkaConfiguration(IConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
    
}