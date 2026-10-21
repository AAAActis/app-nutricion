# ESTADO · Nutrición Inteligente (Grupo G)

> Única fuente de verdad para los 3. Se actualiza antes de cada push. Reemplaza a `ESTADO-santi.md` y `ESTADO-lauti.md` (eran el mismo archivo con distinta "Próxima sesión"; ahora están las tres al final).
> Última actualización: 2026-10-21 noche (Lauti, rama `feature/alimentos-busqueda`: L1 y L3 en revisión; `Program.cs` suma `AddBusinessLogic`, `AddControllers` y `MapControllers`). Repo: https://github.com/AAAActis/app-nutricion. `<COMPLETAR>` = dato que falta.
> Verificado por Santi en su máquina: el compose levanta y `app-nutricion-db` queda `healthy`. El estado de ramas y del `.gitignore` se confirma en el PR de `fix/gitignore-utf8`.

## Objetivo

API en C# "Aplicación de Nutrición Inteligente" con frontend simple, funcionando de punta a punta: registro/login → perfil, peso y objetivo → calorías y macros → buscar y cargar comidas → resumen del día → rutinas y entrenamientos → chat con asistente IA. Incluye seguridad, tests y documentación.
**Entrega mínima: martes 2026-10-27** (hora: `<COMPLETAR: hora exacta>`). **Congelamiento de funcionalidades: viernes 2026-10-23 a las 22:00.** Sábado 24 y domingo 25 no se trabaja. Lunes 26 es integración, bugs, README y ensayo.

## Equipo y responsabilidades

| Integrante | Qué tiene a cargo |
|---|---|
| Javi | Núcleo de la API, seguridad, auth, perfil, macros, CI, frontend base, README general |
| Lauti | Alimentos (búsqueda, porciones, carga de `foods_seed`, respaldo Open Food Facts) y comidas (resumen diario/semanal, recetas, favoritos) |
| Santi | Docker Compose, base de datos (entidades, migraciones), `foods_seed`, rutinas y entrenamientos, asistente IA en C#, n8n (extra) |

El reparto lo propuso Javi con apoyo de un asistente de IA. **Falta ratificarlo con Lauti y Santi** (ver Pendientes).

## Estado de las tareas

Estados: Pendiente / En curso / En revisión / Hecha. Rama o PR: `<COMPLETAR>` cuando exista. Convención: ramas desde `develop`, `feature/<modulo>-<tema>`.

**Javi**

