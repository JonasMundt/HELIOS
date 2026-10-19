
using Helios.Application.Interfaces;
using Helios.Domain.Entities;

namespace Helios.Application.Services;

public class MachineService
{
    private readonly IMachineRepository _repository;

    public MachineService(IMachineRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<Machine>> GetAllMachinesAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<Machine?> GetMachineByIdAsync(Guid id)
    {
        return await _repository.GetByIdAsync(id);
    }
}
