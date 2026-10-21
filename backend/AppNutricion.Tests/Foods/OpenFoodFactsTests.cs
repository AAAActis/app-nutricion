using System.Net;
using System.Text;
using AppNutricion.BusinessLogic.Managers;
using AppNutricion.BusinessLogic.Services;
using AppNutricion.DataAccess.Interfaces;
using AppNutricion.Entities.Entities;
using AppNutricion.Entities.Enums;
using AppNutricion.Presentation.Output;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AppNutricion.Tests.Foods;

public class OpenFoodFactsTests
{
    private const string Code = "7790895000997";

    private readonly Mock<IFoodStore> _store = new();
    private readonly ListLogger<OpenFoodFactsService> _offLogger = new();
    private readonly FakeHandler _handler = new();
    private readonly HttpClient _http;
    private readonly FoodManager _manager;
    private List<Food>? _saved;

    public OpenFoodFactsTests()
    {
        _store.Setup(s => s.UpsertSeedAsync(It.IsAny<IEnumerable<Food>>()))
            .Callback<IEnumerable<Food>>(foods => _saved = foods.ToList())
            .ReturnsAsync((1, 0));
        _http = new HttpClient(_handler);
        OpenFoodFactsService.Configure(_http);
        _manager = new FoodManager(_store.Object, new OpenFoodFactsService(_http, _offLogger), NullLogger<FoodManager>.Instance);
    }

    private const string FullProduct = """
        {"code":"7790895000997","status":"success","product":{"product_name":" Coca-Cola Sabor Original ","brands":"Coca-Cola, The Coca-Cola Company",
         "nutriments":{"energy-kcal_100g":42,"energy-kj_100g":176,"proteins_100g":0,"carbohydrates_100g":10.6,"fat_100g":0}}}
        """;

