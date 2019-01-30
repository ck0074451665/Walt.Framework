using System;
using Nest;

namespace Walt.Framework.Console
{
    [ElasticsearchTypeAttribute(Name="LogElasticsearchDefaultType")]
    public class LogElasticsearch
    {
        public string Id { get; set; }

        public DateTime Time { get; set; }

        public string LogLevel{ get; set; }

        public string Exception{ get; set; }

        public string Mess{ get; set; }
    }
}