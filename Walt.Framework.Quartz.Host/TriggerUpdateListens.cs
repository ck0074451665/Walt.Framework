using System; 
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Logging; 
using Quartz.Impl;
using Walt.Framework.Service.Zookeeper;
using Microsoft.Extensions.DependencyInjection;
using org.apache.zookeeper;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using static org.apache.zookeeper.KeeperException;

namespace Walt.Framework.Quartz.Host
{

    public class TriggerUpdateListens : ITriggerListener
    {
        public string Name { get; set; }

        public TriggerUpdateListens(string name)
        {
            Name = name;
        }

        private bool VoteJob{ get; set;}

        public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = default(CancellationToken))
        {
            ILoggerFactory loggerFact = Program.Host.Services.GetService<ILoggerFactory>();
            var  _logger=loggerFact.CreateLogger<ZookeeperService>();
             _logger.LogInformation(0, null, "执行成功.name:{0} group：{1}", context.JobDetail.Key.Name, context.JobDetail.Key.Group);
            return Task.FromResult(true);
        }

        public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            ILoggerFactory loggerFact = Program.Host.Services.GetService<ILoggerFactory>();
            var  _logger=loggerFact.CreateLogger<ZookeeperService>();
            _logger.LogInformation(0, null, "开始执行job.name:{0} group：{1}", context.JobDetail.Key.Name, context.JobDetail.Key.Group);
            string machine = Environment.MachineName;
            try
            {
                var customerAttri = context.JobDetail.JobType.GetCustomAttributes(false);
                foreach (var customer in customerAttri)
                {
                    if (customer is DistributingAttributes)
                    {
                        var distri = customer as DistributingAttributes;
                        var zookeeper = Program.Host.Services.GetService<IZookeeperService>();
                        string currentTempNodeName = string.Empty;
                        string fullPath = "/lock/"+ context.JobDetail.Key.Name + context.JobDetail.Key.Group;
                        int flag = 0;
                    //Repeat:
                        string jsonData = zookeeper.GetDataByLockNode(fullPath, "getlock"
                        , ZooDefs.Ids.OPEN_ACL_UNSAFE, out currentTempNodeName);
                        if(string.IsNullOrEmpty(currentTempNodeName))
                        {
                            _logger.LogError("获取锁失败。节点：{0},锁前缀：{1},重试：{2}",fullPath,"getlock",flag);
                            // if(flag<=2)
                            // {
                            //     flag = flag + 1;
                            //     goto Repeat;
                            // }
                            VoteJob = true;
                             _logger.LogError("获取分片失败，取消job执行，等待下次。");
                            return Task.FromResult(false);
                            //context.Scheduler.Interrupt(context.JobDetail.Key);
                        }

                        QuartzDbContext db = Program.Host.Services.GetService<QuartzDbContext>();
                         var item = db.QuartzTask.Where(w => w.IsDelete == 0
                        && w.TaskName == context.JobDetail.Key.Name
                        && w.GroupName == context.JobDetail.Key.Group
                        && w.MachineName == machine
                        && w.InstanceId == context.Scheduler.SchedulerInstanceId).FirstOrDefault();
                        
                        if (item != null)
                        {
                            //TODO 这里可以找出机器名，拼接处api，可以查看主机是否存活，从而将一些挂起的任务重新分配。
                        }
                        string distributeFlag = item.MachineName + item.InstanceId;
                        List<DistributingData> distriData = new List<DistributingData>();
                        DistributingData currentDistriEntity = new DistributingData();
                        if (string.IsNullOrEmpty(jsonData))
                        {
                            currentDistriEntity= new DistributingData
                            {
                                DistributeFlag =distributeFlag,
                                PageIndex = 1,
                                PageSize = Program.QuartzOpt.CustomerRecordCountForTest //配置
                            };
                            distriData.Add(currentDistriEntity);
                        }
                        else
                        {
                            distriData = Newtonsoft.Json.JsonConvert.DeserializeObject<List<DistributingData>>(jsonData);
                            if (distriData == null || distriData.Count() < 1)
                            {
                                currentDistriEntity= new DistributingData
                                {
                                    DistributeFlag =distributeFlag,
                                    PageIndex = 1,
                                    PageSize = Program.QuartzOpt.CustomerRecordCountForTest //配置
                                };
                                distriData.Add(currentDistriEntity);
                            }
                            else
                            {
                                currentDistriEntity= distriData.Where(w => w.DistributeFlag == distributeFlag).SingleOrDefault();
                                if (currentDistriEntity == null)
                                {
                                    var maxPageIndex = distriData.Max(w => w.PageIndex);
                                    maxPageIndex = maxPageIndex + 1;
                                    var entity = new DistributingData
                                    {
                                        DistributeFlag = distributeFlag,
                                        PageIndex = maxPageIndex,
                                        PageSize = Program.QuartzOpt.CustomerRecordCountForTest //配置
                                    };
                                    distriData.Add(entity);
                                }
                                else
                                {
                                    var maxPageIndex = distriData.Max(w => w.PageIndex);
                                    maxPageIndex = maxPageIndex + 1;
                                    currentDistriEntity.PageIndex = maxPageIndex;
                                }
                            }
                        }
                        item.Remark = Newtonsoft.Json.JsonConvert.SerializeObject(currentDistriEntity);
                        db.Update(item);
                        db.SaveChanges();
                        string resultData = Newtonsoft.Json.JsonConvert.SerializeObject(distriData);
                        context.JobDetail.JobDataMap.Put("distriData", currentDistriEntity);

                        zookeeper.SetDataAsync(fullPath
                            , resultData, false).GetAwaiter().GetResult();
                        zookeeper.DeleteNode(currentTempNodeName);
                        _logger.LogInformation("分片执行：{0}",resultData);
                    }
                }
            }
             catch(ConnectionLossException cle)
            {
                VoteJob = true;
                _logger.LogError(cle, "获取同步锁出现错误。连接丢失");
            }
            catch(SessionExpiredException sep)
            {
                VoteJob = true;
                _logger.LogError(sep, "获取同步锁出现错误。连接过期");
            }
            catch(KeeperException kep)
            {
                VoteJob = true;
                _logger.LogError(kep, "获取同步锁出现错误。操作zookeeper出错");
            }
            catch(Exception ep)
            {
               
                try
                {
                     _logger.LogError(0,ep,"分片失败。");
                    //context.Scheduler.DeleteJob(context.JobDetail.Key).GetAwaiter().GetResult();
                    VoteJob = true;
                     QuartzDbContext db = Program.Host.Services.GetService<QuartzDbContext>();
                    var item = db.QuartzTask.Where(w => w.IsDelete == 0
                     && w.TaskName == context.JobDetail.Key.Name
                     && w.GroupName == context.JobDetail.Key.Group
                     && w.MachineName == machine
                     && w.InstanceId == context.Scheduler.SchedulerInstanceId).FirstOrDefault();
                    if (item == null)
                    {
                        _logger.LogError(0, ep, "分片失败，获取数据库记录失败。");
                    }
                    else
                    {
                        
                        item.Status = (int)TaskStatus.Canceled;
                        item.OperateStatus = (int)OperateStatus.Stop;
                        item.Remark = ep.ToString();
                        db.Update(item);
                        db.SaveChanges();
                    }
                }
                catch (Exception eep)
                {
                     _logger.LogError(0, eep, "分片失败，更新数据库失败。");
                }
            }
            return Task.FromResult(true);
        }

        public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            ILoggerFactory loggerFact = Program.Host.Services.GetService<ILoggerFactory>();
            var  _logger=loggerFact.CreateLogger<ZookeeperService>();
            if (VoteJob)
            {
                _logger.LogInformation(0, null, "取消执行job.name:{0} group：{1}", context.JobDetail.Key.Name, context.JobDetail.Key.Group);
            }
            return Task.FromResult(VoteJob);
        }
    }

}