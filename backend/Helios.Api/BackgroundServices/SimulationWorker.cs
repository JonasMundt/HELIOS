
using Helios.Api.Services;
using Helios.Application.Interfaces;
using Helios.Application.Services;

namespace Helios.Api.BackgroundServices;

public class SimulationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SimulationWorker> _logger;
    private readonly HeliosRealtimeNotifier _notifier;

    public SimulationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SimulationWorker> logger,
        HeliosRealtimeNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _notifier = notifier;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "HELIOS Simulation Worker started.");

        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(3));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var repository = scope.ServiceProvider
                        .GetRequiredService<ISimulationRepository>();

                    var simulationService = scope.ServiceProvider
                        .GetRequiredService<SimulationService>();

                    var machineService = scope.ServiceProvider
                        .GetRequiredService<MachineService>();

                    // Find the currently active simulation
                    var activeSimulation =
                        await repository.GetActiveAsync();

                    if (activeSimulation is null)
                    {
                        continue;
                    }

                    // Advance production to the next stage
                    var updatedSimulation =
                        await simulationService.AdvanceAsync(
                            activeSimulation.Id);

                    if (updatedSimulation is null)
                    {
                        continue;
                    }

                    // Update machine states for the new stage
                    await machineService.UpdateMachinesForStageAsync(
                        updatedSimulation.CurrentStage);

                    // Notify connected dashboards
                    await _notifier.NotifySimulationsChangedAsync();
                    await _notifier.NotifyMachinesChangedAsync();

                    _logger.LogInformation(
                        "Simulation {SimulationId} advanced to stage {Stage}.",
                        updatedSimulation.Id,
                        updatedSimulation.CurrentStage);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Simulation could not advance.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error in HELIOS Simulation Worker.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "HELIOS Simulation Worker stopped.");
        }
    }
}
