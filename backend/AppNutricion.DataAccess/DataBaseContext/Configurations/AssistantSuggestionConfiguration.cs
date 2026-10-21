using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppNutricion.DataAccess.DataBaseContext.Configurations;

public class AssistantSuggestionConfiguration : IEntityTypeConfiguration<AssistantSuggestion>
{
    public void Configure(EntityTypeBuilder<AssistantSuggestion> builder)
    {
        builder.HasIndex(s => new { s.UserId, s.Status });
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ChatMessage>().WithMany().HasForeignKey(s => s.ChatMessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Food).WithMany().HasForeignKey(s => s.FoodId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MealEntry>().WithMany().HasForeignKey(s => s.MealEntryId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t => t.HasCheckConstraint("CK_AssistantSuggestion_Quantity", "\"QuantityGrams\" > 0"));
    }
}
