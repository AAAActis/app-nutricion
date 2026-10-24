# Aplicación de Nutrición Inteligente

API en ASP.NET Core (.NET 10, arquitectura N-Capas) + EF Core + PostgreSQL, con frontend web y asistente IA.
Materia Programación (PLA II), ESCMB / UNC. Equipo: Javi, Lauti, Santi.

La persona se registra (con consentimiento, Ley 25.326), carga su perfil y su peso, elige un objetivo y la app le calcula calorías y macros (Mifflin-St Jeor, con límites de seguridad). Después busca alimentos (ARGENFOODS + Open Food Facts), registra lo que come, ve el resumen del día y de la semana, arma recetas, registra entrenamientos con rutinas y charla con un asistente que **propone** comidas: nunca escribe solo en la base.

## Qué hay

| Módulo | Endpoints principales | Pantalla |
|---|---|---|
| Cuenta | `POST /api/auth/register`, `POST /api/auth/login`, `GET/DELETE /api/me`, `GET /api/me/export` | Ingresar, Crear cuenta, Cuenta |
| Perfil, peso y objetivos | `GET/PUT /api/me/profile`, `GET/POST /api/me/metrics`, `GET/POST /api/me/goals`, `GET /api/me/targets` | Perfil, Peso, Objetivo |
| Alimentos | `GET /api/foods/search?q=`, `GET /api/foods/barcode/{code}`, favoritos, recientes, `/api/foods/custom` | Mi día |
| Comidas y resúmenes | `/api/meals` (alta, edición, baja, copiar, desde receta, CSV), `GET /api/summary/daily`, `GET /api/summary/weekly` | Mi día |
| Recetas | `/api/recipes` | Mis alimentos y Recetas (PR #25) |
| Entrenamiento | `GET /api/exercises`, `/api/routines`, `/api/workouts` | Rutinas |
| Asistente | `/api/assistant/conversations`, mensajes, aceptar o ignorar sugerencias | Chat |
| Infraestructura | `GET /health`; Swagger UI en `/swagger` (solo Development) | — |

Ejemplos de cada request en [`backend/AppNutricion.Api/AppNutricion.Api.http`](backend/AppNutricion.Api/AppNutricion.Api.http).

## Arquitectura

```
wwwroot (HTML + JS sin frameworks, mismo origen)
        │  fetch con Bearer
        ▼
Api            Program.cs: JWT, rate limiting, ProblemDetails, CORS, CSP, archivos estáticos
  └─ Presentation   Controllers y DTOs Input/Output (sin lógica). userId siempre del token
       └─ BusinessLogic   Managers (reglas y validaciones), motor de macros, asistente (IChatModel), seeds
            └─ DataAccess      ApplicationDbContext, una configuración por tabla, Stores (filtran por UserId)
                 └─ Entities        POCOs y enums
                                         │
                                         ▼
                                  PostgreSQL 16 (Docker)
```

- Las dependencias solo bajan: `Api` → `Presentation` → `BusinessLogic` → `DataAccess` → `Entities`.
- La API es el único escritor de la base (D5). Calorías y macros salen siempre de `Foods`, nunca del LLM (D6).
- El asistente usa un `IChatModel`: `FakeChatModel` (sin clave) o Gemini por su endpoint compatible con OpenAI (D24). Si el LLM falla, responde con los totales del día.
- Decisiones, estado y tareas: [`docs/ESTADO.md`](docs/ESTADO.md). Modelo de datos: [`docs/MODELO-DATOS.md`](docs/MODELO-DATOS.md). Seguridad: [`docs/AUDITORIA-SEGURIDAD.md`](docs/AUDITORIA-SEGURIDAD.md).

## Requisitos

- Docker Desktop (o Docker Engine + Compose v2)
- SDK de .NET 10
- Herramienta de EF Core: `dotnet tool install --global dotnet-ef`

## Levantar el proyecto

### 1. Base de datos (Docker Compose)

PostgreSQL 16 en el contenedor `app-nutricion-db`, publicado solo en `127.0.0.1:5432`, con volumen persistente `pgdata` y healthcheck.

```powershell
Copy-Item .env.example .env      # Linux/macOS: cp .env.example .env
# Editar .env: POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB
docker compose up -d
docker compose ps                # tiene que decir "healthy"
```

| Qué | Comando |
|---|---|
| Ver logs | `docker compose logs -f db` |
| Entrar a psql | `docker compose exec db psql -U <POSTGRES_USER> -d <POSTGRES_DB>` |
| Bajar (los datos quedan) | `docker compose down` |
| Reiniciar desde cero (**borra los datos**) | `docker compose down -v` y después `docker compose up -d` |

> Cambiar `POSTGRES_PASSWORD` en el `.env` **no** cambia la contraseña de una base ya creada: Postgres solo la lee al inicializar el volumen. Para aplicarla hay que reiniciar desde cero.

### 2. Secretos de la API (user-secrets, una vez por máquina)

```powershell
dotnet user-secrets init --project backend/AppNutricion.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=<POSTGRES_DB>;Username=<POSTGRES_USER>;Password=<POSTGRES_PASSWORD>" --project backend/AppNutricion.Api
dotnet user-secrets set "Jwt:Key" "<al menos 32 caracteres aleatorios>" --project backend/AppNutricion.Api
```

Sin una `Jwt:Key` de 32 bytes o más, la API no arranca. Los secretos quedan fuera del repo (en tu perfil de usuario), nunca en `appsettings.json`. En Docker van como variables de entorno (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Llm__ApiKey`; ver `.env.example`).

Asistente con el LLM real (opcional: sin clave usa el modelo Fake). En Development apunta a Gemini `gemini-3.8-flash` por su endpoint compatible con OpenAI (D24); la clave se saca en https://aistudio.google.com/apikey:

```powershell
dotnet user-secrets set "Llm:ApiKey" "<GEMINI_API_KEY>" --project backend/AppNutricion.Api
```

Para pasar a otro proveedor compatible con OpenAI se cambian solo `Llm:BaseUrl`, `Llm:Model` y la clave.

### 3. Migraciones

```powershell
# Aplicar las migraciones existentes (lo que hace cada uno después de clonar o hacer pull)
dotnet ef database update --project backend/AppNutricion.DataAccess --startup-project backend/AppNutricion.Api

# Crear una migración nueva (solo quien cambia el modelo, en su rama, y la commitea)
dotnet ef migrations add <NombreEnPascalCase> --project backend/AppNutricion.DataAccess --startup-project backend/AppNutricion.Api --output-dir Migrations
```

Regla del equipo: **una migración por PR**, y si dos PR traen migraciones, el segundo en mergearse regenera la suya sobre `develop` actualizado.

### 4. Correr

```powershell
dotnet run --project backend/AppNutricion.Api
```

- App: http://localhost:5286 (el frontend sale de `wwwroot`, mismo origen que la API).
- Swagger UI: http://localhost:5286/swagger (solo en Development).
- Al arrancar se cargan, sin duplicar, los alimentos de `SeedData/foods_seed.json` (65 básicos de ARGENFOODS) y los ejercicios y rutinas precargadas de `SeedData/exercises_seed.json`.

## Datos de demo

En Development (`Demo:Enabled = true` en `appsettings.Development.json`; en `appsettings.json` es `false`), al arrancar la API carga un usuario de demo **con los managers de la API**, con las mismas validaciones que los endpoints: nunca escribe directo en la base. Es idempotente: si lo que carga ya está, no agrega nada.

| Dato | Valor |
|---|---|
| Email | `demo@example.com` |
| Contraseña | `demo-nutricion-2026` |

Son credenciales de demo, no un secreto. El usuario trae consentimiento, perfil, 14 días de peso hasta hoy con tendencia a la baja (se ve la línea de tendencia), un objetivo LoseFat activo, comidas de esos días con alimentos del seed (pollo, arroz, huevo, pan francés, banana, avena y queso; hoy, solo desayuno y almuerzo), 3 entrenamientos con la rutina precargada "Torso/Pierna (4 días)", una receta y un favorito. Las fechas son relativas al día en que arranca la API. Necesita los seeds de alimentos y de rutinas, que se cargan antes.

## Tests

```powershell
dotnet test backend/AppNutricion.slnx
```

xUnit + Moq en `backend/AppNutricion.Tests`, una carpeta por módulo. No necesitan Postgres ni user-secrets: la API se levanta en memoria (`WebApplicationFactory`) con una cadena de conexión y una clave JWT falsas, y los Stores se prueban con SQLite en memoria. Ningún test llama al LLM real.

El equipo trabaja con TDD (primero el test en rojo, después el código) y con un verificador que comprueba ese rojo y aplica mutaciones (`.claude/agents/verificador-tdd.md`).

## CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml): en cada PR, y en cada push a `develop` y `main`, compila la solución y corre los tests con .NET 10. Las acciones están fijadas por SHA y el workflow solo tiene permiso de lectura.

## Flujo de trabajo

- Ramas desde `develop`: `feature/<modulo>-<tema>`, `fix/<tema>`, `docs/<tema>`. PR chicos contra `develop`, revisados por otro integrante.
- Commits semánticos (`feat:`, `fix:`, `test:`, `docs:`, `chore:`).
- Nunca se commitean `.env`, cadenas de conexión ni claves.
- `docs/ESTADO.md` se actualiza antes de cada push.

El modelo de datos está documentado en [`docs/MODELO-DATOS.md`](docs/MODELO-DATOS.md). El estado del proyecto, en [`docs/ESTADO.md`](docs/ESTADO.md).
