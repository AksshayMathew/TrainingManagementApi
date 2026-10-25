using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        // Table name matches the assignment (singular), not EF's default plural.
        builder.ToTable("Employee", table =>
            table.HasCheckConstraint("CK_Employee_Experience_NonNegative", "[Experience] >= 0"));

        builder.HasKey(e => e.EmployeeId);

        builder.Property(e => e.EmployeeName).IsRequired().HasMaxLength(FieldLimits.EmployeeNameMaxLength);
        builder.Property(e => e.Email).IsRequired().HasMaxLength(FieldLimits.EmailMaxLength);
        builder.Property(e => e.Department).IsRequired().HasMaxLength(FieldLimits.DepartmentMaxLength);
        builder.Property(e => e.JoiningDate).IsRequired();

        // The database itself guarantees "Email must be unique" - the service check only gives a nicer message.
        builder.HasIndex(e => e.Email).IsUnique().HasDatabaseName("UX_Employee_Email");
    }
}
