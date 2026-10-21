using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class RoutineDayConfiguration : IEntityTypeConfiguration<RoutineDay>
{
    public void Configure(EntityTypeBuilder<RoutineDay> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(60).IsRequired();
        builder.HasIndex(d => new { d.RoutineId, d.DayNumber }).IsUnique();
        builder.HasMany(d => d.Exercises).WithOne().HasForeignKey(e => e.RoutineDayId).OnDelete(DeleteBehavior.Cascade);
    }
}
