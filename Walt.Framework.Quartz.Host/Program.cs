using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Walt.Framework.Log;
using Walt.Framework.Service;
using Walt.Framework.Configuration;
using MySql.Data;
using MySql.Data.EntityFrameworkCore;
using Walt.Framework.Core;
using System.Linq;

using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using Microsoft.Extensions.DependencyInjection;
using Quartz.Impl.Matchers;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;

namespace Walt.Framework.Quartz.Host
{
    public class Program
    {

        private ILoggerFactory _loggerFact;

        public static IHost Host { get; set; }

        public static String QUARTZ_INSTANCE_ID = "PREFIX_QUARTZ_INSTANCE_ID";

        public static QuartzOption QuartzOpt { get; set; }
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                    .UseEnvironment(EnvironmentName.Development)
                    .ConfigureAppConfiguration((hostContext, configApp) =>
                    {
                        configApp.SetBasePath(Directory.GetCurrentDirectory());
                        configApp.AddJsonFile(
                              $"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json",
                                 optional: true);
                        configApp.AddEnvironmentVariables("PREFIX_");
                        configApp.AddCommandLine(args);
                        var rootConfiguration = configApp.Build();
                        QuartzOpt = new QuartzOption();
                        rootConfiguration.GetSection("Quartz").Bind(QuartzOpt); 
                    }).ConfigureLogging((hostContext, configBuild) =>
                    {
                        configBuild.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                        configBuild.AddConsole();
                        configBuild.AddCustomizationLogger();
                    })
                    .ConfigureServices((hostContext, service) =>
                    {
                        service.AddKafka(KafkaBuilder =>
                        {
                            KafkaBuilder.AddConfiguration(hostContext.Configuration.GetSection("KafkaService"));
                        });
                        service.AddZookeeper(zookeeperBuilder =>
                        {
                            zookeeperBuilder.AddConfiguration(hostContext.Configuration.GetSection("zookeeperService"));
                        });
                        service.AddDbContext<QuartzDbContext>(option =>
                        option.UseMySQL(hostContext.Configuration.GetConnectionString("QuartzDatabase")), ServiceLifetime.Transient, ServiceLifetime.Transient);
                    })
                    .Build();


            Host = host;
            ILoggerFactory loggerFact = host.Services.GetService<ILoggerFactory>();

            LogProvider.SetCurrentLogProvider(new ConsoleLogProvider(loggerFact));
            var ischema = RunProgramRunExample(loggerFact);
            host.WaitForShutdown();
            ischema.Shutdown(true);
        }

