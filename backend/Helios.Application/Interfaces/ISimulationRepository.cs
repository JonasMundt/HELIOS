
using Helios.Domain.Entities;

namespace Helios.Application.Interfaces;

public interface ISimulationRepository
{
    Task<SimulationRun?> GetByIdAsync(Guid id);

    Task<IReadOnlyList<SimulationRun>> GetAllAsync();

    Task<SimulationRun?> GetActiveAsync();

    Task AddAsync(SimulationRun simulation);

    Task SaveChangesAsync();
}
