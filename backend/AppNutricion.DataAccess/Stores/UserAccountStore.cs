using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppNutricion.DataAccess.Stores;

public class UserAccountStore(ApplicationDbContext context) : IUserAccountStore
{
    private const string EmailIndexName = "IX_Users_Email";

    public Task<User?> GetByEmailAsync(string normalizedEmail) =>
        context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail);

    public Task<User?> GetByIdAsync(int id) =>
        context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

    public async Task<bool> TryAddAsync(User user)
    {
        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        // Solo el duplicado de email es "ya registrado": cualquier otra violación es un bug y sigue como 500.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: EmailIndexName })
        {
            context.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    // En Postgres, borrar solo el User falla si un Food Custom suyo está en una Recipe suya: la cascada
    // User → Foods choca con RecipeIngredients.FoodId (NoAction) antes de que la cascada User → Recipes
    // borre los ingredientes. Por eso primero las Recipes (sus ingredientes caen en cascada) y después el User.
    public async Task DeleteWithDataAsync(int userId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Recipes.Where(r => r.UserId == userId).ExecuteDeleteAsync();
        await context.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    }
}
