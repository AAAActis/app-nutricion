using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class RoutineExerciseConfiguration : IEntityTypeConfiguration<RoutineExercise>
{
    public void Configure(EntityTypeBuilder<RoutineExercise> builder)
    {
        builder.HasOne(e => e.Exercise).WithMany().HasForeignKey(e => e.ExerciseId).OnDelete(DeleteBehavior.NoAction);
        builder.ToTable(t => t.HasCheckConstraint("CK_RoutineExercise_Targets", "\"TargetSets\" > 0 AND \"TargetReps\" > 0"));
    }
}
