using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class WorkoutLogConfiguration : IEntityTypeConfiguration<WorkoutLog>
{
    public void Configure(EntityTypeBuilder<WorkoutLog> builder)
    {
        builder.Property(w => w.Notes).HasMaxLength(500);
        builder.HasIndex(w => new { w.UserId, w.Date });
        builder.HasOne<User>().WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        // Si se borra la rutina, el historial de entrenamientos queda.
        builder.HasOne<RoutineDay>().WithMany().HasForeignKey(w => w.RoutineDayId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(w => w.Sets).WithOne().HasForeignKey(s => s.WorkoutLogId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t => t.HasCheckConstraint("CK_WorkoutLog_Duration", "\"DurationMinutes\" > 0"));
    }
}
