CONTEXTO COMPLETO PARA MI IA (autosuficiente: no tenés acceso a otras conversaciones)

Leyenda: [DECIDIDO] = acordado por el equipo. [A CONFIRMAR] = propuesta o dato pendiente. [NO VERIFICADO] = dato que aún no comprobamos; no lo trates como cierto.
Quién soy: Santi, integrante de un grupo de 5 estudiantes. Respondé siempre en español.

====================================
1. DE QUÉ TRATA EL PROYECTO
====================================
- Equipo [DECIDIDO]: Mateo, Javi, Lauti, Santi (yo) y Franco. Estudiantes de Sistemas, UNC, Córdoba, Argentina.
- Entregable [DECIDIDO]: una API como proyecto de la materia Programación, llamada "Aplicación de Nutrición Inteligente". Se evalúa todo el proyecto, salvo n8n, que es opcional y no se evalúa.
- Fecha [DECIDIDO]: entrega mínima el martes 27/10/2026. Hora exacta [A CONFIRMAR].
- Objetivo de la app: que un usuario pueda registrarse e iniciar sesión, cargar perfil, peso y objetivo, ver sus calorías y macros, buscar y cargar alimentos, ver el resumen del día (comido vs. objetivo), ver rutinas y registrar entrenamientos, y chatear con un asistente IA que responde con sus datos.
- Funcionalidades del MVP: registro y login con consentimiento de datos y borrado de cuenta; perfil, historial de peso, nivel de actividad y objetivo; cálculo de calorías y macros; búsqueda y carga de alimentos con porciones; resumen diario; rutinas precargadas (2 o 3 de ejemplo) y registro de entrenamientos; asistente conversacional; frontend mínimo; Swagger, README y datos de demo.
- Stack [DECIDIDO]: C# con ASP.NET Core Web API, Entity Framework Core con migraciones, PostgreSQL en Docker Compose, auth con JWT y contraseñas con hash (BCrypt), frontend en HTML + JavaScript simple servido desde wwwroot de la misma API, tests con xUnit, Visual Studio Code.
- Asistente IA [DECIDIDO]: la API en C# llama a un LLM con tool calling. Proveedor del LLM y quién consigue la clave [A CONFIRMAR].
- Modelo de datos (resumen): Users, Profiles, BodyMetrics, Goals, Foods (con porción en gramos), MealEntries (macros guardados al momento de la carga), Routines, RoutineDays, WorkoutLogs.
- Endpoints previstos: POST /auth/register, POST /auth/login, DELETE /me, GET/PUT /me/profile, POST/GET /me/metrics, POST/GET /me/goals, GET /me/targets, GET /foods/search?q=, POST/GET/DELETE /me/meals, GET /me/summary?date=, GET /routines, POST/GET /me/workouts, POST /assistant/chat.

====================================
2. QUÉ VAMOS A HACER Y QUÉ ME TOCA A MÍ
====================================
Plan general: en 7 días (del miércoles 21 al martes 27) tener funcionando el recorrido completo. Calendario [DECIDIDO]: Mié 21 reunión para acordar endpoints y tablas, y subir la base del proyecto y Docker; Jue 22 a Sáb 24 cada uno hace su módulo y su pantalla; Dom 25 integración y prueba del recorrido completo; Lun 26 al mediodía se congelan las funcionalidades y se pule (bugs, README, ensayo de demo); Mar 27 entrega.

Reparto del equipo [A CONFIRMAR, es una propuesta]:
- Javi: base del proyecto en C# (solución, conexión a la base, login y registro) + rutinas y entrenamientos.
- Mateo: perfil, peso, objetivo y cálculo de macros.
- Lauti: módulo de alimentos (búsqueda, porciones, respaldo con Open Food Facts).
- Franco: asistente IA en C#. ESTE es el asistente que se evalúa.
- Yo (Santi): Docker Compose con Postgres, comidas del día y resumen diario, carga inicial de alimentos y, como extra, n8n.

Lo mío, OBLIGATORIO:
1. Docker Compose con Postgres, hoy (1 hora aprox.). Todos dependen de esto.
2. Comidas del día y resumen diario (cuando exista la base y el login de Javi).
3. Preparar el conjunto inicial de ~50 a 100 alimentos básicos con el formato acordado (a coordinar con Lauti, que es dueño del módulo de alimentos).

