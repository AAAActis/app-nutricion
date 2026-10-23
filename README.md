# Aplicación de Nutrición Inteligente

API en ASP.NET Core (.NET 10, arquitectura N-Capas) + EF Core + PostgreSQL, con frontend web y asistente IA.
Materia Programación (PLA II), ESCMB / UNC. Equipo: Javi, Lauti, Santi.

> README general: lo completa Javi (J14). Esta primera versión cubre la base de datos (S1).

## Requisitos

- Docker Desktop (o Docker Engine + Compose v2)
- SDK de .NET 10
- Herramienta de EF Core: `dotnet tool install --global dotnet-ef`

## Base de datos (Docker Compose)

PostgreSQL 16 en el contenedor `app-nutricion-db`, publicado solo en `127.0.0.1:5432`, con volumen persistente `pgdata` y healthcheck.

### 1. Configurar

```powershell
Copy-Item .env.example .env      # Linux/macOS: cp .env.example .env
# Editar .env: POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
```

### 2. Comandos

| Qué | Comando |
|---|---|
| Levantar | `docker compose up -d` |
| Ver estado (tiene que decir `healthy`) | `docker compose ps` |
| Ver logs | `docker compose logs -f db` |
| Entrar a psql | `docker compose exec db psql -U <POSTGRES_USER> -d <POSTGRES_DB>` |
| Bajar (los datos quedan) | `docker compose down` |
| Reiniciar desde cero (**borra los datos**) | `docker compose down -v` y después `docker compose up -d` |

> Cambiar `POSTGRES_PASSWORD` en el `.env` **no** cambia la contraseña de una base ya creada: Postgres solo la lee al inicializar el volumen. Para aplicarla hay que reiniciar desde cero.

### 3. Conectar la API (user-secrets, una vez por máquina)

```powershell
dotnet user-secrets init --project backend/AppNutricion.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project backend/AppNutricion.Api
```

Los secretos quedan fuera del repo (en tu perfil de usuario), no en `appsettings.json`.

Asistente con el LLM real (opcional: sin clave usa el modelo Fake). En Development apunta a Gemini `gemini-3.8-flash` por su endpoint compatible con OpenAI (D24); la clave se saca en https://aistudio.google.com/apikey:

```powershell
dotnet user-secrets set "Llm:ApiKey" "<GEMINI_API_KEY>" --project backend/AppNutricion.Api
```

Para pasar a otro proveedor compatible con OpenAI se cambian solo `Llm:BaseUrl`, `Llm:Model` y la clave.

### 4. Migraciones

```powershell
# Aplicar las migraciones existentes (lo que hace cada uno después de clonar o hacer pull)
dotnet ef database update --project backend/AppNutricion.DataAccess --startup-project backend/AppNutricion.Api

# Crear una migración nueva (solo quien cambia el modelo, en su rama, y la commitea)
dotnet ef migrations add <NombreEnPascalCase> --project backend/AppNutricion.DataAccess --startup-project backend/AppNutricion.Api --output-dir Migrations
```

Regla del equipo: **una migración por PR**, y si dos PR traen migraciones, el segundo en mergearse regenera la suya sobre `develop` actualizado.

El modelo de datos está documentado en [`docs/MODELO-DATOS.md`](docs/MODELO-DATOS.md). El estado del proyecto, en [`docs/ESTADO.md`](docs/ESTADO.md).

## Datos de demo

En Development (`Demo:Enabled = true` en `appsettings.Development.json`; en `appsettings.json` es `false`), al arrancar la API carga un usuario de demo **con los managers de la API**, con las mismas validaciones que los endpoints: nunca escribe directo en la base. Es idempotente: si lo que carga ya está, no agrega nada.

| Dato | Valor |
|---|---|
| Email | `demo@example.com` |
| Contraseña | `demo-nutricion-2026` |

Son credenciales de demo, no un secreto. El usuario trae consentimiento, perfil, 14 días de peso hasta hoy con tendencia a la baja (se ve la línea de tendencia), un objetivo LoseFat activo, comidas de esos días con alimentos del seed (pollo, arroz, huevo, pan francés, banana, avena y queso; hoy, solo desayuno y almuerzo), 3 entrenamientos con la rutina precargada "Torso/Pierna (4 días)", una receta y un favorito. Las fechas son relativas al día en que arranca la API. Necesita los seeds de alimentos y de rutinas, que se cargan antes.
