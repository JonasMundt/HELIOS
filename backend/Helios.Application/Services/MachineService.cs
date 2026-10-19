
using Helios.Domain.Entities;
using Helios.Domain.Enums;

namespace Helios.Application.Services;

public class MachineService
{
    private readonly List<Machine> _machines = new();

    public MachineService()
    {
        _machines.Add(new Machine(
            "Conveyor Belt 01",
            MachineType.ConveyorBelt
        ));

        _machines.Add(new Machine(
            "Scanner Station 01",
            MachineType.Scanner
        ));

        _machines.Add(new Machine(
            "Hydraulic Press 01",
            MachineType.HydraulicPress
        ));

        _machines.Add(new Machine(
            "Robot Arm 01",
            MachineType.RobotArm
        ));
    }

    public IReadOnlyList<Machine> GetAllMachines()
    {
        return _machines.AsReadOnly();
    }

    public Machine? GetMachineById(Guid id)
    {
        return _machines.FirstOrDefault(
            machine => machine.Id == id
        );
    }
}
