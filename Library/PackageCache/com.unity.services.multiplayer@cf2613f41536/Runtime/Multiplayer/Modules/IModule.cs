using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// Component providing the multiplayer session module
    /// </summary>
    interface IModule
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default);
        public Task LeaveAsync();
    }
}
