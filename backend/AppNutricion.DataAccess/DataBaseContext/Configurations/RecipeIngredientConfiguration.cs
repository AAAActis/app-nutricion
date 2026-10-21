using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        // No se puede borrar un alimento que forma parte de una receta.
        builder.HasOne(i => i.Food).WithMany().HasForeignKey(i => i.FoodId).OnDelete(DeleteBehavior.NoAction);
        builder.ToTable(t => t.HasCheckConstraint("CK_RecipeIngredient_Quantity", "\"QuantityGrams\" > 0"));
    }
}
