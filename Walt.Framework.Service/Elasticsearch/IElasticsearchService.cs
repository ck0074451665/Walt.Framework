using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using Nest;

namespace Walt.Framework.Service.Elasticsearch
{
    public interface IElasticsearchService
    {
        // Task<IBulkResponse> AddDocument(IBulkRequest bulk); 
        Task<bool> CreateIndexIfNoExists<T>(string indexName) where T : class;

        // Task<bool> CreateMappingIfNoExists<T>(string indexName
        // , Func<PutMappingDescriptor<T>, IPutMappingRequest> selector) where T : class;
        Task<ICreateResponse> CreateDocument<T>(string indexName,T  t) where T:class;
    }
}