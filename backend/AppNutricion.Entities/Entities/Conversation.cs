namespace AppNutricion.Entities.Entities;

public class Conversation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
