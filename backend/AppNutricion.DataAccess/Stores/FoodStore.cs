using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class FoodStore(ApplicationDbContext context) : IFoodStore
{
    public Task<List<Food>> SearchByNameAsync(string normalizedTerm, int? userId, int limit)
    {
        // % y _ del usuario se escapan para que no funcionen como comodines del LIKE.
        var pattern = "%" + normalizedTerm.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return Visible(userId)
            .Where(f => EF.Functions.ILike(f.NormalizedName, pattern, "\\"))
            .OrderBy(f => f.NormalizedName == normalizedTerm ? 0 : f.NormalizedName.StartsWith(normalizedTerm) ? 1 : 2)
            .ThenBy(f => f.NormalizedName.Length)
            .ThenBy(f => f.NormalizedName)
            .Take(limit)
            .ToListAsync();
    }

    public Task<Food?> GetByIdAsync(int id, int? userId) =>
        Visible(userId).FirstOrDefaultAsync(f => f.Id == id);

    public Task<Food?> GetByBarcodeAsync(string barcode, int? userId) =>
        Visible(userId).Where(f => f.Barcode == barcode).OrderBy(f => f.Id).FirstOrDefaultAsync();

    private IQueryable<Food> Visible(int? userId) =>
        context.Foods.AsNoTracking().Where(f => f.CreatedByUserId == null || f.CreatedByUserId == userId);
}
