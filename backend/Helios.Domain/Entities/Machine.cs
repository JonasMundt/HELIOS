
using Helios.Domain.Enums;

namespace Helios.Domain.Entities;

public class Machine
{
    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public MachineType Type { get; private set; }

    public MachineStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Machine(string name, MachineType type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Machine name cannot be empty.",
                nameof(name)
            );
        }

        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        Status = MachineStatus.Offline;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetStatus(MachineStatus newStatus)
    {
        Status = newStatus;
    }
}
