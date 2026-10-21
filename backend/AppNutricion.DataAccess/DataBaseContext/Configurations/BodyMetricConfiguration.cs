using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class BodyMetricConfiguration : IEntityTypeConfiguration<BodyMetric>
{
    public void Configure(EntityTypeBuilder<BodyMetric> builder)
    {
        builder.HasIndex(b => new { b.UserId, b.Date }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t => t.HasCheckConstraint("CK_BodyMetric_Weight", "\"WeightKg\" > 0"));
    }
}
