using System; 
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Logging; 
using Quartz.Impl;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Walt.Framework.Quartz.Host
{

    public class JobUpdateListens : IJobListener
    { 
        public string Name => "jobupdateListens";

        public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                string machine = Environment.MachineName;
                QuartzDbContext db = Program.Host.Services.GetService<QuartzDbContext>();
                var item = db.QuartzTask.FirstOrDefault(w => w.IsDelete == 0
                && w.TaskName == context.JobDetail.Key.Name
                && w.GroupName == context.JobDetail.Key.Group
                && w.MachineName == machine
                && w.InstanceId == context.Scheduler.SchedulerInstanceId);
                item.Status = (int)TaskStatus.WaitingToRun;
                db.Update<QuartzTask>(item);
                db.SaveChanges();
            }
            catch (Exception ep)
            {
                //context.Scheduler.Interrupt(context.JobDetail.Key);
                var logFaoctory = Program.Host.Services.GetService<ILoggerFactory>();
                var log = logFaoctory.CreateLogger<JobUpdateListens>();
                log.LogError(0, ep, "JobToBeExecuted:Job执行错误,name：{0},Group:{1}", context.JobDetail.Key.Name, context.JobDetail.Key.Group);
            }
            return Task.FromResult(true);
        }

        public Task JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                QuartzDbContext db = Program.Host.Services.GetService<QuartzDbContext>();
                var logFaoctory = Program.Host.Services.GetService<ILoggerFactory>();
                var log = logFaoctory.CreateLogger<JobUpdateListens>();
                string machine = Environment.MachineName;
                var item = db.QuartzTask.FirstOrDefault(w => w.IsDelete == 0
                                                        && w.TaskName == context.JobDetail.Key.Name
                                                        && w.GroupName == context.JobDetail.Key.Group
                                                        && w.MachineName == machine
                                                        && w.InstanceId == context.Scheduler.SchedulerInstanceId);
                if (jobException != null)
                {
                    item.Status = (int)TaskStatus.Faulted;
                    item.Remark = Newtonsoft.Json.JsonConvert.SerializeObject(jobException);
                    log.LogError("Job执行错误,name：{0},Group:{1}", context.JobDetail.Key.Name, context.JobDetail.Key.Group);
                }
                else
                {
                    item.Status = (int)TaskStatus.RanToCompletion;
                    item.RecentRunTime = context.FireTimeUtc.DateTime;
                    if (context.NextFireTimeUtc.HasValue)
                    {
                        item.NextFireTime = context.NextFireTimeUtc.Value.DateTime;
                    }
                }
                db.Update<QuartzTask>(item);
                db.SaveChanges();
            }
            catch (Exception ep)
            {
                //context.Scheduler.Interrupt(context.JobDetail.Key);
                var logFaoctory = Program.Host.Services.GetService<ILoggerFactory>();
                var log = logFaoctory.CreateLogger<JobUpdateListens>();
                log.LogError(0, ep, "JobWasExecuted:Job执行错误,name：{0},Group:{1}", context.JobDetail.Key.Name, context.JobDetail.Key.Group);
            }
            return Task.FromResult(true);
        }
    }

}