using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class WorkoutSetConfiguration : IEntityTypeConfiguration<WorkoutSet>
{
    public void Configure(EntityTypeBuilder<WorkoutSet> builder)
    {
        builder.HasIndex(s => s.ExerciseId);   // progreso por ejercicio
        builder.HasOne(s => s.Exercise).WithMany().HasForeignKey(s => s.ExerciseId).OnDelete(DeleteBehavior.NoAction);
        builder.ToTable(t => t.HasCheckConstraint("CK_WorkoutSet_Values", "\"Reps\" > 0 AND \"WeightKg\" >= 0"));
    }
}