    private void Respond(HttpStatusCode status, string body) =>
        _handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    [Fact]
    public async Task EstaEnLaBase_NoLlamaAOpenFoodFacts()
    {
        var food = new Food { Name = "Coca-Cola" };
        _store.Setup(s => s.GetByBarcodeAsync(Code, null)).ReturnsAsync(food);
        Respond(HttpStatusCode.OK, FullProduct);

        Assert.Same(food, await _manager.GetByBarcodeAsync(Code, null));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task NoEstaYOpenFoodFactsLoTrae_LoGuardaYLoDevuelve()
    {
        Respond(HttpStatusCode.OK, FullProduct);
        var before = DateTime.UtcNow;

        var result = await _manager.GetByBarcodeAsync(Code, null);

        Assert.NotNull(_saved);
        var saved = Assert.Single(_saved);
        Assert.Same(saved, result);
        Assert.Equal(FoodSource.OpenFoodFacts, saved.Source);
        Assert.Equal(Code, saved.ExternalId);
        Assert.Equal(Code, saved.Barcode);
        Assert.Null(saved.CreatedByUserId);
        Assert.Equal("Coca-Cola Sabor Original", saved.Name);
        Assert.Equal("coca-cola sabor original", saved.NormalizedName);
        Assert.Equal("Coca-Cola", saved.Brand); // solo la primera marca
        Assert.Equal(42m, saved.CaloriesPer100g);
        Assert.Equal(0m, saved.ProteinPer100g);
        Assert.Equal(10.6m, saved.CarbsPer100g);
        Assert.Equal(0m, saved.FatPer100g);
        Assert.True(saved.CreatedAt >= before && saved.CreatedAt.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task EnergiaSoloEnKj_SeConvierteAKcal()
    {
        Respond(HttpStatusCode.OK, """
            {"product":{"product_name":"Galletitas","nutriments":{"energy-kj_100g":1882.8,"proteins_100g":7,"carbohydrates_100g":70,"fat_100g":18}}}
            """);

        var result = await _manager.GetByBarcodeAsync(Code, null);

        Assert.NotNull(result);
        Assert.Equal(450m, result.CaloriesPer100g); // 1882.8 / 4.184
    }

    [Fact]
    public async Task EnergiaEnKjConDecimales_SeRedondeaA2()
    {
        Respond(HttpStatusCode.OK, """
            {"product":{"product_name":"Agua saborizada","nutriments":{"energy-kj_100g":126,"proteins_100g":0,"carbohydrates_100g":7.5,"fat_100g":0}}}
            """);

        var result = await _manager.GetByBarcodeAsync(Code, null);

        Assert.NotNull(result);
        Assert.Equal(30.11m, result.CaloriesPer100g); // 126 / 4.184 = 30.114...
    }

    [Theory]
    [InlineData("""{"energy-kcal_100g":42,"carbohydrates_100g":10.6,"fat_100g":0}""")]
    [InlineData("""{"energy-kcal_100g":42,"proteins_100g":0,"fat_100g":0}""")]
    [InlineData("""{"energy-kcal_100g":42,"proteins_100g":0,"carbohydrates_100g":10.6}""")]
    [InlineData("""{"proteins_100g":0,"carbohydrates_100g":10.6,"fat_100g":0}""")]
    public async Task OpenFoodFactsSinEnergiaOMacros_NoEncontradoYNoGuarda(string nutriments)
    {
        Respond(HttpStatusCode.OK, $$$"""{"product":{"product_name":"Algo","nutriments":{{{nutriments}}}}}""");

        Assert.Null(await _manager.GetByBarcodeAsync(Code, null));
        Assert.Null(_saved);
    }

    [Fact]
    public async Task OpenFoodFactsConDatosFueraDeRango_NoEncontradoYNoGuarda()
    {
        Respond(HttpStatusCode.OK, """
            {"product":{"product_name":"Raro","nutriments":{"energy-kcal_100g":950,"proteins_100g":0,"carbohydrates_100g":0,"fat_100g":100}}}
            """);

        Assert.Null(await _manager.GetByBarcodeAsync(Code, null));
        Assert.Null(_saved);
    }

    [Fact]
    public async Task OpenFoodFactsNoLoTiene_NoEncontrado()
    {
        Respond(HttpStatusCode.NotFound, """{"code":"7790895000997","status":"failure","result":{"id":"product_not_found"}}""");

        var ex = await Record.ExceptionAsync(() => _manager.GetByBarcodeAsync(Code, null));

        Assert.Null(ex);
        Assert.Null(_saved);
    }

    [Fact]
    public async Task OpenFoodFactsEnTimeout_NoEncontradoSinExcepcionYConWarning()
    {
        _http.Timeout = TimeSpan.FromMilliseconds(100);
        _handler.Respond = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        Food? result = null;
        var ex = await Record.ExceptionAsync(async () => result = await _manager.GetByBarcodeAsync(Code, null));

        Assert.Null(ex);
        Assert.Null(result);
        Assert.Single(_offLogger.Warnings);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task OpenFoodFactsCon5xx_NoEncontradoSinExcepcionYConWarning(HttpStatusCode status)
    {
        Respond(status, "<html>error</html>");

        Food? result = null;
        var ex = await Record.ExceptionAsync(async () => result = await _manager.GetByBarcodeAsync(Code, null));

        Assert.Null(ex);
        Assert.Null(result);
        Assert.Single(_offLogger.Warnings);
    }

    [Fact]
    public async Task SinRed_NoEncontradoSinExcepcionYConWarning()
    {
        _handler.Respond = (_, _) => throw new HttpRequestException("sin red");

        Food? result = null;
        var ex = await Record.ExceptionAsync(async () => result = await _manager.GetByBarcodeAsync(Code, null));

        Assert.Null(ex);
        Assert.Null(result);
        Assert.Single(_offLogger.Warnings);
    }

    [Fact]
    public async Task Configure_UsaV3UserAgentPropioYTimeoutDe5Segundos()
    {
        var http = new HttpClient(_handler);
        OpenFoodFactsService.Configure(http);
        Respond(HttpStatusCode.OK, FullProduct);

        await new OpenFoodFactsService(http, _offLogger).GetByBarcodeAsync(Code);

        Assert.Equal(TimeSpan.FromSeconds(5), http.Timeout);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal("world.openfoodfacts.org", request.RequestUri!.Host);
        Assert.Equal($"/api/v3/product/{Code}.json", request.RequestUri.AbsolutePath);
        Assert.Equal("AppNutricion/1.0 (PLA II UNC)", request.Headers.UserAgent.ToString());
    }

    [Fact]
    public void FoodOutput_DeOpenFoodFacts_LlevaAtribucion()
    {
        var off = FoodOutput.From(new Food { Name = "Coca-Cola", Source = FoodSource.OpenFoodFacts });
        var own = FoodOutput.From(new Food { Name = "Manzana", Source = FoodSource.Argenfoods });

        Assert.Equal("OpenFoodFacts", off.Source);
        Assert.Equal("Open Food Facts (ODbL)", off.Attribution);
        Assert.Null(own.Attribution);
    }

    private class FakeHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Respond(request, cancellationToken);
        }
    }
}
