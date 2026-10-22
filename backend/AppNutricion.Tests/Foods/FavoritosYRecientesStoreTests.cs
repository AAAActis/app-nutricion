using AppNutricion.DataAccess.Stores;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AppNutricion.Tests.Foods;

public sealed class FavoritosYRecientesStoreTests : IDisposable
{
    private const int Yo = 1;
    private const int Otro = 2;
    private static readonly DateTime Base = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    private readonly BaseSqlite _db = new(Yo, Otro);
    private readonly int _pan, _leche, _arroz, _customDeOtro;

    public FavoritosYRecientesStoreTests()
    {
        using var context = _db.NuevoContexto();
        var foods = new[]
        {
            Alimento("Pan"), Alimento("Leche"), Alimento("Arroz"),
            Alimento("Torta de otro", FoodSource.Custom, Otro),
        };
        context.Foods.AddRange(foods);
        context.SaveChanges();
        (_pan, _leche, _arroz, _customDeOtro) = (foods[0].Id, foods[1].Id, foods[2].Id, foods[3].Id);
    }

    public void Dispose() => _db.Dispose();

    private static Food Alimento(string nombre, FoodSource source = FoodSource.Argenfoods, int? owner = null) => new()
    {
        Name = nombre, NormalizedName = nombre.ToLowerInvariant(), Source = source, CreatedByUserId = owner,
        ExternalId = owner is null ? nombre : null, CreatedAt = Base,
    };

    private FoodStore NuevoStore() => new(_db.NuevoContexto());

    private async Task Comer(int userId, int? foodId, int minuto)
    {
        await using var context = _db.NuevoContexto();
        context.MealEntries.Add(new MealEntry
        {
            UserId = userId, FoodId = foodId, ItemName = "x", Date = new DateOnly(2026, 10, 20), MealType = MealType.Lunch,
            QuantityGrams = 100, CreatedAt = Base.AddMinutes(minuto),
        });
        await context.SaveChangesAsync();
    }

    private async Task Favorito(int userId, int foodId, int minuto)
    {
        await using var context = _db.NuevoContexto();
        context.FavoriteFoods.Add(new FavoriteFood { UserId = userId, FoodId = foodId, CreatedAt = Base.AddMinutes(minuto) });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddFavorite_DosVeces_QuedaUnaSolaFilaSinError()
    {
        await NuevoStore().AddFavoriteAsync(Yo, _pan);
        await NuevoStore().AddFavoriteAsync(Yo, _pan);

        await using var context = _db.NuevoContexto();
        var favorito = Assert.Single(await context.FavoriteFoods.ToListAsync());
        Assert.Equal((Yo, _pan), (favorito.UserId, favorito.FoodId));
        Assert.NotEqual(default, favorito.CreatedAt);
    }

    // Dos POST simultáneos: el otro request inserta la fila después del AnyAsync de este y antes de su insert.
    // Choca en la PK y se trata como "ya existía".
    [Fact]
    public async Task AddFavorite_OtroRequestLoInsertaJustoAntes_NoFallaYQuedaUnaFila()
    {
        var store = new FoodStore(_db.NuevoContexto(new OtroRequestInserta(() => Favorito(Yo, _pan, 0))));

        await store.AddFavoriteAsync(Yo, _pan);

        await using var context = _db.NuevoContexto();
        var favorito = Assert.Single(await context.FavoriteFoods.ToListAsync());
        Assert.Equal((Yo, _pan), (favorito.UserId, favorito.FoodId));
    }

    private sealed class OtroRequestInserta(Func<Task> insertar) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await insertar();
            return result;
        }
    }

    [Fact]
    public async Task RemoveFavorite_SoloElDelUsuario_YSiNoExisteNoFalla()
    {
        await Favorito(Yo, _pan, 0);
        await Favorito(Otro, _pan, 0);

        await NuevoStore().RemoveFavoriteAsync(Yo, _pan);
        await NuevoStore().RemoveFavoriteAsync(Yo, _pan);

        await using var context = _db.NuevoContexto();
        Assert.Equal(Otro, Assert.Single(await context.FavoriteFoods.ToListAsync()).UserId);
    }

    [Fact]
    public async Task GetFavorites_SoloDelUsuarioYVisibles_ElUltimoAgregadoPrimero()
    {
        await Favorito(Yo, _pan, 0);
        await Favorito(Yo, _arroz, 5);
        await Favorito(Yo, _customDeOtro, 9);   // ya no lo ve (no es suyo)
        await Favorito(Otro, _leche, 10);

        var favoritos = await NuevoStore().GetFavoritesAsync(Yo);

        Assert.Equal(["Arroz", "Pan"], favoritos.Select(f => f.Name));
    }

    [Fact]
    public async Task GetRecent_SinDuplicados_DelMasRecienteAlMasViejo_SoloDelUsuario()
    {
        await Comer(Yo, _pan, 1);
        await Comer(Yo, _arroz, 2);
        await Comer(Yo, _leche, 0);
        await Comer(Yo, _pan, 3);
        await Comer(Yo, null, 9);               // alimento borrado
        await Comer(Yo, _customDeOtro, 8);      // ya no lo ve
        await Comer(Otro, _leche, 10);

        var recientes = await NuevoStore().GetRecentAsync(Yo, 10);

        Assert.Equal(["Pan", "Arroz", "Leche"], recientes.Select(f => f.Name));
    }

    [Fact]
    public async Task GetRecent_RespetaElLimite()
    {
        await Comer(Yo, _pan, 1);
        await Comer(Yo, _arroz, 2);
        await Comer(Yo, _leche, 3);

        var recientes = await NuevoStore().GetRecentAsync(Yo, 2);

        Assert.Equal(["Leche", "Arroz"], recientes.Select(f => f.Name));
    }
}
