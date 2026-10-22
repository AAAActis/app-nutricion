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

    public Task DeleteWithDataAsync(int userId) => Task.CompletedTask;
}
