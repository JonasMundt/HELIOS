
using Helios.Domain.Entities;

namespace Helios.Application.Interfaces;

public interface IMachineRepository
{
    Task<IReadOnlyList<Machine>> GetAllAsync();

    Task<Machine?> GetByIdAsync(Guid id);

    Task AddAsync(Machine machine);

    Task SaveChangesAsync();
}
