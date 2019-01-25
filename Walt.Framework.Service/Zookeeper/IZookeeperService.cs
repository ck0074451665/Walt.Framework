using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using org.apache.zookeeper;
using org.apache.zookeeper.data;

namespace Walt.Framework.Service.Zookeeper
{
    public interface IZookeeperService
    {
        Task<string> CreateZNode(string path,string data,CreateMode createMode,List<ACL> aclList);

        Task<DataResult> GetDataAsync(string path,Watcher watcher,bool isSync);

        Task<Stat> SetDataAsync(string path,string data,bool isSync);

        Task<ChildrenResult> GetChildrenAsync(string path,Watcher watcher,bool isSync) ;

        Task DeleteNode(string path);

        string GetDataByLockNode(string path,string sequenceName,List<ACL> aclList,out string tempNode);

        void Close(string tempNode);
    }
}