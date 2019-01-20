
using System; 
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Logging; 
using Quartz.Impl;

namespace Walt.Framework.Quartz.Host
{

    public class SchedulerListenes : ISchedulerListener
    {
        public Task JobAdded(IJobDetail jobDetail, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobDeleted(JobKey jobKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobInterrupted(JobKey jobKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobPaused(JobKey jobKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobResumed(JobKey jobKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobScheduled(ITrigger trigger, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobsPaused(string jobGroup, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobsResumed(string jobGroup, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task JobUnscheduled(TriggerKey triggerKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task SchedulerError(string msg, SchedulerException cause, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task SchedulerInStandbyMode(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task SchedulerShutdown(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task SchedulerShuttingdown(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task SchedulerStarted(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task SchedulerStarting(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task SchedulingDataCleared(CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task TriggerFinalized(ITrigger trigger, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task TriggerPaused(TriggerKey triggerKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task TriggerResumed(TriggerKey triggerKey, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task TriggersPaused(string triggerGroup, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task TriggersResumed(string triggerGroup, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }
    }

}