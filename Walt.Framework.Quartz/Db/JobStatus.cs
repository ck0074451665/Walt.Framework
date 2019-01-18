


using System.Threading.Tasks;

namespace Walt.Framework.Quartz
{
    public enum JobStatus
    {
            //
        // 摘要:
        //     The task has been initialized but has not yet been scheduled.
        Created = 0,
        //
        // 摘要:
        //     The task is waiting to be activated and scheduled internally by the .NET Framework
        //     infrastructure.
        WaitingForActivation = 1,
        //
        // 摘要:
        //     The task has been scheduled for execution but has not yet begun executing.
        WaitingToRun = 2,
        //
        // 摘要:
        //     The task is running but has not yet completed.
        Running = 3,
        //
        // 摘要:
        //     The task has finished executing and is implicitly waiting for attached child
        //     tasks to complete.
        WaitingForChildrenToComplete = 4,
        //
        // 摘要:
        //     The task completed execution successfully.
        RanToCompletion = 5,
        //
        // 摘要:
        //     The task acknowledged cancellation by throwing an OperationCanceledException
        //     with its own CancellationToken while the token was in signaled state, or the
        //     task&#39;s CancellationToken was already signaled before the task started executing.
        //     For more information, see Task Cancellation.
        Canceled = 6,
        //
        // 摘要:
        //     The task completed due to an unhandled exception.
        Faulted = 7,
        
        Stop=8
    }
}