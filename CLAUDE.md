# Reglas del proyecto para agentes (Claude Code)

Leer antes de tocar código: `docs/ESTADO.md` (estado y quién hace qué) y `docs/MODELO-DATOS.md` (tablas).

## Arquitectura N-Capas (las dependencias solo bajan)

`Api` → `Presentation` → `BusinessLogic` → `DataAccess` → `Entities`

- `Entities`: POCOs en `/Entities`, enums en `/Enums`. Cero dependencias, cero lógica.
- `DataAccess`: único lugar que habla con Postgres. `ApplicationDbContext` en `/DataBaseContext`, una configuración por tabla en `/DataBaseContext/Configurations`, Stores en `/Stores` con su interfaz en `/Interfaces`, registro en `/Registrations`.
- `BusinessLogic`: reglas, cálculos y APIs externas. Managers (uno por entidad principal) y Services (LLM, Open Food Facts).
- `Presentation`: Controllers, DTOs `/Input` y `/Output`. Sin lógica de negocio ni acceso a datos.

## Reglas de negocio que no se negocian

- La API es el único escritor de la base. n8n y el LLM nunca escriben en ella (D5).
- Calorías y macros salen siempre de la tabla `Foods`, nunca del LLM. El `userId` sale del token JWT, nunca del cliente ni del LLM (D6).
- Toda consulta de datos de usuario filtra por `UserId`. Un usuario jamás ve datos de otro.
- `MealEntry` y `Goal` congelan sus valores al crearse: no se recalculan después.
- Al LLM no se le manda email ni nombre.
- Fechas: `DateTime` siempre en UTC; los días del diario son `DateOnly` en la zona horaria del perfil.

## Git

- Ramas desde `develop`: `feature/<modulo>-<tema>`, `fix/<tema>`. PR chicos, revisados por otro integrante.
- Commits semánticos (`feat:`, `fix:`, `chore:`, `docs:`, `test:`).
- Nunca commitear `.env`, cadenas de conexión ni claves. Secretos en user-secrets.
- Una migración de EF por PR. No editar migraciones ya mergeadas.
- Actualizar `docs/ESTADO.md` antes de cada push.

## Comandos

- Compilar: `dotnet build backend/AppNutricion.slnx`
- Migrar: `dotnet ef database update --project backend/AppNutricion.DataAccess --startup-project backend/AppNutricion.Api`
