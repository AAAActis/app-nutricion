using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class MealEntryConfiguration : IEntityTypeConfiguration<MealEntry>
{
    public void Configure(EntityTypeBuilder<MealEntry> builder)
    {
        builder.Property(m => m.ItemName).HasMaxLength(200).IsRequired();
        builder.HasIndex(m => new { m.UserId, m.Date });

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        // Macros congelados: si se borra el alimento o la receta, la línea del diario queda.
        builder.HasOne(m => m.Food).WithMany().HasForeignKey(m => m.FoodId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(m => m.Recipe).WithMany().HasForeignKey(m => m.RecipeId).OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t => t.HasCheckConstraint("CK_MealEntry_Quantity", "\"QuantityGrams\" > 0"));
    }
}
