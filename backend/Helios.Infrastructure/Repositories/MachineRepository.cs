
using Helios.Application.Interfaces;
using Helios.Domain.Entities;
using Helios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Helios.Infrastructure.Repositories;

public class MachineRepository : IMachineRepository
{
    private readonly HeliosDbContext _context;

    public MachineRepository(HeliosDbContext context)
    {
        _context = context;
    }

    // Read machines without tracking (dashboard)
    public async Task<IReadOnlyList<Machine>> GetAllAsync()
    {
        return await _context.Machines
            .AsNoTracking()
            .OrderBy(machine => machine.Type)
            .ToListAsync();
    }

    // Read machines with tracking (automatic control)
    public async Task<IReadOnlyList<Machine>> GetAllTrackedAsync()
    {
        return await _context.Machines
            .OrderBy(machine => machine.Type)
            .ToListAsync();
    }

    public async Task<Machine?> GetByIdAsync(Guid id)
    {
        return await _context.Machines
            .FirstOrDefaultAsync(machine => machine.Id == id);
    }

    public async Task AddAsync(Machine machine)
    {
        await _context.Machines.AddAsync(machine);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
