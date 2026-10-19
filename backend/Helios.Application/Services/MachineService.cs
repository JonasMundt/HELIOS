
using Helios.Application.Interfaces;
using Helios.Domain.Entities;
using Helios.Domain.Enums;

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

    public async Task<bool> StartMachineAsync(Guid id)
    {
        var machine = await _repository.GetByIdAsync(id);

        if (machine is null)
        {
            return false;
        }

        machine.SetStatus(MachineStatus.Running);
        await _repository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> StopMachineAsync(Guid id)
    {
        var machine = await _repository.GetByIdAsync(id);

        if (machine is null)
        {
            return false;
        }

        machine.SetStatus(MachineStatus.Offline);
        await _repository.SaveChangesAsync();

        return true;
    }

    public async Task UpdateMachinesForStageAsync(
        ProductionStage stage)
    {
        var machines = await _repository.GetAllTrackedAsync();

        MachineType? activeMachine = stage switch
        {
            ProductionStage.ConveyorBelt => MachineType.ConveyorBelt,
            ProductionStage.Scanning => MachineType.Scanner,
            ProductionStage.Pressing => MachineType.HydraulicPress,
            ProductionStage.RobotArm => MachineType.RobotArm,
            _ => null
        };

        foreach (var machine in machines)
        {
            var newStatus =
                activeMachine.HasValue &&
                machine.Type == activeMachine.Value
                    ? MachineStatus.Running
                    : MachineStatus.Idle;

            machine.SetStatus(newStatus);
        }

        await _repository.SaveChangesAsync();
    }
}
