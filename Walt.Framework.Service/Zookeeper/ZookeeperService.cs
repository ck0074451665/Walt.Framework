using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using org.apache.zookeeper;
using org.apache.zookeeper.data;
using static org.apache.zookeeper.ZooKeeper;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using static org.apache.zookeeper.Watcher.Event;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace  Walt.Framework.Service.Zookeeper
{

    internal class  WaitLockWatch:Watcher
    {
        private AutoResetEvent _autoResetEvent;
        private ILogger _logger;

        private string _path;

        private ZookeeperService _zookeeperService;

        public string _tempNode;

        public WaitLockWatch(AutoResetEvent autoResetEvent
        ,ZookeeperService zookeeperService
        ,ILogger logger,string path
        ,string tempNode)
        {
            _autoResetEvent=autoResetEvent;
            _zookeeperService=zookeeperService;
            _logger=logger;
            _path=path;
            _tempNode=tempNode;
        }

       public override Task process(WatchedEvent @event)
       {
           _logger.LogDebug("{0}节点下子节点发生改变，激发监视方法。",_path);
            var childList=_zookeeperService.GetChildrenAsync(_path,null,true).Result;
            if(childList==null||childList.Children==null||childList.Children.Count<1)
                   {
                        _logger.LogDebug("获取子序列失败，计数为零.path:{0}",_path);
                        return Task.FromResult(0);
                   }
                   var top=childList.Children.OrderBy(or=>or).First();
                   if(_path+"/"+top==_tempNode)
                   {
                        _logger.LogDebug("释放阻塞");
                        _autoResetEvent.Set();
                   }
           
            return Task.FromResult(0);
       }
    }


    internal class WaitConnWatch : Watcher
    {
        private AutoResetEvent _autoResetEvent;
        private ILogger _logger;

        public WaitConnWatch(AutoResetEvent autoResetEvent
        ,ILogger logger)
        {
            _autoResetEvent=autoResetEvent;
            _logger=logger;
        }

       public override Task process(WatchedEvent @event)
       {
           _logger.LogDebug("watch激发,回掉状态：{0}",@event.getState().ToString());
            if(@event.getState()== KeeperState.SyncConnected
            ||@event.getState()== KeeperState.ConnectedReadOnly)
            {
                _logger.LogDebug("释放阻塞");
                _autoResetEvent.Set();
            }
            return Task.FromResult(0);
       }
    }

    public class ZookeeperService : IZookeeperService
    {

        public List<string> requestLockSequence=new List<string>();
        private object _lock=new object();
        private ZookeeperOptions _zookeeperOptions;
        private ZooKeeper _zookeeper;

         private static readonly byte[] NO_PASSWORD = new byte[0];

         public Watcher Wathcer {get;set;}

         public ILoggerFactory LoggerFac { get; set; }

         private ILogger _logger;

         AutoResetEvent[] autoResetEvent=new AutoResetEvent[2]
         {new AutoResetEvent(false),new AutoResetEvent(false)};

        public ZookeeperService(IOptionsMonitor<ZookeeperOptions>  zookeeperOptions
        ,ILoggerFactory loggerFac)
        {
            LoggerFac=loggerFac;
            _logger=LoggerFac.CreateLogger<ZookeeperService>();
            _zookeeperOptions=zookeeperOptions.CurrentValue; 
            _logger.LogDebug("配置参数：{0}",JsonConvert.SerializeObject(_zookeeperOptions));
             zookeeperOptions.OnChange((zookopt,s)=>{
                _zookeeperOptions=zookopt; 
            });
            _logger.LogDebug("开始连接");
            Conn(_zookeeperOptions); 
        }

       

        private void Conn(ZookeeperOptions zookeeperOptions)
        {
            bool isReadOnly=default(Boolean);
            Wathcer=new WaitConnWatch(autoResetEvent[0],_logger);
            if(isReadOnly!=zookeeperOptions.IsReadOnly)
            {
                isReadOnly=zookeeperOptions.IsReadOnly;
            }

            
            byte[] pwd=new byte[0];
            //如果没有密码和sessionId
            if(string.IsNullOrEmpty(zookeeperOptions.SessionPwd)
            &&_zookeeperOptions.SessionId==default(int))
            {
             _zookeeper=new ZooKeeper(zookeeperOptions.Connectstring,zookeeperOptions.SessionTimeout,Wathcer,isReadOnly);
            }
            else if (!string.IsNullOrEmpty(zookeeperOptions.SessionPwd))
            {
                pwd=System.Text.Encoding.Default.GetBytes(zookeeperOptions.SessionPwd);
                 _zookeeper=new ZooKeeper(zookeeperOptions.Connectstring,zookeeperOptions.SessionTimeout,Wathcer,0,pwd,isReadOnly);
            }
            else
            {
                 _zookeeper=new ZooKeeper(zookeeperOptions.Connectstring
                 ,zookeeperOptions.SessionTimeout,Wathcer,zookeeperOptions.SessionId,pwd,isReadOnly);
            }
             if(_zookeeper.getState()==States.CONNECTING)
            {
                _logger.LogDebug("当前状态：CONNECTING。阻塞等待");
                autoResetEvent[0].WaitOne();
            }
        }

        public Task<string> CreateZNode(string path,string data,CreateMode createMode,List<ACL> aclList)
        {
            ReConn();
            if(string.IsNullOrEmpty(path)||!path.StartsWith('/'))
            {
                _logger.LogDebug("path路径非法，参数：path：{0}",path);
                return null;
            }
            byte[] dat=new byte[0];
            if(string.IsNullOrEmpty(data))
            { 
                dat=System.Text.Encoding.Default.GetBytes(data);
            }
            if(createMode==null)
            {
                 _logger.LogDebug("createMode为null,默认使用CreateMode.PERSISTENT");
                createMode=CreateMode.PERSISTENT;
            }
            return _zookeeper.createAsync(path,dat,aclList,createMode);
        }

        public Task<DataResult> GetDataAsync(string path,Watcher watcher,bool isSync)
        {
            ReConn();
            if(_zookeeper.existsAsync(path).Result==null )
            {
                _logger.LogDebug("path不存在");
                return null;
            }
            if(isSync)
            {
                 _logger.LogDebug("即将进行同步。"); 
                 var task=Task.Run(async ()=>{
                     try
                     {
                         await _zookeeper.sync(path);  
                    }
                    catch(Exception ep)
                    {
                       _logger.LogError("同步失败。",ep);
                       return;  
                    }
                 }); 
                task.Wait();
            }
           

            return _zookeeper.getDataAsync(path,watcher);
        }

         public Task<Stat> SetDataAsync(string path,string data,bool isSync)
        {
            ReConn();
            if(_zookeeper.existsAsync(path).Result==null )
            {
                 _logger.LogDebug("path不存在");
                return null;
            }
            byte[] dat=new byte[0];
            if(!string.IsNullOrEmpty(data))
            { 
                dat=System.Text.Encoding.Default.GetBytes(data);
            }
            return _zookeeper.setDataAsync(path,dat);
        }

         public async Task<ChildrenResult> GetChildrenAsync(string path,Watcher watcher,bool isSync) 
         {
             ReConn();
              if(_zookeeper.existsAsync(path).Result==null )
            {
                 _logger.LogDebug("path不存在");
                return null;
            }
             if(isSync)
            {
                 _logger.LogDebug("即将进行同步。");
                 var task=Task.Run(async  ()=>{
                     try
                     {
                        _logger.LogDebug("开始同步");
                        await _zookeeper.sync(path);  
                      }
                      catch(Exception ep)
                      {
                          _logger.LogError("同步失败。",ep);
                          return;
                      }
                 });
                task.Wait();
            }
             return await _zookeeper.getChildrenAsync(path,watcher);
         }

         public void DeleteNode(string path,String tempNode)
         {
             if(!string.IsNullOrEmpty(tempNode))
             {
                requestLockSequence.Remove(tempNode); 
             }
             ReConn();
              if(_zookeeper.existsAsync(path).Result==null )
            {
                 _logger.LogDebug("path不存在");
                return;
            }
            var  task=Task.Run(async ()=>{
                try
                {
                 _logger.LogDebug("删除node：{0}",path);
                  await _zookeeper.deleteAsync(path);
                }
                catch(Exception ep)
                {
                    _logger.LogError("删除失败",ep);
                    return;
                }
            });
            task.Wait();
            var sequencePath=requestLockSequence.Where(w=>path==w).FirstOrDefault();
            if(sequencePath!=null)
            {
                requestLockSequence.Remove(sequencePath);
            }
         }

         public  string GetDataByLockNode(string path,string sequenceName,List<ACL> aclList,out string tempNodeOut)
         {
             _logger.LogInformation("获取分布式锁开始。");
             ReConn();
             string tempNode=string.Empty;
             tempNodeOut=string.Empty;

              if(_zookeeper.existsAsync(path).Result==null )
            {
                 _logger.LogDebug("path不存在");
                return null;
            }

            
            try
            {
                _logger.LogDebug("开始锁定语句块");
                lock(_lock)
                {
                     _logger.LogDebug("锁定，访问requestLockSequence的代码应该同步。");
                    tempNode=requestLockSequence
                    .Where(w=>w.StartsWith(path+"/"+sequenceName)).FirstOrDefault();
                   
                    if(tempNode==null)
                    {
                        tempNode=CreateZNode(path+"/"+sequenceName,"",CreateMode.EPHEMERAL_SEQUENTIAL,aclList).Result;
                        _logger.LogDebug("创建节点：{0}",tempNode);
                        if(tempNode==null)
                        {
                            _logger.LogDebug("创建临时序列节点失败。详细参数:path:{0},data:{1},CreateMode:{2}"
                            ,path+"/squence","",CreateMode.EPHEMERAL_SEQUENTIAL);
                            return null;
                        }
                         _logger.LogInformation("创建成功，加入requestLockSequence列表。");
                        requestLockSequence.Add(tempNode);
                    }
                    else
                    {
                        _logger.LogDebug("已经存在的锁节点，返回null");
                        return null;
                    }
                }

                var childList= GetChildrenAsync(path,null,true).Result;
                   if(childList==null||childList.Children==null||childList.Children.Count<1)
                   {
                        _logger.LogDebug("获取子序列失败，计数为零.path:{0}",path);
                        return null;
                   }
                   _logger.LogDebug("获取path:{0}的子节点：{1}",path,Newtonsoft.Json.JsonConvert.SerializeObject(childList.Children));
                   var top=childList.Children.OrderBy(or=>or).First();
                   byte[] da=null;
                   if(path+"/"+top==tempNode)
                   {
                       tempNodeOut =tempNode;
                       da= GetDataAsync(path,null,true).Result.Data;
                        if(da==null||da.Length<1)
                        {
                            return string.Empty;
                        } 
                        return System.Text.Encoding.Default.GetString(da);
                   }
                   else
                   {
                    childList= GetChildrenAsync(path,new WaitLockWatch(autoResetEvent[1],this,_logger,path,tempNode),true).Result;
                    autoResetEvent[1].WaitOne();
                   }
                    _logger.LogDebug("继续执行。");
                    tempNodeOut =tempNode;
                    da= GetDataAsync(path,null,true).Result.Data;
                    if(da==null||da.Length<1)
                    {
                         return string.Empty;
                    }
                    return System.Text.Encoding.Default.GetString(da);
            }
            catch(Exception ep)
            {
                 _logger.LogError(ep,"获取同步锁出现错误。");
                if(!string.IsNullOrEmpty(tempNode))
                {
                    DeleteNode(tempNode,tempNode);  
                }
            }
            return null;
         }

         private void ReConn()
         {
             _logger.LogInformation("检查连接状态");
             if(_zookeeper.getState()==States.CLOSED
             ||_zookeeper.getState()== States.NOT_CONNECTED)
             {
                 _logger.LogInformation("连接为关闭，开始重新连接。");
                Conn(_zookeeperOptions);
             }
         }

         public void Close(string tempNode)
         {
             var task=Task.Run(async ()=>{ 
                 try
                 {
                     requestLockSequence.Remove(tempNode); 
                    await _zookeeper.closeAsync();
                 }
                 catch(Exception ep)
                 {
                      _logger.LogError("zookeeper关闭失败。",ep);
                 }

             });
             task.Wait(); 
         }
 
    }
}