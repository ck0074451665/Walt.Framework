
using System;
using System.Net.Http;
using Elasticsearch.Net;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nest;

namespace Walt.Framework.Service.Elasticsearch
{
    public class ElasticsearchService:IElasticsearchService
    {

        private  ElasticsearchOptions _elasticsearchOptions=null;

        private ElasticClient _elasticClient = null;

        private ILoggerFactory _loggerFac;

        public ElasticsearchService(IOptionsMonitor<ElasticsearchOptions>  options
        ,ILoggerFactory loggerFac)
        {
            _elasticsearchOptions = options.CurrentValue;
             options.OnChange((elasticsearchOpt,s)=>{
                _elasticsearchOptions=elasticsearchOpt; 
                    System.Diagnostics.Debug
                    .WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(elasticsearchOpt)+"---"+s);
            });

            var lowlevelClient = new ElasticLowLevelClient();
            var urlColl = new Uri[_elasticsearchOptions.Host.Length];
            for (int i = 0; i < _elasticsearchOptions.Host.Length;i++)
            {
                urlColl[i] = new Uri(_elasticsearchOptions.Host[i]);
            }
            _loggerFac = loggerFac;
            var connectionPool = new SniffingConnectionPool(urlColl);
            var settings = new ConnectionSettings(connectionPool)
            .RequestTimeout(TimeSpan.FromMinutes(_elasticsearchOptions.TimeOut));
            _elasticClient = new ElasticClient(settings);
        }

        public async Task<bool> CreateIndexIfNoExists(IndexName indexName,IndexSettings settings)
        {
            
            var log=_loggerFac.CreateLogger<ElasticsearchService>();
            var exists =await _elasticClient.IndexExistsAsync(Indices.Index(indexName));
            if(exists.Exists)
            {
                log.LogWarning("index:{0}已经存在",indexName.ToString());
                return await Task.FromResult(true);
            }
            else
            {
                CreateIndexRequest request = new CreateIndexRequest(indexName);
                request.Settings = settings;
                var response=await _elasticClient.CreateIndexAsync(request);
                log.LogInformation(response.DebugInformation);
                if(response.Acknowledged)
                {
                    log.LogInformation("index:{0},创建成功",indexName.ToString());
                    return await Task.FromResult(false);
                }
                else
                {
                    log.LogError(response.ServerError.ToString());
                    log.LogError(response.OriginalException.ToString());
                    return await Task.FromResult(false);
                }
            }
        }


        public async Task<bool> CreateMappingIfNoExists<T>(IndexName indexName, TypeName typeName
        , Func<PutMappingDescriptor<T>, IPutMappingRequest> selector) where T : class
        {
            var log = _loggerFac.CreateLogger<ElasticsearchService>();
            var types = Types.Parse(typeName.Name);
            var exists = await _elasticClient.TypeExistsAsync(Indices.Index(indexName), typeName);
            if (exists.Exists)
            {
                log.LogWarning("index:{0},type:{1}已经存在", indexName, typeName);
                return await Task.FromResult(true);
            }
            PutMappingRequest indexMappings = new PutMappingRequest(indexName,typeName);
            
            var putMapping = await _elasticClient.MapAsync<T>((des) =>
            {
                return selector(des);
            });
            log.LogInformation(putMapping.DebugInformation);
            if (putMapping.Acknowledged)
            {
                log.LogInformation("index:{0},type:{1},创建成功", indexName, typeName);
                return await Task.FromResult(false);
            }
            else
            {
                log.LogError(putMapping.ServerError.ToString());
                log.LogError(putMapping.OriginalException.ToString());
                return await Task.FromResult(false);
            }

        }


        public async Task<IBulkResponse> AddDocument(IBulkRequest  bulk)
        {
            var log=_loggerFac.CreateLogger<ElasticsearchService>(); 
            if(bulk==null)
            {
                log.LogError("bulk 参数不能为空。");
                return null;
            }
            var bulkResponse = await _elasticClient.BulkAsync(bulk);
             log.LogInformation(bulkResponse.DebugInformation);
            if (bulkResponse.ApiCall.Success)
            {
                log.LogInformation("index:{0},type:{1},创建成功", bulk.Index, bulk.Type);
                return bulkResponse;
            }
            else
            {
                log.LogError(bulkResponse.ServerError.ToString());
                log.LogError(bulkResponse.OriginalException.ToString());
                return null;
            }
        }
    }
}