Lo mío, OPCIONAL (solo si lo obligatorio está hecho):
4. Importar marcas y envasados desde Open Food Facts, con n8n como herramienta de apoyo.
5. Bot de n8n dentro de la app, como alternativa al asistente de Franco.

Prioridad: 1 → 2 y 3 → 4 → 5.
Dependencias: todos necesitan el compose y la base de Javi. Mis comidas dependen de la tabla de alimentos y del login (el userId sale del token). Lauti necesita el formato del conjunto de alimentos para su búsqueda. Franco necesita mi resumen diario y los objetivos de Mateo; mientras tanto trabaja con respuestas simuladas.
Aclaración: n8n es un extra. Si no llego, el proyecto igual se entrega completo con el asistente de Franco.

====================================
3. PARA QUÉ HACEMOS DOCKER COMPOSE
====================================
Problema que resuelve: que los 5 levantemos la misma base PostgreSQL con un solo comando, sin instalar Postgres a mano ni tener versiones distintas. Todos tenemos Docker disponible [DECIDIDO].

Servicios:
- db: PostgreSQL con un volumen para que los datos persistan entre reinicios, variables de entorno tomadas de un archivo .env, y un healthcheck. La versión de la imagen la fijamos explícitamente [A CONFIRMAR cuál].
- Opcional: una herramienta web para ver la base (Adminer o pgAdmin).
- La API en C# NO va en contenedor por ahora: cada uno la corre desde su máquina (VS Code o dotnet run) y se conecta a Postgres por el puerto publicado [A CONFIRMAR con Javi].

Conexión API ↔️ PostgreSQL: con la API en la máquina, el host es localhost y el puerto es el que publiquemos (el 5432 por defecto). La cadena de conexión tiene la forma Host=localhost;Port=<puerto>;Database=<db>;Username=<usuario>;Password=<clave>, con los valores reales fuera del repo (user-secrets o variables de entorno). Si algún día la API corre en contenedor dentro del mismo compose, el host pasa a ser el nombre del servicio (por ejemplo db).

Archivos a entregar al repositorio:
- docker-compose.yml
- .env.example con nombres de variables y valores de ejemplo, SIN claves reales
- .env agregado al .gitignore (el .env real nunca se sube)
- Una sección en el README con los comandos: levantar (docker compose up -d), bajar (docker compose down), ver logs, y cómo reiniciar la base desde cero (borrando el volumen)

Cómo lo usan mis compañeros:
1. Tener Docker instalado y funcionando.
2. Copiar .env.example como .env y completar los valores.
3. Correr docker compose up -d.
4. Aplicar las migraciones de EF Core (esa parte es de Javi).
5. Configurar la cadena de conexión localmente en su API.
Lo que necesita cada uno: Docker andando, que el puerto elegido esté libre (si alguien tiene un Postgres local instalado puede haber conflicto con el 5432) y su .env propio.
No conozco la estructura del repositorio ni si ya existe la rama develop [A CONFIRMAR]. No inventes nombres de carpetas ni de archivos que no te haya confirmado.

====================================
4. MI INSTALACIÓN ACTUAL DE n8n
====================================
Ya tengo n8n funcionando localmente desde antes de este proyecto. NO sé cómo está instalado (Docker, npm o app de escritorio), ni cómo guarda sus datos, ni qué versión es, ni qué workflows importantes tiene. No supongas nada de eso.

Pedido para vos, en este orden:
1. Antes de proponer nada, guiame para REVISAR cómo lo tengo instalado, con comandos de solo lectura: si corre en Docker o no, qué imagen y versión, qué puerto usa, dónde guarda sus datos (volumen o carpeta), qué variables de entorno tiene, y si tiene credenciales y workflows que no quiero perder.
2. Explicame las alternativas con sus pros y contras ANTES de recomendar una. Alternativas a considerar:
   A. Usar mi misma instancia y crear workflows y credenciales nuevos para el proyecto.
   B. Crear una instancia separada solo para el proyecto (por ejemplo, un servicio aparte con su propio volumen).
   C. Una combinación.
   Puntos a comparar: riesgo de romper lo que ya tengo, conflicto de puertos, credenciales mezcladas, facilidad de respaldo y de exportar workflows, y si conviene integrarlo al compose del proyecto o mantenerlo aparte (solo yo uso n8n, y es opcional).
3. No borres, no reemplaces ni reinstales mi instalación actual. Antes de tocar algo, indicame cómo exportar o respaldar mis workflows y credenciales.
4. Explicame cómo se conectaría la opción elegida con los servicios del proyecto (ver sección 6).

