using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();
    }
}
