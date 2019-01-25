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
using static org.apache.zookeeper.ZooDefs;
using static org.apache.zookeeper.KeeperException;

namespace  Walt.Framework.Service.Zookeeper
{

    internal class WaitLockWatch : Watcher
    {
        private AutoResetEvent _autoResetEvent;

        private ManualResetEvent _mutex;
        private ILogger _logger;

        private string _path;

        public WaitLockWatch(AutoResetEvent autoResetEvent
        , ILogger logger, string path
        , ManualResetEvent mutex)
        {
            _autoResetEvent = autoResetEvent;
            _logger = logger;
            _path = path;
            _mutex = mutex;
        }

        public override Task process(WatchedEvent @event)
        {
             _mutex.Set();
            return Task.FromResult(true);
        }
    }


    internal class WaitConnWatch : Watcher
    {
        private AutoResetEvent _autoResetEvent;
        private ILogger _logger;

        private ManualResetEvent _mutex;

        public WaitConnWatch(AutoResetEvent autoResetEvent
        ,ILogger logger
        ,ManualResetEvent mutex)
        {
            _autoResetEvent=autoResetEvent;
            _logger=logger;
            _mutex = mutex;
        }

       public override Task process(WatchedEvent @event)
       {
           _logger.LogInformation("watch激发,回掉状态：{0}",@event.getState().ToString());
            if(@event.getState()== KeeperState.SyncConnected
            ||@event.getState()== KeeperState.ConnectedReadOnly)
            {
                _logger.LogInformation("释放连接阻塞");
                _autoResetEvent.Set();
            }
            else
            {
                _logger.LogInformation("连接断开，释放分布式锁阻塞");
                _mutex.Set();
            }
            return Task.FromResult(0);
       }
    }

    public class ZookeeperService : IZookeeperService
    {
        private ZookeeperOptions _zookeeperOptions;
        private ZooKeeper _zookeeper;

         private static readonly byte[] NO_PASSWORD = new byte[0];

         public Watcher Wathcer {get;set;}

         public ILoggerFactory LoggerFac { get; set; }

         private ILogger _logger;

        internal Thread CurrThread{ get; }



        AutoResetEvent[] autoResetEvent=new AutoResetEvent[2]
         {new AutoResetEvent(false),new AutoResetEvent(false)};
        ManualResetEvent _manualReset = new ManualResetEvent(false);
        public ZookeeperService(IOptionsMonitor<ZookeeperOptions>  zookeeperOptions
        ,ILoggerFactory loggerFac)
        {
            LoggerFac=loggerFac;
            _logger=LoggerFac.CreateLogger<ZookeeperService>();
            _zookeeperOptions=zookeeperOptions.CurrentValue; 
            _logger.LogInformation("配置参数：{0}",JsonConvert.SerializeObject(_zookeeperOptions));
             zookeeperOptions.OnChange((zookopt,s)=>{
                _zookeeperOptions=zookopt; 
            });
            _logger.LogInformation("开始连接");
            Conn(_zookeeperOptions); 
            CurrThread = System.Threading.Thread.CurrentThread;
        }

       

        private void Conn(ZookeeperOptions zookeeperOptions)
        {
            bool isReadOnly=default(Boolean);
            Wathcer=new WaitConnWatch(autoResetEvent[0],_logger,_manualReset);
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
                _logger.LogInformation("当前状态：CONNECTING。阻塞等待");
                autoResetEvent[0].WaitOne();
            }
        }

        public Task<string> CreateZNode(string path,string data,CreateMode createMode,List<ACL> aclList)
        {
            ReConn();
            if(string.IsNullOrEmpty(path)||!path.StartsWith('/'))
            {
                _logger.LogInformation("path路径非法，参数：path：{0}",path);
                return null;
            }
            byte[] dat=new byte[0];
            if(string.IsNullOrEmpty(data))
            { 
                dat=System.Text.Encoding.Default.GetBytes(data);
            }
            if(createMode==null)
            {
                 _logger.LogInformation("createMode为null,默认使用CreateMode.PERSISTENT");
                createMode=CreateMode.PERSISTENT;
            }
            return _zookeeper.createAsync(path,dat,aclList,createMode);
        }

        public async void Sync(string path)
        {
            try
            {
                _logger.LogInformation("同步成功");
                 await _zookeeper.sync(path);
            }
            catch (Exception ep)
            {
                _logger.LogError("同步失败。", ep);
            }
        }

        public async Task<DataResult> GetDataAsync(string path,Watcher watcher,bool isSync)
        {
            ReConn();
            if(await _zookeeper.existsAsync(path)==null )
            {
                _logger.LogInformation("path不存在");
                return null;
            }
            if (isSync)
            {
                _logger.LogInformation("即将进行同步。");
                try
                {
                   await  _zookeeper.sync(path);
                    _logger.LogInformation("同步成功");
                }
                catch (Exception ep)
                {
                    _logger.LogError("同步失败。", ep);
                }
            }
            return await _zookeeper.getDataAsync(path,watcher);
        }

