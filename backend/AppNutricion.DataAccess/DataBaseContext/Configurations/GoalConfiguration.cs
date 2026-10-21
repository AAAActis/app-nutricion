using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        // Un solo objetivo activo por usuario.
        builder.HasIndex(g => g.UserId).IsUnique().HasFilter("\"IsActive\" = true");
        builder.HasIndex(g => new { g.UserId, g.CreatedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
