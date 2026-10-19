
using Helios.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Helios.Infrastructure.Persistence;

public class HeliosDbContext : DbContext
{
    public HeliosDbContext(
        DbContextOptions<HeliosDbContext> options
    ) : base(options)
    {
    }

    public DbSet<Machine> Machines => Set<Machine>();

    public DbSet<SimulationRun> SimulationRuns => Set<SimulationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Machine configuration
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(machine => machine.Id);

            entity.Property(machine => machine.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(machine => machine.Type)
                .IsRequired();

            entity.Property(machine => machine.Status)
                .IsRequired();

            entity.Property(machine => machine.CreatedAt)
                .IsRequired();
        });

        // SimulationRun configuration
        modelBuilder.Entity<SimulationRun>(entity =>
        {
            entity.HasKey(simulation => simulation.Id);

            entity.Property(simulation => simulation.StartTime);

            entity.Property(simulation => simulation.EndTime);

            entity.Property(simulation => simulation.Status)
                .IsRequired();

            entity.Property(simulation => simulation.ProducedItems)
                .IsRequired();

            entity.Property(simulation => simulation.CurrentStage)
                .IsRequired();
        });
    }
}
