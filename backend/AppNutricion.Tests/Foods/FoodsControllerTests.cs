using System.Net;
using AppNutricion.Tests.Infraestructura;
using Moq;

namespace AppNutricion.Tests.Foods;

public class FoodsControllerTests : IClassFixture<ApiFactoryAutenticada>
{
    private readonly ApiFactoryAutenticada _factory;

    public FoodsControllerTests(ApiFactoryAutenticada factory)
    {
        _factory = factory;
        _factory.FoodManager.Reset();
    }

    [Fact]
    public async Task GetByBarcode_CodigoInvalido_Devuelve400ComoProblemDetails()
    {
        _factory.FoodManager.Setup(m => m.GetByBarcodeAsync("abc", null))
            .ThrowsAsync(new ArgumentException("El código de barras debe tener entre 8 y 14 dígitos."));

        var response = await _factory.CrearCliente(null).GetAsync("/api/foods/barcode/abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("entre 8 y 14", await response.Content.ReadAsStringAsync());
    }
}
