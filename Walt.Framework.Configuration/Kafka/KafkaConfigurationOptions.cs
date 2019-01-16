using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Walt.Framework.Service.Kafka;

namespace Walt.Framework.Configuration
{
    public class KafkaConfigurationOptions : IConfigureOptions<KafkaOptions>
    {

        private readonly IConfiguration _configuration;


        public KafkaConfigurationOptions(IConfiguration configuration)
        {
           _configuration=configuration;
        }


        public void Configure(KafkaOptions options)
        {
             System.Diagnostics.Debug.WriteLine("kafka配置类，适配方法。"
             +Newtonsoft.Json.JsonConvert.SerializeObject(options));
        }
    }
}