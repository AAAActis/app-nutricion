using AppNutricion.BusinessLogic.Interfaces;
using AppNutricion.Entities.Entities;

namespace AppNutricion.BusinessLogic.Managers;

public class ProfileManager : IProfileManager
{
    public Task<Profile?> GetAsync(int userId) => Task.FromResult<Profile?>(null);

    public Task<SaveProfileResult> SaveAsync(int userId, ProfileData data) =>
        Task.FromResult(new SaveProfileResult(new Profile(), false));
}
