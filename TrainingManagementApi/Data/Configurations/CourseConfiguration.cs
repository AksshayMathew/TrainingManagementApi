using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrainingManagementApi.Models;

namespace TrainingManagementApi.Data.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Course", table =>
            table.HasCheckConstraint("CK_Course_Duration_Positive", "[Duration] > 0"));

        builder.HasKey(c => c.CourseId);

        builder.Property(c => c.CourseName).IsRequired().HasMaxLength(FieldLimits.CourseNameMaxLength);
        builder.Property(c => c.TrainerName).IsRequired().HasMaxLength(FieldLimits.TrainerNameMaxLength);
    }
}
