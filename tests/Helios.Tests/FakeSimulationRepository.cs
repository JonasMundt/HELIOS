
using Helios.Application.Interfaces;
using Helios.Domain.Entities;
using Helios.Domain.Enums;

namespace Helios.Tests;

public class FakeSimulationRepository : ISimulationRepository
{
    private readonly List<SimulationRun> _simulations = new();

    public Task<SimulationRun?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(
            _simulations.FirstOrDefault(s => s.Id == id));
    }

    public Task<IReadOnlyList<SimulationRun>> GetAllAsync()
    {
        IReadOnlyList<SimulationRun> result = _simulations
            .OrderByDescending(s => s.StartTime)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<SimulationRun?> GetActiveAsync()
    {
        return Task.FromResult(
            _simulations.FirstOrDefault(
                s => s.Status == SimulationStatus.Running));
    }

    public Task AddAsync(SimulationRun simulation)
    {
        _simulations.Add(simulation);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return Task.CompletedTask;
    }
}
