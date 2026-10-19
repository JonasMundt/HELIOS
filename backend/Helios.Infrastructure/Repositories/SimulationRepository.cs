
using Helios.Application.Interfaces;
using Helios.Domain.Entities;
using Helios.Domain.Enums;
using Helios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Helios.Infrastructure.Repositories;

public class SimulationRepository : ISimulationRepository
{
    private readonly HeliosDbContext _context;

    public SimulationRepository(HeliosDbContext context)
    {
        _context = context;
    }

    public async Task<SimulationRun?> GetByIdAsync(Guid id)
    {
        return await _context.SimulationRuns
            .FirstOrDefaultAsync(simulation => simulation.Id == id);
    }

    public async Task<IReadOnlyList<SimulationRun>> GetAllAsync()
    {
        return await _context.SimulationRuns
            .AsNoTracking()
            .OrderByDescending(simulation => simulation.StartTime)
            .ToListAsync();
    }

    public async Task<SimulationRun?> GetActiveAsync()
    {
        return await _context.SimulationRuns
            .FirstOrDefaultAsync(
                simulation => simulation.Status == SimulationStatus.Running
            );
    }

    public async Task AddAsync(SimulationRun simulation)
    {
        await _context.SimulationRuns.AddAsync(simulation);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
