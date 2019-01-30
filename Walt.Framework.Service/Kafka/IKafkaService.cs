using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Walt.Framework.Service.Kafka
{
    public interface IKafkaService
    {
        Task<Message> Producer<T>(string topic, string key, T t);


        void AddProductEvent();

        void AddConsumerEvent(IEnumerable<string> topics);

        Action<Message> GetMessageDele { get; set; }
        Action<Error> ErrorDele { get; set; }
        Action<LogMessage> LogDele { get; set; }
    }
}