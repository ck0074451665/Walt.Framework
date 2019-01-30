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

        public Action<Message> GetMessageDele{ get; set; }

        public Action<Error> ErrorDele{ get; set; }

        public Action<LogMessage> LogDele{ get; set; }

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
 
        public  async Task<Message> Producer<T>(string topic,string key,T t)
        {  
            if(string.IsNullOrEmpty(topic)
            || t==null)
            {
                throw new ArgumentNullException("topic或者value不能为null.");
            }
            string data = Newtonsoft.Json.JsonConvert.SerializeObject(t);
            var task=  await _producer.ProduceAsync(topic,ConvertToByte(key),ConvertToByte(data)); 
           return task;
        }


        public void AddProductEvent()
        {
            _producer.OnError+=new EventHandler<Error>(Error);
            _producer.OnLog+=new EventHandler<LogMessage>(Log);
        }

        public void AddConsumerEvent(IEnumerable<string> topics)
        {
            _consumer.Subscribe(topics);
            _consumer.OnMessage += new EventHandler<Message>(GetMessage);
            _consumer.OnError += new EventHandler<Error>(Error);
            _consumer.OnLog += new EventHandler<LogMessage>(Log);
        }

        private void GetMessage(object sender, Message mess)
        {
            if(GetMessageDele!=null)
            {
                GetMessageDele(mess);
            }
        }

        private void Error(object sender, Error mess)
        {
            if(ErrorDele!=null)
            {
                ErrorDele(mess);
            }
        }

        private void Log(object sender, LogMessage mess)
        {
            if(LogDele!=null)
            {
                LogDele(mess);
            }
        }
    }
}