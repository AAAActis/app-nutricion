using AppNutricion.Entities.Enums;

namespace AppNutricion.Entities.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
