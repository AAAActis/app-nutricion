using AppNutricion.DataAccess.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AppNutricion.Tests.Infraestructura;

// API completa con usuarios, perfiles, pesos y objetivos en memoria, y un reloj fijo.
// Ahora = 22/10/2026 02:00 UTC: en Argentina (UTC−3) todavía es 21/10. Así se nota si "hoy" sale en UTC.
// Ojo: JwtBearer valida el vencimiento con la hora real, no con este reloj: los tokens se arman con TokensDePrueba.
public class ApiFactoryPerfil : ApiFactoryAuth
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 22, 2, 0, 0, TimeSpan.Zero);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new RelojFijo(Ahora)));
            services.Replace(ServiceDescriptor.Singleton<IProfileStore, ProfileStoreEnMemoria>());
            services.Replace(ServiceDescriptor.Singleton<IBodyMetricStore, BodyMetricStoreEnMemoria>());
            services.Replace(ServiceDescriptor.Singleton<IGoalStore, GoalStoreEnMemoria>());
        });
    }
}