====================================
5. CARGA DE ALIMENTOS
====================================
Plan [DECIDIDO]: ~50 a 100 alimentos básicos de ARGENFOODS (Universidad Nacional de Luján) + productos envasados y marcas de Open Food Facts Argentina.

Estado de las fuentes:
- ARGENFOODS (argenfood.unlu.edu.ar): publica una tabla de composición de alimentos argentinos. Si ofrece descarga en Excel o CSV [NO VERIFICADO]. Si solo está en la web, los 50 a 100 básicos se copian a mano a una planilla.
- Open Food Facts Argentina (ar.openfoodfacts.org): base abierta con productos argentinos. En sus listados aparecen más de 12.000 productos, pero con nutrición incompleta en muchos [cifra aproximada, NO VERIFICADO]. Detalles de su API, límites de uso, formato de exportación, licencia y atribución [NO VERIFICADO: revisarlos en su documentación].
- Complemento posible: el atlas Smart24Food (PLOS ONE, DOI 10.1371/journal.pone.0359331), con alimentos argentinos y fotos de porciones. Dónde se descarga [NO VERIFICADO]. No depender de él.
- Fitia queda DESCARTADA [DECIDIDO]: es una base privada y no se hace scraping de ella.

Datos que necesitamos por alimento: nombre, marca (opcional), fuente (ARGENFOODS u OFF), id externo (por ejemplo el código de barras en OFF), kcal, proteínas, carbohidratos y grasas por 100 g, y una porción de referencia (nombre y gramos; por ejemplo "1 unidad"). Los gramos de cada porción hay que respaldarlos con una fuente, no estimarlos a ojo.

Cómo normalizar:
- Todo a "por 100 g". Si un producto viene por 100 ml o por porción, convertirlo o marcarlo y revisarlo.
- Si la energía viene solo en kJ, convertir a kcal (1 kcal = 4,184 kJ).
- Descartar productos sin energía o a los que les falte algún macro.
- Descartar valores imposibles (por ejemplo, proteínas + carbohidratos + grasas mayores a 100 g cada 100 g, o kcal absurdas).
- Decimales con punto, nombres sin espacios sobrantes y con mayúsculas y acentos consistentes, sin duplicados por (fuente, id externo).

CONTRADICCIÓN RESUELTA: la decisión es que la API es el ÚNICO escritor de la base. Entonces n8n NO carga directo en PostgreSQL. Flujo ajustado:
1. El equipo define la lista de básicos y el formato del archivo (esquema fijo).
2. Se extraen los datos: ARGENFOODS a mano o por descarga, y Open Food Facts con n8n (opcional) o con un script, usando su API oficial.
3. Se normaliza y se genera un ARCHIVO (JSON o CSV) con el conjunto filtrado, por ejemplo foods_seed.
4. Se revisa el archivo entre varios (una persona más lo mira) y se sube al repositorio. Es un conjunto filtrado y chico, no un volcado completo.
5. La API carga ese archivo en la tabla Foods al arrancar o con un comando de seed, de forma idempotente (si ya existe el par fuente + id externo, no lo duplica). Esa parte es del módulo de alimentos de Lauti y se coordina con él [A CONFIRMAR].
Alternativa, NO para el MVP: que n8n llame a un endpoint interno de importación de la API, protegido con token de servicio. Es más trabajo y más superficie de ataque.
Así n8n queda como herramienta de apoyo para generar el archivo y nunca toca la base.

====================================
6. BOT OPCIONAL CON n8n
====================================
El bot es el chat de nuestra propia app, no Telegram ni WhatsApp.
Recorrido: frontend (chat) → API (POST /assistant/chat) → webhook de n8n → LLM → respuesta a n8n → API → frontend.

Contexto que la API le manda a n8n: objetivos y macros del usuario, totales del día, rutina activa y los últimos mensajes. NO le manda email, nombre ni identificadores.
Por qué n8n no accede a la base: la API es el único escritor, el userId sale siempre del token (nunca del LLM ni de n8n), y así hay un solo lugar que valida y aplica permisos. Si el asistente propone registrar una comida, el usuario confirma y la API la guarda.
Las calorías y macros salen siempre de la tabla de alimentos, nunca del LLM.
La clave del LLM va en las credenciales de n8n o en una variable de entorno, nunca en el repo.
Propuesta [A CONFIRMAR con Franco]: el asistente de C# y el de n8n son dos implementaciones de la misma interfaz detrás de POST /assistant/chat, así se puede cambiar de uno a otro por configuración. El de Franco es el principal y el que se evalúa.

