using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace  Walt.Framework.Service.Kafka
{
    public class KafkaService : IKafkaService
    {

        private KafkaOptions _kafkaOptions;
        private Producer _producer;
        private Consumer _consumer;
        public KafkaService(IOptionsMonitor<KafkaOptions>  kafkaOptions)
        {
            _kafkaOptions=kafkaOptions.CurrentValue; 
            kafkaOptions.OnChange((kafkaOpt,s)=>{
                _kafkaOptions=kafkaOpt; 
                    System.Diagnostics.Debug
                    .WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(kafkaOpt)+"---"+s);
                    
            });
             _producer=new Producer(_kafkaOptions.Properties);

            _consumer=new Consumer(_kafkaOptions.Properties);
        }

        private byte[] ConvertToByte(string str)
        {
            return System.Text.Encoding.Default.GetBytes(str);
        }
 
        public  async Task<Message> Producer(string topic,string key,string value)
        {  
            if(string.IsNullOrEmpty(topic)
            ||string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException("topic或者value不能为null.");
            }
      
           var task=  await _producer.ProduceAsync(topic,ConvertToByte(key),ConvertToByte(value)); 
           return task;
        }
    }
}