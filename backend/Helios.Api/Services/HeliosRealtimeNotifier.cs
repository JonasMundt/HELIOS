
using Helios.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Helios.Api.Services;

public class HeliosRealtimeNotifier
{
    private readonly IHubContext<HeliosHub> _hubContext;

    public HeliosRealtimeNotifier(
        IHubContext<HeliosHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyMachinesChangedAsync()
    {
        await _hubContext.Clients.All.SendAsync(
            "MachinesChanged");
    }

    public async Task NotifySimulationsChangedAsync()
    {
        await _hubContext.Clients.All.SendAsync(
            "SimulationsChanged");
    }
}
