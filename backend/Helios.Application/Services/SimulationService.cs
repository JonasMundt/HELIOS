
using Helios.Application.Interfaces;
using Helios.Domain.Entities;
using Helios.Domain.Enums;

namespace Helios.Application.Services;

public class SimulationService
{
    private readonly ISimulationRepository _repository;

    public SimulationService(ISimulationRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SimulationRun>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<SimulationRun?> GetByIdAsync(Guid id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<SimulationRun> StartAsync()
    {
        var activeSimulation = await _repository.GetActiveAsync();

        if (activeSimulation is not null)
        {
            throw new InvalidOperationException(
                "A simulation is already running.");
        }

        var simulation = new SimulationRun();
        simulation.Start();

        await _repository.AddAsync(simulation);
        await _repository.SaveChangesAsync();

        return simulation;
    }

    public async Task<bool> CancelAsync(Guid id)
    {
        var simulation = await _repository.GetByIdAsync(id);

        if (simulation is null)
        {
            return false;
        }

        simulation.Cancel();

        await _repository.SaveChangesAsync();

        return true;
    }
    public async Task<SimulationRun?> AdvanceAsync(Guid id)
    {
        var simulation = await _repository.GetByIdAsync(id);

        if (simulation is null)
        {
            return null;
        }

        if (simulation.Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException(
                "Only running simulations can advance.");
        }

        if (simulation.CurrentStage == ProductionStage.Finished)
        {
            throw new InvalidOperationException(
                "The production cycle is already finished.");
        }

        var nextStage = (ProductionStage)((int)simulation.CurrentStage + 1);

        simulation.AdvanceStage(nextStage);

        if (nextStage == ProductionStage.Finished)
        {
            simulation.AddProducedItem();
            simulation.Complete();
        }

        await _repository.SaveChangesAsync();

        return simulation;
    }

}
