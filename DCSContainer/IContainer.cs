using System.Threading.Tasks;
using Megumin.Remote;
using Net.Remote;

namespace Megumin.DCS
{
    public interface IContainer
    {
        IRemote Remote { get; }

        Task<rpcResult> Send<rpcResult>(object testMessage);
    }
}