        private static IScheduler RunProgramRunExample(ILoggerFactory loggerFact)
        {
            var log = loggerFact.CreateLogger<Program>();
            try
            {

                var config = Host.Services.GetService<IConfiguration>();
                // Grab the Scheduler instance from the Factory
                NameValueCollection properties = new NameValueCollection
                {
                    ["quartz.scheduler.instanceName"] = QuartzOpt.InstanceName,
                    ["quartz.scheduler.instanceId"] = QuartzOpt.InsatanceId,
                    ["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz",
                    ["quartz.threadPool.threadCount"] = "5",
                    ["quartz.jobStore.misfireThreshold"] = "60000",
                    ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
                    ["quartz.jobStore.useProperties"] = "false",
                    ["quartz.jobStore.dataSource"] = "default",
                    ["quartz.jobStore.tablePrefix"] = "QRTZ_",
                    ["quartz.jobStore.clustered"] = "true",
                    ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.MySQLDelegate, Quartz",
                    ["quartz.dataSource.default.connectionString"] = config.GetConnectionString("QuatrzClustDatabase"),
                    ["quartz.dataSource.default.provider"] = "MySql",
                    ["quartz.serializer.type"] = "json",
                    ["quartz.scheduler.exporter.type"] = "Quartz.Simpl.RemotingSchedulerExporter, Quartz",
                    ["quartz.scheduler.exporter.port"] = "555",
                    ["quartz.scheduler.exporter.bindName"] = "QuartzScheduler",
                    ["quartz.scheduler.exporter.channelType"] = "tcp",
                    ["quartz.scheduler.exporter.channelName"] = "httpQuartz",
                    ["quartz.scheduler.exporter.rejectRemoteRequests"] = "true"
                };
                StdSchedulerFactory factory = new StdSchedulerFactory(properties);

                IScheduler scheduler = factory.GetScheduler().GetAwaiter().GetResult();
                 
                 string machine = Environment.MachineName;
                 QuartzDbContext db = Host.Services.GetService<QuartzDbContext>();
                var listQuartzTask = db.QuartzTask.Where(w => w.IsDelete == 0
                && w.MachineName == machine && w.InstanceId == QuartzOpt.InsatanceId)
                .ToListAsync().GetAwaiter().GetResult();

                log.LogDebug("从数据库获取task记录,详细信息:{0}", Newtonsoft.Json.JsonConvert.SerializeObject(listQuartzTask));
                Dictionary<string,Assembly> collAssembly=new Dictionary<string, Assembly>();
                foreach (var item in listQuartzTask)
                {
                    //加载程序集
                    if (!string.IsNullOrEmpty(item.AssemblyName) && !collAssembly.ContainsKey(item.AssemblyName))
                    {
                        try
                        {
                            collAssembly[item.AssemblyName] =
                            AssemblyHelp.GetAssemblyByteByAssemblyName(
                                Path.Combine(Directory.GetCurrentDirectory(), "AssemblyColl"), item.AssemblyName);
                        }
                        catch (Exception ep)
                        {
                            log.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, ep, "没有找到程序集.");
                            Task.Delay(10000);
                            continue;
                        }
                    }
                }

                // and start it off
                scheduler.Start();
                // if (!QuartzOpt.IsSlave)
                // {
                    var task = Task.Run(() =>
                    {
                        bool isClear = QuartzOpt.IsClear;
                        log.LogInformation("job监控程序开始循环，间隔为15秒");

                        while (true)
                        {
                            try
                            {
                                if (scheduler != null)
                                {

                                    log.LogDebug("检查scheduler是否开始");
                                    if (scheduler.IsStarted)
                                    {
                                        if (isClear)
                                        {
                                            scheduler.Clear().GetAwaiter().GetResult();
                                            isClear = false;
                                        }
                                        log.LogDebug("scheduler已经开始");

                                        foreach (var item in listQuartzTask)
                                        {

                                            db = Host.Services.GetService<QuartzDbContext>();
                                            listQuartzTask = db.QuartzTask.Where(w => w.IsDelete == 0
                                            && w.MachineName == machine && w.InstanceId == QuartzOpt.InsatanceId)
                                            .ToListAsync().GetAwaiter().GetResult();
                                            log.LogDebug("从数据库获取task记录,详细信息:{0}", Newtonsoft.Json.JsonConvert.SerializeObject(listQuartzTask));
                                            //加载程序集
                                            if (!string.IsNullOrEmpty(item.AssemblyName) && !collAssembly.ContainsKey(item.AssemblyName))
                                            {
                                                try
                                                {
                                                    collAssembly[item.AssemblyName] =
                                                    AssemblyHelp.GetAssemblyByteByAssemblyName(
                                                        Path.Combine(Directory.GetCurrentDirectory(), "AssemblyColl"), item.AssemblyName);
                                                }
                                                catch (Exception ep)
                                                {
                                                    log.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, ep, "没有找到程序集.");
                                                    Task.Delay(10000);
                                                    continue;
                                                }
                                            }
                                            log.LogDebug("开始检查task：{0}", Newtonsoft.Json.JsonConvert.SerializeObject(item));
                                            var jobKey = new JobKey(item.TaskName, item.GroupName);
                                            var triggerKey = new TriggerKey(item.TaskName, item.GroupName);
                                            if (scheduler.CheckExists(jobKey).Result)
                                            {
                                                var jobDetai = scheduler.GetJobDetail(jobKey);
                                                var trigger = scheduler.GetTrigger(triggerKey);
                                                log.LogDebug("此task已经存在scheduler中，数据库状态：{0}，scheduer中的状态：{1}.trigger状态：{2}"
                                                , ((OperateStatus)item.OperateStatus).ToString(), jobDetai.Status.ToString(), trigger.Status.ToString());

                                                if ((OperateStatus)item.OperateStatus == OperateStatus.Stop)
                                                {
                                                    log.LogInformation("删除schduler中的job：{0}", jobKey.ToString());
                                                    if (!scheduler.DeleteJob(jobKey).GetAwaiter().GetResult())
                                                    {
                                                        log.LogError("删除job失败。name:{0},group:{1}", jobKey.Name, jobKey.Group);
                                                    }
                                                }
                                                else
                                                {
                                                    if (jobDetai.IsFaulted)
                                                    {
                                                        if (jobDetai.Exception != null)
                                                        {
                                                            log.LogError(10005, jobDetai.Exception, "job faulted");
                                                        }
                                                        var jobItem = db.QuartzTask.FirstOrDefault(w => w.IsDelete == 0
                                                        && w.TaskName == jobKey.Name
                                                        && w.GroupName == jobKey.Group
                                                        && w.MachineName == machine
                                                        && w.InstanceId == scheduler.SchedulerInstanceId);
                                                        item.Status = (int)TaskStatus.Faulted;
                                                        item.OperateStatus = (int)OperateStatus.Stop;
                                                        db.Update<QuartzTask>(jobItem);
                                                        db.SaveChanges();
                                                    }
                                                    else
                                                    {
                                                        if (jobDetai.Status != TaskStatus.Running
                                                            && jobDetai.Status != TaskStatus.RanToCompletion
                                                            && jobDetai.Status != TaskStatus.WaitingForActivation
                                                            && jobDetai.Status != TaskStatus.WaitingForChildrenToComplete
                                                            && jobDetai.Status != TaskStatus.WaitingToRun)
                                                        {
                                                            var interTask = scheduler.Interrupt(jobKey, new CancellationToken(true))
                                                            .GetAwaiter().GetResult();
                                                            jobDetai.Start();
                                                        }
                                                    }
                                                }

                                                var triggerListener = scheduler.ListenerManager.GetTriggerListener("triggerUpdate");
                                                
                                                if (triggerListener == null)
                                                {
                                                    triggerListener = new TriggerUpdateListens();
                                                    IMatcher<TriggerKey> triggermatcher = KeyMatcher<TriggerKey>.KeyEquals(triggerKey);
                                                    scheduler.ListenerManager.AddTriggerListener(triggerListener, triggermatcher);
                                                }

                                                var jobListener = scheduler.ListenerManager.GetJobListener("jobupdateListens");
                                                if (jobListener == null)
                                                {
                                                    IJobListener jobUpdateListener = new JobUpdateListens();
                                                    IMatcher<JobKey> jobmatcher = KeyMatcher<JobKey>.KeyEquals(jobKey);
                                                    scheduler.ListenerManager.AddJobListener(jobUpdateListener, jobmatcher);
                                                }
                                            }
                                            else
                                            {
                                                log.LogInformation("添加新的job，判断是否状态为停止。");
                                                if ((OperateStatus)item.OperateStatus != OperateStatus.Stop)
                                                {
                                                    log.LogInformation("添加新的job");
                                                    var assemblyName = item.AssemblyName;
                                                    var className = item.ClassName;


                                                    Type jobTaskType = null;
                                                    try
                                                    {
                                                        jobTaskType = AssemblyHelp.GetTypeByAssemblyNameAndClassName(collAssembly[item.AssemblyName], className);
                                                        log.LogInformation("找到类型，type:{0}",className);
                                                    }
                                                    catch (Exception ep)
                                                    {
                                                        log.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, ep, "没有找到type.");
                                                    }
                                                    if (jobTaskType == null)
                                                    {
                                                        try
                                                        {
                                                            jobTaskType = AssemblyHelp
                                                            .GetTypeByCurrentAssemblyNameAndClassName(className, Assembly.GetExecutingAssembly());
                                                            if (jobTaskType == null)
                                                            {
                                                                log.LogInformation("没有找到类型");
                                                                continue;
                                                            }
                                                            log.LogInformation("找到类型，type:{0}",className);
                                                        }
                                                        catch (Exception ep)
                                                        {
                                                            log.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, ep, "没有找到类型.");
                                                            continue;
                                                        }
                                                    }
                                                    IJobDetail job = JobBuilder.Create(jobTaskType)
                                                        .WithIdentity(item.TaskName, item.GroupName)
                                                        .Build();

                                                    ITrigger trigger = TriggerBuilder.Create()
                                                        .WithIdentity(item.TaskName, item.GroupName)
                                                        .StartNow()
                                                        .WithCronSchedule(item.CronExpressionString)
                                                        .Build();
                                                    scheduler.ScheduleJob(job, trigger).GetAwaiter().GetResult();
                                                    log.LogInformation("添加成功，type:{0}",className);
                                                    ITriggerListener triggerListener = new TriggerUpdateListens();
                                                    IMatcher<TriggerKey> triggermatcher = KeyMatcher<TriggerKey>.KeyEquals(trigger.Key);
                                                    scheduler.ListenerManager.AddTriggerListener(triggerListener, triggermatcher);


                                                    IJobListener jobUpdateListener = new JobUpdateListens();
                                                    IMatcher<JobKey> jobmatcher = KeyMatcher<JobKey>.KeyEquals(job.Key);
                                                    scheduler.ListenerManager.AddJobListener(jobUpdateListener, jobmatcher);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        log.LogInformation("scheduler is not IsStarted");
                                    }
                                }
                                else
                                {
                                    log.LogInformation("scheduler is null");
                                }
                            }
                            catch (Exception ep)
                            {
                                log.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, ep, "task监控程序执行错误.");
                            }
                            Thread.Sleep(15000);
                        }
                    });
                // }
                // else
                // {
                //     db = Host.Services.GetService<QuartzDbContext>();
                //     listQuartzTask = db.QuartzTask.Where(w => w.IsDelete == 0
                //                                             && w.MachineName == machine
                //                                             && w.InstanceId == QuartzOpt.InsatanceId)
                //                                       .ToListAsync().GetAwaiter().GetResult();
                //     foreach (var item in listQuartzTask)
                //     {
                //          var jobKey = new JobKey(item.TaskName, item.GroupName);
                //          var triggerKey = new TriggerKey(item.TaskName, item.GroupName);


                //         // var jobItem = db.QuartzTask.FirstOrDefault(w => w.IsDelete == 0
                //         //                          && w.TaskName == jobKey.Name
                //         //                          && w.GroupName == jobKey.Group
                //         //                          && w.MachineName == machine
                //         //                          && w.InstanceId == scheduler.SchedulerInstanceId);
                //         // item.Status = (int)TaskStatus.Faulted;
                //         // item.OperateStatus = (int)OperateStatus.Stop;
                //         // db.Update<QuartzTask>(jobItem);
                //         // db.SaveChanges();



                //         if (scheduler.CheckExists(jobKey).Result)
                //         {
                //             var triggerListener = scheduler.ListenerManager.GetTriggerListener("triggerUpdate");
                //             if (triggerListener == null)
                //             {
                //                 triggerListener = new TriggerUpdateListens();
                //                 IMatcher<TriggerKey> triggermatcher = KeyMatcher<TriggerKey>.KeyEquals(triggerKey);
                //                 scheduler.ListenerManager.AddTriggerListener(triggerListener, triggermatcher);
                //             }

                //             var jobListener = scheduler.ListenerManager.GetJobListener("jobupdateListens");
                //             if (jobListener == null)
                //             {
                //                 IJobListener jobUpdateListener = new JobUpdateListens();
                //                 IMatcher<JobKey> jobmatcher = KeyMatcher<JobKey>.KeyEquals(jobKey);
                //                 scheduler.ListenerManager.AddJobListener(jobUpdateListener, jobmatcher);
                //             }
                //         }
                //     }
                //}
                return scheduler;
                // Tell quartz to schedule the job using our trigger
                //await scheduler.ScheduleJob(job, trigger);
            }
            catch (SchedulerException sep)
            {
                log.Log(Microsoft.Extensions.Logging.LogLevel.Error, 0, sep, "job执行错误。");
            }
            return null;
        }

        // simple log provider to get something to the console
    }

    [PersistJobDataAfterExecution]
    [DisallowConcurrentExecution]
    public class BaseJob1 : IJob
    {
        public BaseJob1()
        {
        }
        public virtual async Task Execute(IJobExecutionContext context)
        {
            var logFacty = Program.Host.Services.GetService<ILoggerFactory>();
            var log = logFacty.CreateLogger<BaseJob1>();
            if (context.CancellationToken == new CancellationToken(true))
            {
                log.LogInformation("正常取消job执行。");
                return;
            }
            log.LogInformation(@"---------------！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！！
            @@@@@@@@@@@@@@@@!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
            + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));
        }
    }
}