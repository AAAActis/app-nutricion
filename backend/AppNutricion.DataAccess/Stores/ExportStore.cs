using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

// Solo lectura (AsNoTracking). Cada consulta filtra por el usuario; RefreshTokens no se lee nunca.
public class ExportStore(ApplicationDbContext context) : IExportStore
{
    public async Task<UserDataExport?> GetAsync(int userId)
    {
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        return new UserDataExport(
            user,
            await context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId),
            await context.BodyMetrics.AsNoTracking().Where(b => b.UserId == userId).OrderBy(b => b.Date).ToListAsync(),
            await context.Goals.AsNoTracking().Where(g => g.UserId == userId).OrderBy(g => g.CreatedAt).ThenBy(g => g.Id).ToListAsync(),
            await context.MealEntries.AsNoTracking().Where(m => m.UserId == userId)
                .OrderBy(m => m.Date).ThenBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync(),
            await context.FavoriteFoods.AsNoTracking().Include(f => f.Food).Where(f => f.UserId == userId)
                .OrderBy(f => f.CreatedAt).ToListAsync(),
            await context.Foods.AsNoTracking().Where(f => f.CreatedByUserId == userId).OrderBy(f => f.Id).ToListAsync(),
            await context.Recipes.AsNoTracking().Where(r => r.UserId == userId)
                .Include(r => r.Ingredients.OrderBy(i => i.Id)).ThenInclude(i => i.Food)
                .OrderBy(r => r.Id).AsSplitQuery().ToListAsync(),
            // Solo las rutinas propias: las precargadas (UserId null) no son datos del usuario.
            await context.Routines.AsNoTracking().Where(r => r.UserId == userId)
                .Include(r => r.Days.OrderBy(d => d.DayNumber)).ThenInclude(d => d.Exercises.OrderBy(e => e.Order)).ThenInclude(e => e.Exercise)
                .OrderBy(r => r.Id).AsSplitQuery().ToListAsync(),
            await context.WorkoutLogs.AsNoTracking().Where(w => w.UserId == userId)
                .Include(w => w.Sets.OrderBy(s => s.SetNumber)).ThenInclude(s => s.Exercise)
                .OrderBy(w => w.Date).ThenBy(w => w.Id).AsSplitQuery().ToListAsync(),
            await context.Conversations.AsNoTracking().Where(c => c.UserId == userId)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id))
                .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).AsSplitQuery().ToListAsync(),
            await context.AssistantSuggestions.AsNoTracking().Include(s => s.Food).Where(s => s.UserId == userId)
                .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToListAsync());
    }
}