         public async Task<Stat> SetDataAsync(string path,string data,bool isSync)
        {
            ReConn();
            if(await _zookeeper.existsAsync(path)==null )
            {
                 _logger.LogInformation("path不存在");
                return null;
            }
            byte[] dat=new byte[0];
            if(!string.IsNullOrEmpty(data))
            { 
                dat=System.Text.Encoding.Default.GetBytes(data);
            }
            return await _zookeeper.setDataAsync(path,dat);
        }

        public async Task<ChildrenResult> GetChildrenAsync(string path, Watcher watcher, bool isSync)
        {
            ReConn();
            if (await _zookeeper.existsAsync(path) == null)
            {
                _logger.LogInformation("path不存在");
                return null;
            }
            if (isSync)
            {
                _logger.LogInformation("即将进行同步。");
                try
                {
                    _logger.LogInformation("开始同步");
                    await _zookeeper.sync(path);
                    _logger.LogInformation("同步成功");
                }
                catch (Exception ep)
                {
                    _logger.LogError("同步失败。", ep);
                }
            }
            return await _zookeeper.getChildrenAsync(path, watcher);
        }

        public async Task DeleteNode(string path)
         {
             ReConn();
              if(await _zookeeper.existsAsync(path)==null )
            {
                 _logger.LogDebug("删除path：path不存在");
                return;
            }
            try
            {
                _logger.LogDebug("删除node：{0}", path);
                await _zookeeper.deleteAsync(path);
            }
            catch (Exception ep)
            {
                _logger.LogError("删除失败", ep);
                return;
            }
        }

        public async Task<bool> SetWatcher(string path,Watcher watcher)
        {
            ReConn();
            var stat = await _zookeeper.existsAsync(path);
            if(stat==null )
            {
                 _logger.LogDebug("判断path是否存在：path不存在");
                return false;
            }
             try
            {
                _logger.LogDebug("设置监控：{0}", path);
                await _zookeeper.getDataAsync(path,watcher);
                return true;
            }
            catch (Exception ep)
            {
                _logger.LogError("设置监控错误", ep);
                return false;
            }
        }

