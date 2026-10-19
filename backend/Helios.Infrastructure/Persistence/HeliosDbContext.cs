
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
    }
}