Direcciones según dónde corre cada cosa (los nombres "n8n" y "api" son ejemplos; usar los nombres reales de los servicios):
- API y n8n ambos en la máquina (n8n instalado con npm o app de escritorio): API → n8n en http://localhost:5678/webhook/<ruta>. n8n → API en http://localhost:<puerto de la API>.
- API en la máquina y n8n en contenedor (con el puerto publicado): API → n8n igual, http://localhost:5678/webhook/<ruta>. n8n → API en http://host.docker.internal:<puerto de la API> (funciona en Docker Desktop; en Linux hay que configurarlo aparte).
- API y n8n ambos en contenedores de la misma red de compose: API → n8n en http://n8n:5678/webhook/<ruta>. n8n → API en http://api:<puerto>. La base se alcanza como db.
- La URL /webhook-test/ solo funciona mientras el editor de n8n está escuchando. La URL /webhook/ funciona con el workflow activado.
Cuándo alcanza con la red local: siempre que quien llama a n8n sea nuestra API, en la misma máquina o red de Docker. Que n8n salga a internet hacia el proveedor del LLM solo requiere conexión saliente, sin dirección pública.
Cuándo hace falta dirección pública: solo si un servicio externo tiene que llamar a n8n (un bot de Telegram o de WhatsApp). Telegram exige un webhook HTTPS público, que se resuelve con un túnel (por ejemplo ngrok) y configurando la variable WEBHOOK_URL de n8n. No hace falta dominio propio. Esto NO es parte del plan ni del MVP.

====================================
7. REGLAS Y PRÓXIMOS PASOS
====================================
Seguridad [DECIDIDO]:
- Contraseñas con hash, nunca en texto plano ni en logs.
- Cada consulta filtra por el userId del token; un usuario no ve datos de otro.
- Validación de entradas en todos los endpoints.
- Consentimiento al registrarse y borrado de cuenta (Ley 25.326).
- Al LLM no se le envían email, nombre ni identificadores.
- El userId de las herramientas del asistente sale del token, nunca del LLM.
- Rate limiting en login y en el chat.
- Guardrails del asistente: no diagnostica, no recomienda dietas extremas, piso calórico, sin déficit a menores de 18, aviso de que no es consejo médico.
Secretos [DECIDIDO]: solo en variables de entorno o .env, con .env.example en el repo. Si una clave se sube por error, se revoca y se rota el mismo día. Aplica a la clave del LLM, la clave JWT y la cadena de conexión.
Ramas y PR [DECIDIDO]: ramas desde develop con nombre feature/<modulo>-<tema>, PR chicos, sin merge sin revisión de otra persona. CI verde antes de mergear [A CONFIRMAR si habrá CI].
Límites del MVP: quedan fuera subir y parsear PDF de rutinas, alimentos personalizados, streaming del chat, sugerencias automáticas diarias, historial persistente de conversaciones y bots de Telegram o WhatsApp.
Si falta tiempo, se recorta primero el pulido visual, después el respaldo de Open Food Facts y después los entrenamientos. Nunca se recortan: login, macros, comidas con resumen, el chat de Franco y la seguridad.
Cosas que todavía hay que confirmar: formato de descarga de ARGENFOODS, fuentes y límites de Open Food Facts, versión de Postgres, existencia del repo y de develop, si habrá CI, reparto final de roles, proveedor del LLM, hora de entrega y cómo tengo instalado mi n8n.

CÓMO QUIERO QUE ME AYUDES
Guiame en español, de a UN bloque por vez, con explicaciones claras de qué hacemos y por qué, y esperá mi confirmación antes de pasar al siguiente. No me des todo junto. No inventes datos del repositorio ni de mi instalación: preguntame o pedime que ejecute comandos de revisión. Si ves un error o un riesgo en lo que hago, avisame. No toques ni borres mi instalación actual de n8n.

PRIMERA TAREA
Armar el docker-compose.yml con un único servicio, PostgreSQL, más el .env.example y la entrada de .env en el .gitignore. Guiame paso a paso: qué va en cada línea, cómo levantarlo, cómo comprobar que funciona y cómo conectarme desde un cliente. Todavía no incluyas n8n ni la API en el compose. Cuando esté probado, seguimos con el README del compose y recién después con la revisión de mi instalación de n8n.