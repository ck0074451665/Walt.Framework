using System;
using System.Collections.Generic;

namespace Walt.Framework.Service.Elasticsearch
{
    public class ElasticsearchOptions
    {
        public string[] Host{ get; set; }

        public int TimeOut{ get; set; }

        public string User{ get; set; }

        public string Pass{ get; set;}
    }
}