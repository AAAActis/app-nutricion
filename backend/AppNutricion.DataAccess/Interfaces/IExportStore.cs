using AppNutricion.Entities.Entities;

namespace AppNutricion.DataAccess.Interfaces;

// Todo lo de un usuario para exportar (J5). Las listas vienen con sus hijos (ingredientes, días, series, mensajes).
// Nunca trae RefreshTokens. El User viene con PasswordHash: quien lo serializa lo tiene que mapear a un DTO.
public record UserDataExport(
    User User,
    Profile? Profile,
    List<BodyMetric> BodyMetrics,
    List<Goal> Goals,
    List<MealEntry> MealEntries,
    List<FavoriteFood> FavoriteFoods,
    List<Food> CustomFoods,
    List<Recipe> Recipes,
    List<Routine> Routines,
    List<WorkoutLog> WorkoutLogs,
    List<Conversation> Conversations);

// Solo lectura.
public interface IExportStore
{
    // null = el usuario no existe.
    Task<UserDataExport?> GetAsync(int userId);
}
