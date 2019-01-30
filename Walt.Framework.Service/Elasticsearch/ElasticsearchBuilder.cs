using Microsoft.Extensions.DependencyInjection;

namespace Walt.Framework.Service.Elasticsearch
{
    public class ElasticsearchBuilder : IElasticsearchBuilder
    {
        public IServiceCollection Services {get;}

        public ElasticsearchBuilder(IServiceCollection services)
        {
            Services=services;
        }
    }
}