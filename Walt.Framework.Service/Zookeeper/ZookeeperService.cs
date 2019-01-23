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
            string path = @event.getPath();
            var type = @event.get_Type();
            try
            {

                if (type == EventType.NodeDeleted)
                {
                    var childList = _zookeeperService.GetChildrenAsync(_path, null, true).Result;
                    if (childList == null || childList.Children == null || childList.Children.Count < 1)
                    {
                        _logger.LogInformation("获取子序列失败，计数为零.path:{0}", _path);
                        return Task.FromResult(true);
                    }
                    var top = childList.Children.OrderBy(or => or).First();
                    _logger.LogInformation("{0}节点发生改变，激发监视方法。", _path + "/" + top);
                    if (_path + "/" + top == _tempNode)
                    {
                        _logger.LogInformation("释放阻塞");
                        _autoResetEvent.Set();
                        return Task.FromResult(true);
                    }
                     _zookeeperService.SetWatcher(_path + "/" + top
                    , new WaitLockWatch(_autoResetEvent, _zookeeperService, _logger, path, _tempNode)).GetAwaiter().GetResult();
                }
            }
            catch(KeeperException kep)
            {
                 _logger.LogError(0,kep,"{0}节点发生改变，激发监视方法。但是出现错误", path);
                 throw kep;
            }
            catch (Exception ep)
            {
                _logger.LogError(0,ep,"{0}节点发生改变，激发监视方法。但是出现错误", path);
            }
            return Task.FromResult(true);
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
           _logger.LogInformation("watch激发,回掉状态：{0}",@event.getState().ToString());
            if(@event.getState()== KeeperState.SyncConnected
            ||@event.getState()== KeeperState.ConnectedReadOnly)
            {
                _logger.LogInformation("释放阻塞");
                _autoResetEvent.Set();
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

         AutoResetEvent[] autoResetEvent=new AutoResetEvent[2]
         {new AutoResetEvent(false),new AutoResetEvent(false)};

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

        public async void DeleteNode(string path,string tempNode)
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
             if(await _zookeeper.existsAsync(path)==null )
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

                //GetChild: //这里防止并发出现错误。
                var taskGetData=Task.Run(async () =>{
                    int circleCount = 0;
                    while (true)
                    {
                        Thread.Sleep(200);
                        circleCount++;
                        _logger.LogInformation("循环获取锁。当前循环次数:{0}", circleCount);
                        try
                        {
                            var childList =await GetChildrenAsync(path, null, true);
                            if (childList == null || childList.Children == null || childList.Children.Count < 1)
                            {
                                _logger.LogWarning("获取子序列失败，计数为零.path:{0}", path);
                                return null;
                            }
                            _logger.LogInformation("获取path:{0}的子节点：{1}", path, Newtonsoft.Json.JsonConvert.SerializeObject(childList.Children));

                            var top = childList.Children.OrderBy(or => or).First();
                            if (path + "/" + top == tempNode)
                            {
                                return tempNode;
                            }
                        }
                        catch (Exception ep)
                        {
                            _logger.LogError(ep,"循环获取锁出错。");
                            return null;
                        }
                    }
                });
                tempNode = taskGetData.GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(tempNode))
                {
                    byte[] da = null;
                    tempNodeOut = tempNode;
                    da = GetDataAsync(path, null, true).Result.Data;
                    if (da == null || da.Length < 1)
                    {
                        return string.Empty;
                    }
                    return System.Text.Encoding.Default.GetString(da);
                }

                // bool isSet=
                //     SetWatcher(path + "/" + top,new WaitLockWatch(autoResetEvent[1], this, _logger, path, tempNode)).Result;
                // if(!isSet)
                // {
                //     goto GetChild;
                // }
                //autoResetEvent[1].WaitOne();
                // _logger.LogDebug("继续执行。");
                // tempNodeOut = tempNode;
                // da = GetDataAsync(path, null, true).Result.Data;
                // if (da == null || da.Length < 1)
                // {
                //     return string.Empty;
                // }

                // return System.Text.Encoding.Default.GetString(da);
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
                    DeleteNode(tempNode, tempNode);
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