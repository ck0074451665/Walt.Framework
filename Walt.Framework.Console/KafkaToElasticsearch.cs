using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Walt.Framework.Service.Elasticsearch;
using Walt.Framework.Service.Kafka;

namespace Walt.Framework.Console
{
    public class KafkaToElasticsearch : IConsole
    {
        ILoggerFactory _logFact;

        IConfiguration _config;

        IElasticsearchService _elasticsearch;

        IKafkaService _kafkaService;

        public KafkaToElasticsearch(ILoggerFactory logFact,IConfiguration config
        ,IElasticsearchService elasticsearch
        ,IKafkaService kafkaService)
        {
            _logFact = logFact;
            _config = config;
            _elasticsearch = elasticsearch;
            _kafkaService = kafkaService;
        }
        public Task AsyncExcute(CancellationToken cancel=default(CancellationToken))
        {
            var log = _logFact.CreateLogger<KafkaToElasticsearch>();
            _kafkaService.AddConsumerEvent(new List<string>(){"mylog"});
            _kafkaService.GetMessageDele = (message) => {
                var id = message.Key;
                var offset = string.Format("{0}---{2}",message.Offset.IsSpecial,message.Offset.Value);
                var topic = message.Topic;
                var topicPartition = message.TopicPartition.Partition.ToString();
                var topicPartitionOffsetValue = message.TopicPartitionOffset.Offset.Value;
                log.LogInformation("id:{0},offset:{1},topic:{2},topicpatiton:{3},topicPartitionOffsetValue:{4}"
                ,id,offset,topic,topicPartition,topicPartitionOffsetValue);
            };
             _kafkaService.ErrorDele = (message) => {
                 log.LogError(message.ToString());
             };
             _kafkaService.LogDele = (message) => { 
                 log.LogInformation(message.ToString());
            };
            log.LogInformation("事件添加完毕");
            var waitForStop = 
            new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancel.Register(()=>{
                log.LogInformation("task执行被取消回掉函数");
                waitForStop.SetResult(null);
            });
            waitForStop.Task.Wait();
            log.LogInformation("任务已经被取消。");
            return Task.FromResult(true);
        }
    }
}