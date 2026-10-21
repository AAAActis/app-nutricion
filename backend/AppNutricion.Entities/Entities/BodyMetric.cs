namespace AppNutricion.Entities.Entities;

// Historial de peso. El "peso actual" es el último registro: no se duplica en Profile.
public class BodyMetric
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }                      // un registro por día (índice único)
    public decimal WeightKg { get; set; }
    public DateTime CreatedAt { get; set; }
}
