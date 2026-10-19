
using Microsoft.AspNetCore.SignalR;

namespace Helios.Api.Hubs;

public class HeliosHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(
            "SystemMessage",
            "Connected to HELIOS Real-Time Hub");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
