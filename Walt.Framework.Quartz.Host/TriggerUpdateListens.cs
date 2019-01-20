
using System; 
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Logging; 
using Quartz.Impl;

namespace Walt.Framework.Quartz.Host
{

    public class TriggerUpdateListens : ITriggerListener
    {
        public string Name => "triggerUpdate";

        public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(true);
        }

        public Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(false);
        }
    }

}