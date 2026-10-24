# Auditoría de seguridad (J13)

Revisión del 2026-10-23 sobre `develop` (`d1d43f8`, con los PR #1 a #24). Cada punto del checklist dice **qué se controla**, **dónde está** (archivo y línea) y **qué test lo prueba**. Los tests están en `backend/AppNutricion.Tests` y corren con `dotnet test backend/AppNutricion.slnx` (también en CI, `.github/workflows/ci.yml`). Donde no hay test automático, se dice.

Rutas de código relativas a `backend/`. Estado: ✅ cumple · ⚠️ cumple con salvedades · ❌ pendiente.

## 1. Autenticación

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| A1 | Contraseñas con hash (`PasswordHasher<User>` de ASP.NET Core Identity: PBKDF2 con sal), nunca en texto plano | `AppNutricion.BusinessLogic/Managers/AuthManager.cs:41` y `:58` | `Auth/AuthManagerLoginTests.cs` → `LoginAsync_PasswordIncorrecta_DevuelveNullYVerificaUnaVez` | ✅ |
| A2 | Contraseña de 8 a 128 caracteres (el tope evita hashear entradas enormes) | `AuthManager.cs:13-14`, `:25` | `Auth/AuthManagerLoginTests.cs` → `LoginAsync_PasswordDeMasDe128_DevuelveNullSinLlamarAlHasher` | ✅ |
| A3 | El login no revela si el email existe: mismo 401 y una verificación de hash también con email inexistente (hash ficticio) | `AuthManager.cs:58` | `Api/AuthEndpointsTests.cs` → `Login_PasswordIncorrectaOEmailInexistente_401ConElMismoCuerpo`; `Auth/AuthManagerLoginTests.cs` → `LoginAsync_EmailInexistente_DevuelveNullYVerificaIgualUnaVez` | ✅ |
| A4 | JWT validado: firma (HMAC-SHA256 con `Jwt:Key`), emisor, audiencia y vencimiento | `AppNutricion.Api/Program.cs:26-35` | `Api/AuthEndpointsTests.cs` → `Me_TokenConClaveEmisorOAudienciaDistinta_401`; `Api/ComidasConTokenRealTests.cs` → `GetMeals_TokenFirmadoConOtraClave_401SinLlegarAlManager` | ✅ |
| A5 | Sin `Jwt:Key` de 32 bytes o más, la API no arranca (y el mensaje no muestra la clave) | `Program.cs:20-21` | `Api/JwtKeyArranqueTests.cs` → `Arranque_ConJwtKeyFaltanteOCorta_Falla` | ✅ |
| A6 | El token de una cuenta borrada deja de valer (401) | `Program.cs:40` (`OnTokenValidated`) | `Api/AuthEndpointsTests.cs` → `Me_TokenDeUsuarioInexistente_401` | ✅ |
| A7 | El token no lleva el email (solo `sub`, emisor, audiencia, vencimiento) | `AppNutricion.BusinessLogic/Services/JwtAccessTokenService.cs` | `Auth/JwtAccessTokenServiceTests.cs` → `CreateFor_NoIncluyeElEmailEnElToken` | ✅ |
| A8 | Token del frontend en `sessionStorage` (se borra al cerrar la pestaña), nunca en `localStorage` ni en cookies | `AppNutricion.Api/wwwroot/js/auth.js:1-7` | Sin test automático (no hay runner de JS); revisión manual | ⚠️ |
| A9 | Refresh token y logout del lado del servidor | — | — | ❌ J3 pendiente: el access token vale hasta que vence (`Jwt:ExpirationMinutes`, 60) |

## 2. Autorización y aislamiento entre usuarios

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| B1 | Todos los controllers con datos de usuario llevan `[Authorize]` (sin token → 401). Sin `[Authorize]`: `AuthController` (registro y login) y la búsqueda de alimentos compartidos de `FoodsController` | `AppNutricion.Presentation/Controllers/*.cs` | `Api/AuthEndpointsTests.cs` → `Me_SinToken_401`; `Foods/FoodsControllerTests.cs` → `EndpointsNuevos_SinHeader_Devuelven401`; `Api/PerfilEndpointsTests.cs` → `GetProfile_SinToken_401` | ⚠️ No hay un test que recorra todos los controllers |
| B2 | El `userId` sale del claim `sub` del token (`ICurrentUser.RequireUserId()`), nunca del body ni de la query | `AppNutricion.Presentation/Auth/CurrentUser.cs:18` | `Meals/MealsControllerTests.cs` → `Post_UsaElUserIdDelClaimAunqueElBodyTraigaOtro_Y_Devuelve201` | ✅ |
| B3 | Toda consulta filtra por `UserId`: un recurso de otro usuario responde como inexistente (null / 404), sin revelar que existe | Stores en `AppNutricion.DataAccess/Stores/` | `Meals/MealEntryStoreTests.cs` → `GetById_DeOtroUsuario_DevuelveNull`; `Meals/MealsControllerTests.cs` → `Delete_DeOtroUsuarioOInexistente_Devuelve404ProblemDetails`; `Api/PerfilEndpointsTests.cs` → `GetProfile_DeOtroUsuario_404`; `Recetas/RecetasDePuntaAPuntaTests.cs` → `ElCustomYLaRecetaDeOtro_NoSeVenNiSeUsan`; `Asistente/AssistantManagerTests.cs` → `ConversacionDeOtroUsuario_NullEnTodo` | ✅ |
| B4 | Rutinas: las precargadas son de todos, las propias solo de su dueño; una precargada no se borra | `AppNutricion.DataAccess/Stores/RoutineStore.cs` | `Training/EntrenamientoStoresTests.cs` → `Rutinas_ListaVisible_PrecargadasYPropias_NuncaLasDeOtro`, `Rutinas_GetVisible_PrecargadaSi_DeOtroNo`; `Training/RoutineManagerTests.cs` → `Delete_Precargada_ConflictException` | ✅ |

## 3. Validación de entradas y errores

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| C1 | Dato inválido → 400 `ValidationProblem` con el campo que falla (los managers tiran `ArgumentException` con el nombre del campo) | `AppNutricion.Presentation/Controllers/ControllerBaseExtensions.cs` | `Meals/MealsControllerTests.cs` → `Post_ManagerLanzaArgumentException_Devuelve400ValidationProblemConElCampo` | ✅ |
| C2 | Topes en listas y rangos (recientes hasta 30, rangos de hasta 366 días, mensaje del chat de hasta 1000 caracteres) | Managers en `AppNutricion.BusinessLogic/Managers/` | `Foods/FavoritosYRecientesManagerTests.cs` → `GetRecent_LimiteDefault10YMaximo30`; `Meals/MealsControllerTests.cs` → `Export_RangoInvalido_Devuelve400ConElCampo`; `Asistente/AssistantControllerTests.cs` → `Enviar_TextoInvalido_400Content` | ✅ |
| C3 | Una excepción no manejada sale como ProblemDetails 500 genérico: sin mensaje ni stack trace | `Program.cs:77` (`UseExceptionHandler`) | `Api/ErroresTests.cs` → `ExcepcionNoManejada_DevuelveProblemDetails500SinDetallesInternos` | ✅ |
| C4 | Inyección SQL: todo por EF Core (consultas parametrizadas). La búsqueda escapa `%`, `_` y `\` del término antes del `ILIKE` | `AppNutricion.DataAccess/Stores/FoodStore.cs:14-16` | Sin test automático del escape (ILIKE no corre en SQLite); probado contra Postgres | ⚠️ |
| C5 | Inyección de fórmulas en el CSV exportado: un texto que empieza con `=`, `+`, `-`, `@` o tab se neutraliza con `'` | `AppNutricion.BusinessLogic/Managers/MealEntryManager.cs:174` | `Meals/ExportarCsvTests.cs` → `Alimento_QueEmpiezaComoFormula_QuedaNeutralizado`, `Alimento_EscapadoRfc4180` | ✅ |
| C6 | Respuestas de Open Food Facts validadas: un 5xx o un JSON inválido no se guarda como producto | `AppNutricion.BusinessLogic/Services/OpenFoodFactsService.cs` | `Foods/OpenFoodFactsTests.cs` → `OpenFoodFactsCon5xxYCuerpoJsonValido_NoSeAceptaComoProducto`, `OpenFoodFactsCon200YJsonInvalido_NoEncontradoSinExcepcionYConWarning` | ✅ |

## 4. Abuso y disponibilidad

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| D1 | Rate limiting en login y registro: 5 por minuto por IP (compartidos), 429 con `Retry-After` | `AppNutricion.Presentation/RateLimiting/` (política `Auth`), `Program.cs:106` | `Api/RateLimitingTests.cs` → `Auth_SextoIntentoDesdeLaMismaIp_429ConRetryAfter`, `Auth_LoginYRegistroCompartenLaVentanaDeLaIp` | ✅ |
| D2 | Rate limiting del chat: 20 mensajes por minuto por usuario | `RateLimiting/` (política `AssistantChat`) | `Asistente/AssistantControllerTests.cs` → `LaVuelta21DelMinuto_429_PorUsuario` | ✅ |
| D3 | Detrás de un proxy todas las requests llegan con la misma IP | — | — | ⚠️ Sin `ForwardedHeaders` (no hay proxy; queda para S4) |

## 5. Secretos y logs

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| E1 | Cadena de conexión, `Jwt:Key` y `Llm:ApiKey` en user-secrets (desarrollo) o variables de entorno (Docker); `appsettings.json` sin claves; `.env` en `.gitignore` | `AppNutricion.Api/appsettings.json`, `.gitignore`, `.env.example` | `Api/JwtKeyArranqueTests.cs` (sin clave no arranca). Que no haya secretos en el repo se revisa a mano en cada PR | ⚠️ Ver T2 en `docs/ESTADO.md`: un `.env` viejo quedó en el historial (contraseña rotada, D17) |
| E2 | Nunca la contraseña en logs: los Input de auth y de borrado de cuenta ocultan la contraseña en `ToString()` (MVC los loguea en Trace) | `AppNutricion.Presentation/Input/LoginInput.cs:6`, `RegisterInput.cs:7`, `DeleteAccountInput.cs:6` | `Api/LogsSinSecretosTests.cs` → `FlujoDeAuth_NingunLogTieneContrase…`; `Cuenta/AccountManagerDeleteTests.cs` → `DeleteAccountInput_ToString_NoMuestraLaContrase…` | ✅ |
| E3 | La clave del LLM va solo en el header `Authorization`; los logs del adaptador tienen el status, el tiempo y el tipo de excepción, nunca la clave ni el cuerpo | `AppNutricion.BusinessLogic/Services/OpenAiCompatibleChatModel.cs:34`, `:41`, `:50` | `Asistente/OpenAiCompatibleChatModelTests.cs` → `RespuestaFinal_SeMapeaATextoSinLlamadas_YSeLogueaElStatusSinElBody` (no hay un test que busque la clave en los logs) | ⚠️ |

## 6. Frontend y navegador

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| F1 | CSP solo `'self'` en el HTML (sin scripts inline ni recursos externos) | `AppNutricion.Api/FrontendSecurity.cs:7`, `Program.cs:98` | `Api/FrontendTests.cs` → `Raiz_ConContentSecurityPolicySoloMismoOrigen`, `Html_SinScriptsNiEstilosExternos` | ✅ |
| F2 | XSS: los datos se muestran como texto (`dom.js`), ningún JS usa `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write` ni `eval` | `AppNutricion.Api/wwwroot/js/dom.js` | `Api/FrontendTests.cs` → `Js_NuncaUsaInnerHtmlNiParecidos` | ✅ |
| F3 | CORS cerrado por defecto (solo mismo origen; otros orígenes se agregan por config) | `Program.cs:69-71` | `Api/CorsTests.cs` → `OrigenNoConfigurado_NoRecibeHeadersCors`, `OrigenConfigurado_RecibeHeadersCors` | ✅ |
| F4 | Swagger y el documento OpenAPI solo en Development | `Program.cs:81-85` | `Api/SwaggerTests.cs` → `FueraDeDesarrollo_SwaggerYOpenApiNoSeExponen` | ✅ |
| F5 | Redirección a HTTPS | `Program.cs:87` (`UseHttpsRedirection`) | Sin test automático | ⚠️ En la demo local se usa `http://localhost`; sin HSTS |

## 7. Datos personales (Ley 25.326)

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| G1 | Registro solo con consentimiento; se guarda la versión aceptada y la fecha en UTC | `AuthManager.cs:27-39`, `AppNutricion.BusinessLogic/Constants/ConsentVersions.cs` | `Auth/AuthManagerRegisterTests.cs` → `RegisterAsync_SinConsentimiento_LanzaArgumentExceptionDeConsentAccepted`, `RegisterAsync_GuardaConsentimientoVersionadoYFechasDelRelojEnUtc` | ✅ |
| G2 | El texto del consentimiento tiene lo que pide la ley (responsable con contacto, finalidad, derechos, leyenda de la AAIP, aviso de Gemini) y la misma versión que la API | `AppNutricion.Api/wwwroot/js/consent.js` | `Api/FrontendTests.cs` → `Consentimiento_IncluyeLoQuePideLaLey`, `Consentimiento_SinNadaPorCompletar`, `Consentimiento_LaVersionDelFrontendEsLaDeLaApi` | ✅ |
| G3 | Supresión: `DELETE /api/me` con la contraseña actual borra la cuenta y todos sus datos, sin tocar los de otro | `AppNutricion.BusinessLogic/Managers/AccountManager.cs`, `AppNutricion.DataAccess/Stores/UserAccountStore.cs` | `Api/BorradoDeCuentaEndpointsTests.cs`; `Asistente/AssistantStoreTests.cs` → `BorrarCuenta_BorraConversacionesMensajesYSugerencias_YNoTocaLasDeOtro` | ✅ |
| G4 | Acceso: `GET /api/me/export` con todos los datos del usuario, sin el hash ni los refresh tokens | `AppNutricion.DataAccess/Stores/ExportStore.cs`, `AppNutricion.Presentation/Output/ExportOutput.cs` | `Api/ExportEndpointsTests.cs` → `Export_NuncaIncluyeElHashNiRefreshTokens` | ✅ |

## 8. Asistente (LLM)

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| H1 | El LLM nunca escribe en la base (D5): solo propone ingestas; la comida se crea cuando el usuario acepta, con macros de Foods (D6) | `AppNutricion.BusinessLogic/Managers/AssistantManager.cs` | `Asistente/AssistantManagerTests.cs` → `ComiArroz_CreaUnaSugerenciaPendiente_YNingunaComida` | ✅ |
| H2 | Al LLM no van userId, email ni nombre (solo el contexto permitido, D23) | `AssistantManager.cs` (`SystemPromptAsync`) | `Asistente/AssistantManagerTests.cs` → `ElModeloNoRecibeUserIdNiEmail_YSiElContextoPermitido` | ✅ |
| H3 | Los argumentos de las herramientas se validan como si vinieran de un cliente; el userId lo pone la API | `AppNutricion.BusinessLogic/Services/AssistantTools.cs` | `Asistente/AssistantManagerTests.cs` → `ElUserIdQueMandaElLlm_SeIgnora`; `Asistente/RutinasDelAsistenteTests.cs` → `ConsultarRutinas_ElUserIdEsElDeLaApi_NoElDeLosArgumentos` | ✅ |
| H4 | Si el LLM falla o tarda más de `Llm:TimeoutSeconds`, responde con los totales del día (`degraded`), nunca un 500 | `AssistantManager.cs` | `Asistente/AssistantManagerTests.cs` → `ElModeloTardaMasQueElTimeout_DegradedYSeCancelaElToken` | ✅ |
| H5 | Privacidad del proveedor: en el plan gratis de la Gemini API, Google puede usar lo que se manda para mejorar sus productos | Consentimiento (`consent.js`, aviso de Gemini) | `Api/FrontendTests.cs` → `Consentimiento_AvisoDeGeminiAntesDeLosDerechos` | ⚠️ Avisado al usuario; el plan pago lo evita |

## 9. Infraestructura

| # | Control | Dónde | Test | Estado |
|---|---|---|---|---|
| I1 | Postgres publicado solo en `127.0.0.1` | `docker-compose.yml:9` | Sin test automático | ✅ |
| I2 | CI: build y tests en cada PR, acciones fijadas por SHA, permisos de solo lectura | `.github/workflows/ci.yml` | El propio workflow | ✅ |
| I3 | Repo público con un `.env` y un contexto viejos en el historial | — | — | ❌ T2 en `docs/ESTADO.md`: decidir repo privado o limpieza del historial |

## Pendientes que salen de esta auditoría

1. **A9 / J3:** refresh token y logout (hoy el token vale 60 minutos aunque el usuario "salga").
2. **B1:** un test que recorra todos los controllers y falle si alguno con datos de usuario no tiene `[Authorize]`.
3. **I3 / T2:** historial del repo público.
4. **D3, F5:** `ForwardedHeaders` y HSTS cuando haya un despliegue detrás de un proxy (S4).
5. **C4:** probar el escape del `ILIKE` contra Postgres en un test de integración.
