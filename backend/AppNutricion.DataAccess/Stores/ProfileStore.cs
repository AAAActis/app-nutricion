using AppNutricion.DataAccess.DataBaseContext;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppNutricion.DataAccess.Stores;

public class ProfileStore(ApplicationDbContext context) : IProfileStore
{
    public Task<Profile?> GetByUserIdAsync(int userId) =>
        context.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<bool> UpsertAsync(Profile profile)
    {
        var current = await context.Profiles.FirstOrDefaultAsync(p => p.UserId == profile.UserId);
        if (current is null)
        {
            context.Profiles.Add(profile);
            await context.SaveChangesAsync();
            return true;
        }

        current.BirthDate = profile.BirthDate;
        current.BiologicalSex = profile.BiologicalSex;
        current.HeightCm = profile.HeightCm;
        current.ActivityLevel = profile.ActivityLevel;
        current.TimeZoneId = profile.TimeZoneId;
        current.UpdatedAt = profile.UpdatedAt;
        await context.SaveChangesAsync();
        profile.Id = current.Id;
        return false;
    }
}
