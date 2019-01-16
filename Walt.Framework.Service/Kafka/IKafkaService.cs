using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Walt.Framework.Service.Kafka
{
    public interface IKafkaService
    {
     Task<Message> Producer(string topic,string key,string value);
    }
}