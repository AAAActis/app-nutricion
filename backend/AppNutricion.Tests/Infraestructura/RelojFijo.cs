namespace AppNutricion.Tests.Infraestructura;

// Reloj que siempre devuelve la misma hora: para afirmar fechas exactas.
internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}
