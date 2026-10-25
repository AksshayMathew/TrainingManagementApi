using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.Data.Configurations;

public class TrainingAssignmentConfiguration : IEntityTypeConfiguration<TrainingAssignment>
{
    public void Configure(EntityTypeBuilder<TrainingAssignment> builder)
    {
        // Built from TrainingStatuses so the database rule can never drift from the C# list.
        var allowedStatuses = string.Join(", ", TrainingStatuses.All.Select(status => $"'{status}'"));

        builder.ToTable("TrainingAssignment", table =>
        {
            table.HasCheckConstraint("CK_TrainingAssignment_EndDate_OnOrAfterStartDate", "[EndDate] >= [StartDate]");
            table.HasCheckConstraint("CK_TrainingAssignment_Status_Valid", $"[Status] IN ({allowedStatuses})");
        });

        builder.HasKey(a => a.AssignmentId);

        builder.Property(a => a.StartDate).IsRequired();
        builder.Property(a => a.EndDate).IsRequired();
        builder.Property(a => a.Status).IsRequired().HasMaxLength(FieldLimits.StatusMaxLength);

        // Employee 1 -> many TrainingAssignment. Restrict = the database refuses to delete an employee who still
        // has assignments, so training records are never deleted by accident (the API reports it as a clear 400).
        builder.HasOne(a => a.Employee)
            .WithMany(e => e.TrainingAssignments)
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Course 1 -> many TrainingAssignment, same protection.
        builder.HasOne(a => a.Course)
            .WithMany(c => c.TrainingAssignments)
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // "An employee should not be assigned the same course more than once" - enforced by the database.
        // The index starts with EmployeeId, so it also serves "all trainings of an employee" lookups.
        builder.HasIndex(a => new { a.EmployeeId, a.CourseId })
            .IsUnique()
            .HasDatabaseName("UX_TrainingAssignment_Employee_Course");
    }
}
