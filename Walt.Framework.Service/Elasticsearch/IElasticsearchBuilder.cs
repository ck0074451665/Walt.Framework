using Microsoft.Extensions.DependencyInjection;

namespace Walt.Framework.Service.Elasticsearch
{
    public interface IElasticsearchBuilder
    {
        IServiceCollection Services {get;}
    }
}