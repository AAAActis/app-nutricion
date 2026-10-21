using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class FoodConfiguration : IEntityTypeConfiguration<Food>
{
    public void Configure(EntityTypeBuilder<Food> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Brand).HasMaxLength(120);
        builder.Property(f => f.Barcode).HasMaxLength(32);
        builder.Property(f => f.ExternalId).HasMaxLength(64);
        builder.Property(f => f.PortionName).HasMaxLength(60);

        // Búsqueda por similitud sin tildes (pg_trgm): sirve para Contains y para ranking.
        builder.HasIndex(f => f.NormalizedName).HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(f => f.Barcode);
        // Idempotencia del seed: NULL no choca con NULL en Postgres, así que los Custom no molestan.
        builder.HasIndex(f => new { f.Source, f.ExternalId }).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(f => f.CreatedByUserId).OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Food_Macros",
                "\"CaloriesPer100g\" >= 0 AND \"ProteinPer100g\" >= 0 AND \"CarbsPer100g\" >= 0 AND \"FatPer100g\" >= 0");
            t.HasCheckConstraint("CK_Food_CustomOwner",
                "\"Source\" <> 'Custom' OR \"CreatedByUserId\" IS NOT NULL");
        });
    }
}
