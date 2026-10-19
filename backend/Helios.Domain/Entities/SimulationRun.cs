
using Helios.Domain.Enums;

namespace Helios.Domain.Entities;

public class SimulationRun
{
    public Guid Id { get; private set; }

    public DateTime? StartTime { get; private set; }

    public DateTime? EndTime { get; private set; }

    public SimulationStatus Status { get; private set; }

    public int ProducedItems { get; private set; }
    
    public ProductionStage CurrentStage { get; private set; }


    public SimulationRun()
    {
        Id = Guid.NewGuid();
        Status = SimulationStatus.Pending;
        CurrentStage = ProductionStage.Waiting;
        ProducedItems = 0;
    }

    public void Start()
    {
        if (Status != SimulationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending simulations can be started.");
        }

        StartTime = DateTime.UtcNow;
        Status = SimulationStatus.Running;
    }

    public void Complete()
    {
        if (Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException(
                "Only running simulations can be completed.");
        }

        EndTime = DateTime.UtcNow;
        Status = SimulationStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException(
                "Only running simulations can be cancelled.");
        }

        EndTime = DateTime.UtcNow;
        Status = SimulationStatus.Cancelled;
    }

    public void AddProducedItem()
    {
        if (Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException(
                "Cannot produce items outside a running simulation.");
        }

        ProducedItems++;
    }

    
    public void AdvanceStage(ProductionStage nextStage)
    {
        if (Status != SimulationStatus.Running)
        {
            throw new InvalidOperationException(
                "Production stages can only change during a running simulation.");
        }

        if (nextStage != (ProductionStage)((int)CurrentStage + 1))
        {
            throw new InvalidOperationException(
                "Invalid production stage transition.");
        }

        CurrentStage = nextStage;
    }

}
