using System.Threading;
using System.Threading.Tasks;

namespace Walt.Framework.Console
{
    public interface IConsole
    {
        Task AsyncExcute(CancellationToken cancel=default(CancellationToken));
    }
}