         public  string GetDataByLockNode(string path,string sequenceName,List<ACL> aclList,out string tempNodeOut)
         {
             _logger.LogInformation("获取分布式锁开始。");
             string tempNode=string.Empty;
             tempNodeOut=string.Empty;
            try
            {

                ReConn();
                if (_zookeeper.existsAsync(path).Result == null)
                {
                    _logger.LogDebug("path不存在，创建");
                    CreateZNode(path, "", CreateMode.PERSISTENT, aclList).GetAwaiter().GetResult();
                }


                tempNode = CreateZNode(path + "/" + sequenceName, "", CreateMode.EPHEMERAL_SEQUENTIAL, aclList).Result;
                _logger.LogDebug("创建节点：{0}", tempNode);
                if (tempNode == null)
                {
                    _logger.LogDebug("创建临时序列节点失败。详细参数:path:{0},data:{1},CreateMode:{2}"
                    , path + "/squence", "", CreateMode.EPHEMERAL_SEQUENTIAL);
                    return null;
                }
                _logger.LogInformation("创建成功。");


                // var taskGetData=Task.Run(async () =>{
                //     int circleCount = 0;
                //     while (true)
                //     {
                //         Thread.Sleep(200);
                //         circleCount++;
                //         _logger.LogInformation("循环获取锁。当前循环次数:{0}", circleCount);
                //         try
                //         {
                //             var childList =await GetChildrenAsync(path, null, true);
                //             if (childList == null || childList.Children == null || childList.Children.Count < 1)
                //             {
                //                 _logger.LogWarning("获取子序列失败，计数为零.path:{0}", path);
                //                 return null;
                //             }
                //             _logger.LogInformation("获取path:{0}的子节点：{1}", path, Newtonsoft.Json.JsonConvert.SerializeObject(childList.Children));

                //             var top = childList.Children.OrderBy(or => or).First();
                //             if (path + "/" + top == tempNode)
                //             {
                //                 return tempNode;
                //             }
                //         }
                //         catch (Exception ep)
                //         {
                //             _logger.LogError(ep,"循环获取锁出错。");
                //             return null;
                //         }
                //     }
                // });
                // tempNode = taskGetData.GetAwaiter().GetResult();
                // if (!string.IsNullOrEmpty(tempNode))
                // {
                //     byte[] da = null;
                //     tempNodeOut = tempNode;
                //     da = GetDataAsync(path, null, true).Result.Data;
                //     if (da == null || da.Length < 1)
                //     {
                //         return string.Empty;
                //     }
                //     return System.Text.Encoding.Default.GetString(da);
                // }
                int clycleCount = 0;
            GetChild: //这里防止并发出现错误。
                clycleCount++;
                var childList = GetChildrenAsync(path, null, true).GetAwaiter().GetResult();
                if (childList == null || childList.Children == null || childList.Children.Count < 1)
                {
                    _logger.LogWarning("获取子序列失败，计数为零.path:{0}", path);
                    return null;
                }
                _logger.LogInformation("获取path:{0}的子节点：{1}", path, Newtonsoft.Json.JsonConvert.SerializeObject(childList.Children));

                var top = childList.Children.OrderBy(or => or).First();
                if (path + "/" + top == tempNode)
                {
                    tempNodeOut = tempNode;
                    var da = GetDataAsync(path, null, true).Result.Data;
                    if (da == null || da.Length < 1)
                    {
                        return string.Empty;
                    }
                    return System.Text.Encoding.Default.GetString(da);
                }
                // bool isSet=
                //     SetWatcher(path + "/" + top,).Result;
                // if(!isSet)
                // {
                //     goto GetChild;
                // }
                bool isSet= SetWatcher(path + "/" + top,new WaitLockWatch(autoResetEvent[1], _logger, path,_manualReset)).Result;
                if(!isSet)
                {
                    _logger.LogWarning("没有设置上watcher，需要重新运行一遍。");
                    goto GetChild;
                }
                _manualReset.WaitOne(15000);
                 childList = GetChildrenAsync(path, null, true).GetAwaiter().GetResult();
                if (childList == null || childList.Children == null || childList.Children.Count < 1)
                {
                    _logger.LogWarning("再次获取子序列失败，计数为零.path:{0}", path);
                    return null;
                }
                _logger.LogInformation("再次获取path:{0}的子节点：{1}", path, Newtonsoft.Json.JsonConvert.SerializeObject(childList.Children));
                top = childList.Children.OrderBy(or => or).First();
                if (path + "/" + top == tempNode)
                {
                    _logger.LogDebug("节点获取到锁权限。");
                    tempNodeOut = tempNode;
                    var da = GetDataAsync(path, null, true).Result.Data;
                    if (da == null || da.Length < 1)
                    {
                        return string.Empty;
                    }
                    return System.Text.Encoding.Default.GetString(da);
                }
                else
                {
                    _logger.LogDebug("没有获取到锁权限，进行循环。循环第：{0}次",clycleCount);
                    Thread.Sleep(1000);
                    goto GetChild;
                    // Sync(path);
                    
                    //DeleteNode(tempNode).GetAwaiter().GetResult();
                    // DeleteNode(tempNode).GetAwaiter().GetResult();
                    //  _logger.LogError("没有获取到锁，Watcher出现问题，请查看日志。");
                    // if (_zookeeper.existsAsync(tempNode).Result== null)
                    // {
                    //     _logger.LogWarning("tempNode:{0}存在,但是没有获取到锁，在等待的时候，被线程检查程序释放了阻塞，属于误伤"
                    //     ,tempNode);

                    // }
                    // else
                    // {
                    //     _logger.LogError("没有获取到锁，Watcher出现问题，请查看日志。");
                    // }
                }

            }
            catch(ConnectionLossException cle)
            {
                _logger.LogError(cle, "获取同步锁出现错误。连接丢失");
            }
            catch(SessionExpiredException sep)
            {
                _logger.LogError(sep, "获取同步锁出现错误。连接过期");
            }
            catch(KeeperException kep)
            {
                _logger.LogError(kep, "获取同步锁出现错误。操作zookeeper出错");
            }
            catch (Exception ep)
            {
                _logger.LogError(ep, "获取同步锁出现错误。");
                if (!string.IsNullOrEmpty(tempNode))
                {
                    try{
                    DeleteNode(tempNode).GetAwaiter().GetResult();
                    }catch(Exception)
                    {
                        
                    }
                }
            }

            return null;
         }

         private void ReConn()
         {
             _logger.LogInformation("检查连接状态，status:{0}",_zookeeper.getState());
             if(_zookeeper.getState()==States.CLOSED
             ||_zookeeper.getState()== States.NOT_CONNECTED)
             {
                 _logger.LogInformation("连接为关闭，开始重新连接。");
                Conn(_zookeeperOptions);
             }
         }

        public async void Close(string tempNode)
        {
            try
            {
                await _zookeeper.closeAsync();
            }
            catch (Exception ep)
            {
                _logger.LogError("zookeeper关闭失败。", ep);
            }
        }

    }
}