| # | Tarea | Responsable | Estado | Rama o PR | Depende de |
|---|---|---|---|---|---|
| J1 | Esqueleto en capas, Swagger, middleware de errores con formato único, health check, CORS, config por entorno | Javi | En curso (parcial): ya existe la solución con 5 proyectos y OpenAPI básico. `Program.cs` sigue siendo la plantilla `weatherforecast`. Falta Swagger UI, middleware de errores, health check, CORS y config | `main` (PR #1) | S1 |
| J2 | Registro con consentimiento, login, JWT, hash de contraseñas | Javi | Pendiente | — | J1, S2 |
| J3 | Refresh token y logout | Javi | Pendiente | — | J2 |
| J4 | Cambio de contraseña y de email | Javi | Pendiente | — | J2 |
| J5 | Borrado de cuenta (con todos sus datos) y exportación de datos del usuario | Javi | Pendiente | — | J2, S2 |
| J6 | Rate limiting, validación de entradas, logs estructurados | Javi | Pendiente | — | J1 |
| J7 | Perfil, historial de peso con tendencia, objetivos con ritmo semanal | Javi | Pendiente | — | J2, S2 |
| J8 | Motor de macros (Mifflin-St Jeor) con tests y límites de seguridad; IMC y proyección de meta | Javi | Pendiente | — | J7 |
| J9 | Paginación y filtros estándar para todas las listas | Javi | Pendiente | — | J1 |
| J10 | Tests de integración de auth y aislamiento entre usuarios | Javi | Pendiente | — | J2 |
| J11 | CI con GitHub Actions (compila y corre tests en cada PR) | Javi | Pendiente | — | J1 |
| J12 | Frontend base: layout, estilos, manejo del token, login/registro, perfil, objetivos, macros, gráfico de peso | Javi | Pendiente | — | J2, J7, J8 |
| J13 | Auditoría de seguridad con checklist, por escrito | Javi | Pendiente | — | J2 a J9 |
| J14 | README general, ejemplos en Swagger, diagrama de arquitectura | Javi | Pendiente | — | todo |
| J15 | Revisar PR de Lauti y probar el recorrido completo | Javi | Pendiente | — | L1 a L12 |

**Lauti**

| # | Tarea | Responsable | Estado | Rama o PR | Depende de |
|---|---|---|---|---|---|
| L1 | Módulo de alimentos: búsqueda por nombre sin tildes y con ranking, y porciones | Lauti | Hecha | PR #5 | S2 (tabla `Foods`) |
| L2 | Carga del archivo `foods_seed`, idempotente (no duplica) | Lauti | En curso | `feature/alimentos-seed-off` | S3, L1 |
| L3 | Búsqueda por código de barras | Lauti | Hecha | PR #5 | L1 |
| L4 | Comidas: alta, edición, baja y lista; macros guardados al momento, tipo de comida, zona horaria | Lauti | Pendiente | — | J2, S2, L1 |
| L5 | Copiar comidas del día anterior, favoritos y recientes | Lauti | Pendiente | — | L4 |
| L6 | Resumen diario vs. objetivos (con lo que falta) y resumen semanal con racha | Lauti | Pendiente | — | L4, J8 |
| L7 | Respaldo con Open Food Facts (si no está en nuestra base, buscar en su API y guardar con su fuente) | Lauti | En curso | `feature/alimentos-seed-off` | L1. Verificar antes API, límites y licencia |
| L8 | Alimentos personalizados por usuario, separados de los compartidos | Lauti | Pendiente | — | L1 |
| L9 | Recetas: combinación de alimentos con macros totales y por porción | Lauti | Pendiente | — | L1 |
| L10 | Exportar historial de comidas a CSV | Lauti | Pendiente | — | L4 |
| L11 | Tests de comidas, resumen y aislamiento entre usuarios | Lauti | Pendiente | — | L4, L6 |
| L12 | Pantallas: buscador, registrar comida, resumen del día, historial semanal con gráficos, favoritos y recetas | Lauti | Pendiente | — | J12 (layout), L1 a L9 |
| L13 | Revisar PR de Santi y probar el recorrido completo | Lauti | Pendiente | — | S2 a S12 |

**Santi**

| # | Tarea | Responsable | Estado | Rama o PR | Depende de |
|---|---|---|---|---|---|
| S1 | Docker Compose con Postgres, volumen y healthcheck; `.env.example`; `.env` en `.gitignore`; README del compose | Santi | En revisión: compose levantado y `healthy` en la máquina de Santi; README del compose escrito; puerto restringido a `127.0.0.1`; `.gitignore` reescrito en UTF-8. Falta que Javi y Lauti lo levanten en sus máquinas | `fix/gitignore-utf8`, `feature/db-modelo-datos` | — |
| S2 | Base de datos: entidades, DbContext, migraciones, índices y relaciones (acordado con Javi y Lauti) | Santi | En revisión: 19 tablas, configuración por tabla, migración `InitialCreate`. Modelo en `docs/MODELO-DATOS.md`. Falta ratificarlo en la reunión y el review de Lauti | `feature/db-modelo-datos` | S1, acuerdo de los 3 |
| S3 | Archivo `foods_seed`: 50 a 100 básicos de ARGENFOODS + productos de Open Food Facts con macros completos, normalizado a 100 g; que otro lo revise | Santi | Pendiente | — | Verificar fuentes |
| S4 | Dockerfile de la API y compose completo | Santi | Pendiente | — | S1, J1 |
| S5 | Scripts de respaldo y restauración de la base | Santi | Pendiente | — | S1 |
| S6 | Seeds de demo: usuario de ejemplo con 2 semanas de peso, comidas y entrenamientos; 2 o 3 rutinas | Santi | Pendiente | — | S2 |
| S7 | Rutinas y entrenamientos: endpoints, registro de lo realizado, tests | Santi | Pendiente | — | J2, S2 |
| S8 | Progreso por ejercicio y comparación entrenamiento vs. ingesta (ajuste de calorías como estimación) | Santi | Pendiente | — | S7, L6 |
| S9 | Asistente IA en C#: cliente del LLM detrás de interfaz, herramientas, contexto sin email ni nombre, guardrails, fallback | Santi | Pendiente | — | J8, L6 (mientras, datos simulados). Proveedor LLM pendiente |
| S10 | Historial persistente de conversaciones y sugerencias aceptar/ignorar | Santi | Pendiente | — | S9 |
| S11 | Tests del asistente con LLM falso y casos fijos | Santi | Pendiente | — | S9 |
| S12 | Pantallas: rutinas, progreso de entrenamiento y chat | Santi | Pendiente | — | J12, S7, S9 |
| S13 | n8n (extra): workflow del asistente como alternativa y workflow de importación de Open Food Facts que genere el archivo. n8n nunca escribe en la base | Santi | Pendiente | — | S9. Solo si lo anterior está hecho |
| S14 | Guion de la demo y video corto de respaldo | Santi | Pendiente | — | todo |
| S15 | Revisar PR de Javi y probar el recorrido completo | Santi | Pendiente | — | J1 a J12 |

## Decisiones tomadas

| # | Decisión | Fecha | Quién decidió |
|---|---|---|---|
| D1 | Lenguaje C#. Entrega mínima martes 27/10. Se evalúa todo salvo n8n | 2026-10-20 | Javi (respuestas sobre la consigna de la materia) |
| D2 | n8n es opcional y no se evalúa; sale del camino crítico | 2026-10-20 | Javi |
| D3 | Todos pueden correr Docker | 2026-10-20 | Javi |
| D4 | Stack: ASP.NET Core Web API, EF Core con migraciones, PostgreSQL en Docker Compose, JWT + hash, frontend HTML/JS en `wwwroot`, tests xUnit | 2026-10-20 | Propuesta del asistente de IA, aceptada por Javi. **Ratificar con Lauti y Santi** |
| D5 | La API es el único escritor de la base. n8n y el LLM nunca escriben en ella | 2026-10-21 | Javi |
| D6 | Calorías y macros salen siempre de la tabla de alimentos, nunca del LLM. El `userId` sale del token, nunca del LLM | 2026-10-21 | Javi |
| D7 | Fitia descartada como fuente (base privada, sin scraping). Fuentes: ARGENFOODS + Open Food Facts Argentina | 2026-10-21 | Javi |
| D8 | El bot es el chat dentro de la propia app (no Telegram ni WhatsApp) | 2026-10-21 | Javi |
| D9 | El proyecto lo hacen 3 (Javi, Lauti, Santi). Repo: `app-nutricion` | 2026-10-21 | Javi |
| D10 | Sábado 24 y domingo 25 no se trabaja. Congelamiento viernes 23 a las 22:00 | 2026-10-21 | Javi |
| D11 | Arquitectura en capas ya creada en el repo: `Api` → `Presentation` → `BusinessLogic` → `DataAccess` → `Entities` (.NET 10, EF Core 10 + Npgsql). Sin tests ni Dockerfile todavía | 2026-10-21 | Santi (commit "Creación arquitectura base"; autor inferido por el nombre del commit) |
| D12 | PostgreSQL 16 en Docker, contenedor `app-nutricion-db`, puerto 5432, volumen `pgdata` | 2026-10-21 | Santi (commit "Configuración inicial"; autor inferido) |
| D13 | Modelo de datos de `docs/MODELO-DATOS.md`: ids `int`, enums como texto, `DateTime` en UTC, días como `DateOnly`, valores congelados en `MealEntry`/`Goal`/`WorkoutLog`, borrado en cascada desde `User`. Peso actual = último `BodyMetric`; edad = desde `BirthDate` | 2026-10-21 | Santi (a ratificar con Javi y Lauti) |
| D14 | Un solo `docs/ESTADO.md` para los 3 (no una copia por persona) | 2026-10-21 | Santi |
| D15 | Secretos de la API en user-secrets (desarrollo) y variables de entorno (Docker). `appsettings.json` sin cadena de conexión ni claves | 2026-10-21 | Santi |
| D16 | Contextos con datos de infraestructura personal van en `private/` (ignorada), no en el repo | 2026-10-21 | Santi |
| D17 | Contraseña de Postgres rotada (la anterior quedó en el historial público) | 2026-10-21 | Santi |
| D18 | Open Food Facts (L7). Producto por código: `GET https://world.openfoodfacts.org/api/v3/product/{code}.json` (v3 recomendada, v2 deprecada). Lectura sin auth, pero con User-Agent propio (`AppName/Version (contacto)`). Límites: 15 req/min por IP para producto y 10 para búsqueda (no usar para buscar mientras se tipea); si se pasa, responde 503. Licencia: base ODbL, contenidos DbCL, imágenes CC-BY-SA. **Se puede guardar y reusar** (incluso comercialmente) mencionando la licencia y atribuyendo a Open Food Facts con link a https://openfoodfacts.org o a la página del producto; lo derivado se comparte en las mismas condiciones. Datos sin garantía y no aptos para uso médico. Fuentes: https://openfoodfacts.github.io/openfoodfacts-server/api/ y https://world.openfoodfacts.org/terms-of-use | 2026-10-21 | Lauti |

## Pendientes de decisión

| Pregunta | A quién | Qué bloquea |
|---|---|---|
| ¿Lauti y Santi aceptan el reparto y las tareas de arriba? | Lauti y Santi | Todo el calendario |
| ¿Se ratifica el modelo de datos (`docs/MODELO-DATOS.md`)? | Los 3, reunión de hoy | J2, J7, L1, L4, S7, S9 |
| `contexto.md` y el archivo de n8n siguen en el historial del repo público. ¿Se pasa el repo a privado o se limpia el historial (`git filter-repo` + force push, antes de que Javi y Lauti trabajen encima)? | Santi | Seguridad del repo |
| Hora exacta de entrega del martes 27 | Docente | Plan del lunes |
| Proveedor del LLM y quién consigue la clave (verificar cuota y precios vigentes) | Los 3 | S9 |
| ¿ARGENFOODS tiene descarga (Excel/CSV) o se copia a mano? | Santi | S3, L2 |
| ¿Habrá CI (GitHub Actions)? | Javi | Regla de "CI verde antes de mergear" |
| Cómo se registra la autoría con el docente, ya que el grupo figura de 5 | Javi / docente | Entrega |

## Bloqueos y riesgos

- **Bloqueo hasta mergear `fix/gitignore-utf8`:** el `.gitignore` estaba en UTF-16 y no ignoraba nada; `bin/` y `obj/` quedaron commiteados. Nadie crea ramas hasta que esté mergeado en `develop`.
- Riesgo: tres personas hacen el trabajo planeado para cinco, con un solo margen de integración (lunes 26).
- Riesgo: lo que no esté mergeado el viernes 23 a las 22:00 no entra.
- Riesgo: dependencias cruzadas (modelo de datos de Santi, login de Javi, resumen de Lauti). Mitigación: acordar tablas y endpoints hoy y trabajar con datos simulados mientras tanto.
- Riesgo: el código generado con agentes de IA hay que correrlo, probarlo y entenderlo; se va a tener que defender.
- Riesgo: cuota o caída del LLM o de Open Food Facts. Mitigación: fallback y respaldo propio.
- Si falta tiempo, se recorta en este orden: pulido visual, respaldo OFF, registro de entrenamientos, registro de comidas desde el chat. No se recorta: auth, macros con tests, comidas con resumen, chat, seguridad.

## Deuda y cosas a mejorar

| # | Qué está mal o incompleto | Dónde | Gravedad |
|---|---|---|---|
| T1 | `.gitignore` en UTF-16: no ignoraba `bin/`, `obj/` ni `.env`; compilados en el repo | `.gitignore`, `backend/*/bin`, `backend/*/obj` | Alta → **resuelto en `fix/gitignore-utf8`** al mergear |
| T2 | `.env`, `contexto.md` y un archivo de n8n siguen en el historial del repo **público** | commits `1d80b66` y `dd79c57` | Alta: contraseña rotada (D17); falta decidir repo privado o limpieza de historial |
| T3 | `Program.cs` sigue siendo la plantilla `weatherforecast` (solo se le agregó el registro del `DbContext`); falta Swagger UI, middleware de errores, health check, CORS | `backend/AppNutricion.Api/Program.cs` | Media: es J1 |
| T4 | No hay tests, CI ni Dockerfile (README y `docs/` ya están) | raíz del repo | Media: J11, S4, cada módulo |
| T5 | ~~Postgres publicado en todas las interfaces~~ → `127.0.0.1:5432:5432` | `docker-compose.yml` | Resuelto en `feature/db-modelo-datos` |
| T6 | ~~`.env.example` incompleto~~ → marcadores de cadena de conexión, JWT y LLM | `.env.example` | Resuelto en `feature/db-modelo-datos` |
| T7 | `appsettings.json` sin configuración por entorno | `backend/AppNutricion.Api/` | Baja: J1. La cadena de conexión va en user-secrets (D15), no ahí |

## Cómo levantar y probar el proyecto

Detalle en el `README.md`. Resumen:
1. Docker + SDK de .NET 10 + `dotnet tool install --global dotnet-ef`.
2. `git clone https://github.com/AAAActis/app-nutricion.git` y `git checkout develop`.
3. Copiar `.env.example` como `.env` y completarlo. `docker compose up -d` → `docker compose ps` dice `healthy`.
4. Cadena de conexión en user-secrets (comando en el README).
5. `dotnet ef database update --project backend/AppNutricion.DataAccess --startup-project backend/AppNutricion.Api` → crea las 19 tablas.
6. `dotnet build backend/AppNutricion.slnx` y `dotnet run --project backend/AppNutricion.Api` (`http://localhost:5286`).
7. Tests: `dotnet test backend/AppNutricion.slnx` (xUnit + Moq, en `backend/AppNutricion.Tests`, carpeta por módulo).

## PRÓXIMA SESIÓN · Santi

1. Mergear `fix/gitignore-utf8` y `feature/db-modelo-datos` a `develop` (este último después del review de Lauti y la reunión).
2. Decidir T2: repo privado o limpieza de historial.
3. S3 `foods_seed`: verificar si ARGENFOODS tiene descarga y revisar API, límites y licencia de Open Food Facts. El archivo tiene que traer `Source` + `ExternalId` (clave de idempotencia de Lauti) y macros por 100 g.
4. S6 seeds de demo, S7 rutinas y entrenamientos, S9 asistente. n8n (S13) solo si lo anterior está hecho; antes, respaldar los workflows de La Arch sin tocar la instalación.

## PRÓXIMA SESIÓN · Lauti

1. Revisar el PR `feature/db-modelo-datos` (L13), sobre todo `Foods`, `MealEntries`, `FavoriteFoods`, `Recipes`.
2. `git checkout develop && git pull`, levantar la base y correr `dotnet ef database update`.
3. `git checkout -b feature/alimentos-busqueda` y arrancar L1: Store de `Foods` en DataAccess, Manager en BusinessLogic que arma `NormalizedName`, Controller en Presentation.
4. Si hace falta un campo nuevo en una tabla, va con su propia migración en tu rama.

## PRÓXIMA SESIÓN · Javi

1. Revisar `Users`, `RefreshTokens`, `Profiles`, `BodyMetrics`, `Goals` en `docs/MODELO-DATOS.md`.
2. Seguir J1 sobre `develop`: el registro del `DbContext` ya está en `Program.cs`; respetarlo al reemplazar la plantilla.
3. J2 a J8 con esas tablas. Para Mifflin-St Jeor: peso = último `BodyMetric`, edad desde `BirthDate`.

**Frenar y preguntar si:** el modelo no está ratificado y hay que tocar una tabla de otro, aparece una clave que se iba a subir, n8n tendría que escribir en la base, o hay que elegir proveedor del LLM. No inventar carpetas ni archivos: revisarlos primero en el repo.
