using EmployeeSync.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EmployeeSync.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(builder =>
        {
            builder.ToTable("Employees", table =>
            {
                table.HasCheckConstraint(
                    "CK_Employees_SyncStatus",
                    "[SyncStatus] IN ('pending', 'synced', 'failed')");
                table.HasCheckConstraint(
                    "CK_Employees_Status",
                    "[Status] IN ('active', 'inactive', 'terminated')");
            });

            builder.HasKey(employee => employee.Id);
            builder.Property(employee => employee.Id).ValueGeneratedNever();

            builder.Property(employee => employee.Provider)
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(employee => employee.ProviderEmployeeId)
                .HasMaxLength(128)
                .IsRequired();

            builder.Property(employee => employee.TrackTikEmployeeId)
                .HasMaxLength(160)
                .IsRequired();

            builder.Property(employee => employee.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(employee => employee.LastName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(employee => employee.Email)
                .HasMaxLength(320)
                .IsRequired();

            builder.Property(employee => employee.PhoneNumber).HasMaxLength(40);
            builder.Property(employee => employee.Position).HasMaxLength(120);
            builder.Property(employee => employee.Department).HasMaxLength(120);
            builder.Property(employee => employee.StartDate).HasColumnType("date");

            builder.Property(employee => employee.Status)
                .HasConversion(new ValueConverter<EmployeeStatus, string>(
                    status => status == EmployeeStatus.Active
                        ? "active"
                        : status == EmployeeStatus.Inactive
                            ? "inactive"
                            : "terminated",
                    value => value == "active"
                        ? EmployeeStatus.Active
                        : value == "inactive"
                            ? EmployeeStatus.Inactive
                            : EmployeeStatus.Terminated))
                .HasMaxLength(16)
                .IsRequired();

            builder.Property(employee => employee.SyncStatus)
                .HasConversion(new ValueConverter<SyncStatus, string>(
                    status => status == SyncStatus.Pending
                        ? "pending"
                        : status == SyncStatus.Synced
                            ? "synced"
                            : "failed",
                    value => value == "pending"
                        ? SyncStatus.Pending
                        : value == "synced"
                            ? SyncStatus.Synced
                            : SyncStatus.Failed))
                .HasMaxLength(16)
                .IsRequired();

            builder.Property(employee => employee.Attempts).HasDefaultValue(0);
            builder.Property(employee => employee.LastError).HasMaxLength(2000);
            builder.Property(employee => employee.LastAttemptAt).HasColumnType("datetimeoffset(7)");
            builder.Property(employee => employee.ExistsInTrackTik).HasDefaultValue(false);
            builder.Property(employee => employee.CreatedAt).HasColumnType("datetimeoffset(7)");
            builder.Property(employee => employee.UpdatedAt).HasColumnType("datetimeoffset(7)");
            builder.Property(employee => employee.SyncedAt).HasColumnType("datetimeoffset(7)");

            builder.HasIndex(employee => new { employee.Provider, employee.ProviderEmployeeId })
                .IsUnique()
                .HasDatabaseName("UX_Employees_Provider_ProviderEmployeeId");

            builder.HasIndex(employee => employee.SyncStatus)
                .HasDatabaseName("IX_Employees_SyncStatus");
        });
    }
}
