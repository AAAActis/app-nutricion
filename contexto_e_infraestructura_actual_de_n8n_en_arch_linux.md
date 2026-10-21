# Contexto e Infraestructura Actual: Servidor n8n en Arch Linux

Este documento unifica toda la arquitectura, las decisiones técnicas, las limitaciones de hardware descubiertas y el estado exacto en el que quedó configurado **n8n** en el servidor local, así como los lineamientos para el proyecto de tesis (**La Central**) y las automatizaciones personales.

---

## 1. El Hardware y Entorno Base ("La Arch" y "Atlas OS")

El entorno de trabajo se divide en dos máquinas conectadas mediante red local y **Tailscale**:

1. **Máquina de trabajo principal (Cliente):** PC corriendo **Atlas OS** (Windows optimizado). Desde aquí se administra el servidor vía SSH, se abre la interfaz web de n8n en el navegador (mediante túnel SSH o Tailscale) y se gestionan las bases de datos con DBeaver.
2. **Servidor de automatización ("La Arch"):** Una netbook dedicada corriendo **Arch Linux** (usuario `atiz`, hostname `arch`), pensada para quedar encendida 24/7 ejecutando tareas programadas sin consumir recursos de la máquina principal.

### Especificaciones de hardware de "La Arch" y límites operativos
Durante el despliegue se relevaron los recursos reales de la máquina:
* **Procesador:** Intel Celeron N2806 (2 núcleos a 2.0 GHz, arquitectura de gama baja de hace más de 10 años).
* **Memoria RAM:** 3.7 GiB totales (~1.3 GiB en uso base).
* **Almacenamiento:** 29.4 GiB totales, con ~20.5 GiB en uso (70% ocupado; quedan **menos de 9 GiB libres**).

**Decisión crítica de arquitectura por hardware:**
* **Lo que SÍ corre en La Arch:** n8n, su base PostgreSQL interna y todos los **flujos de scraping e ingesta de datos (Tesis - La Central)** o llamadas a APIs externas (HTTP Requests). Es trabajo de red y base de datos liviano para el cual el Celeron N2806 sobra.
* **Lo que NO debe correr en La Arch (Renderizado de Video con FFmpeg):** Se descartó por completo codificar videos dentro de la Arch. Renderizar con `ffmpeg` en un Celeron de 2 núcleos demoraría horas por clip, y los archivos temporales de video saturarían los <9 GiB libres en una tarde. Si el disco llega al 100%, se corrompe o cae el contenedor `postgres:17` de n8n (donde viven los flujos). El renderizado de videos para YouTube/TikTok se debe hacer en la máquina con Atlas OS o mediante APIs externas en la nube.

---

## 2. Cómo quedó montado n8n en La Arch (Estado Actual Funcional)

La instalación vive en el directorio `~/n8n-server` del usuario `atiz` y corre sobre **Docker y Docker Compose**.

### 2.1. Por qué se descartó el `Dockerfile` personalizado
Inicialmente se intentó crear un `Dockerfile` propio agregando `ffmpeg` y `python3` sobre `docker.n8n.io/n8nio/n8n:latest`. Falló tanto con `apk` como con `apt-get` porque las versiones actuales de n8n utilizan **`Docker Hardened Images (Alpine)`** (imágenes endurecidas de seguridad a las cuales les eliminan todos los gestores de paquetes a propósito para evitar vulnerabilidades).

Al descartarse también el renderizado local de video por las limitaciones de CPU/disco de la Arch, **se eliminó el `build: .` y se pasó a utilizar directamente la imagen oficial limpia (`image: docker.n8n.io/n8nio/n8n`)**. Esto garantiza además que actualizar n8n en el futuro no rompa los flujos de la tesis.

### 2.2. Variables de entorno (`.env`)
Ubicado en `~/n8n-server/.env` (con permisos `chmod 600` e ignorado en `.gitignore`). Las claves se generaron en formato **hexadecimal** (`openssl rand -hex`) para evitar caracteres conflictivos de Base64 (`+`, `/`, `=`).

* `N8N_DB_PASSWORD`: Contraseña de la base interna de n8n.
* `N8N_ENCRYPTION_KEY`: Clave maestra que cifra todas las credenciales guardadas dentro de n8n. *(Obligatorio mantener copia en gestor de contraseñas; si se pierde, las credenciales quedan ilegibles).*

### 2.3. Archivo `docker-compose.yml` definitivo en producción
```yaml
services:
  n8n-db:
    image: postgres:17
    container_name: n8n-db
    environment:
      POSTGRES_DB: n8n
      POSTGRES_USER: n8n
      POSTGRES_PASSWORD: ${N8N_DB_PASSWORD}
    volumes:
      - n8n-db-data:/var/lib/postgresql/data
    restart: unless-stopped

  n8n:
    image: docker.n8n.io/n8nio/n8n
    container_name: n8n-app
    depends_on:
      - n8n-db
    environment:
      DB_TYPE: postgresdb
      DB_POSTGRESDB_HOST: n8n-db
      DB_POSTGRESDB_DATABASE: n8n
      DB_POSTGRESDB_USER: n8n
      DB_POSTGRESDB_PASSWORD: ${N8N_DB_PASSWORD}
      N8N_ENCRYPTION_KEY: ${N8N_ENCRYPTION_KEY}
      GENERIC_TIMEZONE: America/Argentina/Cordoba
      TZ: America/Argentina/Cordoba
      N8N_SECURE_COOKIE: "false"
      N8N_RESTRICT_FILE_ACCESS_TO: /data/archivos
      EXECUTIONS_DATA_PRUNE: "true"
      EXECUTIONS_DATA_MAX_AGE: "168"
      EXECUTIONS_DATA_PRUNE_MAX_COUNT: "10000"
      N8N_DEFAULT_BINARY_DATA_MODE: filesystem
    ports:
      - "127.0.0.1:5678:5678"
    volumes:
      - n8n-data:/home/node/.n8n
      - ./archivos:/data/archivos
    restart: unless-stopped

volumes:
  n8n-db-data:
  n8n-data:
```

### 2.4. Decisiones clave dentro del Compose
1. **`postgres:17` para la base interna de n8n:** Evita los cambios en la ruta de volúmenes introducidos en Postgres 18 y asegura estabilidad (el Postgres 18 del proyecto La Central corre por separado y no se toca).
2. **Puerto cerrado al exterior (`127.0.0.1:5678:5678`):** n8n solo escucha dentro de la propia Arch (`localhost`), evitando quedar expuesto sin HTTPS en la red local.
3. **Poda automática de ejecuciones (`EXECUTIONS_DATA_PRUNE`):** Configurado para borrar historiales de más de 168 horas (7 días) y un tope de 10.000 ejecuciones. **Vital** en la Arch para que la base interna no se coma los 9 GiB libres de disco.
4. **Acceso a archivos restringido (`N8N_RESTRICT_FILE_ACCESS_TO: /data/archivos`):** Los flujos solo pueden leer y escribir en la carpeta `./archivos` mapeada desde el host.

---

## 3. Red y Acceso desde Atlas OS hacia La Arch

Dado que n8n escucha únicamente en `127.0.0.1:5678` dentro de la Arch y no requiere dominio público ni Cloudflare para hacer scraping (porque todas las conexiones de scraping y escritura en base son **salientes**), existen dos métodos limpios para entrar a la interfaz gráfica desde Atlas OS:

### Método A: Túnel SSH bajo demanda (El método actual)
En una terminal PowerShell **en Atlas OS** (nunca dentro de una sesión SSH ya abierta en la Arch, para evitar el error `Address already in use`), se ejecuta:
```bash
ssh -L 5678:127.0.0.1:5678 atiz@arch
```
*(O reemplazando `arch` por la IP de Tailscale de la Arch).* Mientras esa ventana quede abierta, se accede desde el navegador de Atlas OS entrando a **`http://localhost:5678`**.

### Método B: Publicación interna permanente con Tailscale Serve (Opcional)
Para no abrir el túnel SSH cada vez, se puede delegar en Tailscale dentro de la Arch:
```bash
tailscale serve --bg --https=443 http://127.0.0.1:5678
```
Esto publica n8n con certificado HTTPS válido únicamente dentro de tu red privada Tailscale (`https://<nombre-arch>.<tailnet>.ts.net`). Si se activa este modo, en el `docker-compose.yml` se pasa `N8N_SECURE_COOKIE: "true"` y se agregan `N8N_HOST`, `N8N_PROTOCOL: https` y `N8N_EXPRESS_TRUST_PROXY: "true"`.

---

## 4. Organización Interna y Separación de Proyectos

La licencia autoalojada gratuita de n8n (**Sustainable Use License**) permite uso interno propio o del negocio sin costo, pero **no incluye la función Enterprise de "Proyectos" separados**. Para convivir en la misma instancia entre el proyecto de tesis (**La Central**) y tus proyectos personales (**Videos/YouTube**) sin mezclar nada, se aplica esta convención estricta:

| Ámbito | Etiqueta (Tag) en Flujos | Prefijo en Credenciales | Base de Datos Destino |
| :--- | :--- | :--- | :--- |
| **Tesis (La Central)** | `LC` | `[LC] Postgres Demo` | `lacentral_demo` (PostgreSQL 18 vía Tailscale) |
| **Personal (Videos)** | `YT` | `[YT] ...` | Base propia aislada o APIs externas (**JAMÁS tocar bases de La Central**) |

---

## 5. Arquitectura de Base de Datos e Ingesta para "La Central" (Tesis)

A diferencia del trabajo previo con Edu en Amílcar (que utiliza **MySQL**), el sistema de **La Central** corre sobre **PostgreSQL 18**.

### 5.1. Reglas de aislamiento en PostgreSQL
* **Entorno de pruebas obligatorio (`lacentral_demo`):** Ningún flujo de n8n en desarrollo apunta a `lacentral_test` (que contiene la copia de 124 proveedores y 2108 clientes reales) ni a producción. Todo escribe en `lacentral_demo`.
* **Conexión vía Tailscale:** Desde dentro del contenedor de n8n, `localhost` es el propio contenedor. Por lo tanto, la credencial `[LC] Postgres Demo` debe apuntar a la **IP de Tailscale de la máquina donde corre el Postgres de La Central** (obtenida con `tailscale ip -4` en dicha máquina). No se debe modificar `pg_hba.conf` ni abrir puertos públicos adicionales porque Postgres ya responde sobre Tailscale.

### 5.2. Esquema de las dos tablas de ingesta (en `lacentral_demo`)
```sql
BEGIN;

CREATE TABLE ingesta_corrida (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    proveedor_id  integer     NOT NULL,
    estado        varchar(16) NOT NULL,
    origen        varchar(32) NOT NULL,
    filas_leidas  integer     NOT NULL DEFAULT 0,
    nota          text        NULL,
    iniciada_en   timestamptz NOT NULL DEFAULT now(),
    terminada_en  timestamptz NULL,
    CONSTRAINT ingesta_corrida_estado_valido
        CHECK (estado IN ('EN_PROCESO','OK','ERROR')),
    CONSTRAINT ingesta_corrida_fin_coherente
        CHECK (terminada_en IS NULL OR terminada_en >= iniciada_en)
);

CREATE TABLE proveedor_articulo (
    id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    proveedor_id        integer        NOT NULL,
    codigo_proveedor    varchar(100)   NOT NULL,
    nombre_original     varchar(255)   NULL,
    nombre_normalizado  varchar(255)   NULL,
    clave_compacta      varchar(255)   NULL,
    marca               varchar(100)   NULL,
    costo_neto          numeric(14,2)  NULL,
    existencia          integer        NULL,
    fecha_dato          timestamptz    NOT NULL DEFAULT now(),
    articulo_id         integer        NULL,
    CONSTRAINT proveedor_articulo_unico
        UNIQUE (proveedor_id, codigo_proveedor),
    CONSTRAINT proveedor_articulo_costo_no_negativo
        CHECK (costo_neto IS NULL OR costo_neto >= 0)
);

CREATE INDEX ix_prov_art_normalizado ON proveedor_articulo (nombre_normalizado);
CREATE INDEX ix_prov_art_compacta    ON proveedor_articulo (clave_compacta);
CREATE INDEX ix_prov_art_sin_vincular ON proveedor_articulo (proveedor_id)
    WHERE articulo_id IS NULL;

COMMIT;
```

### 5.3. Rol de seguridad con privilegios mínimos (`n8n_ingesta`)
n8n no se conecta como superusuario. Utiliza el rol `n8n_ingesta`, el cual:
* Tiene permiso **únicamente** de `SELECT, INSERT, UPDATE` sobre `proveedor_articulo` e `ingesta_corrida`.
* **No tiene permiso `DELETE`** en ninguna tabla (un error en un `WHERE` no puede vaciar catálogos).
* **No tiene acceso** a tablas del negocio (`articulo`, `cliente`, `factura_*`, `usuario`).
* Tiene límites de tiempo (`statement_timeout = '120s'`) y límite de 5 conexiones simultáneas.

---

## 6. Estándar del Flujo de 4 Nodos en n8n (PostgreSQL vs. MySQL)

Para cada proveedor de La Central se replica la arquitectura de 4 pasos probada en Amílcar, pero adaptada a la sintaxis nativa de PostgreSQL:

### Nodo 1 · Crear Corrida (Postgres `Execute Query`)
Aprovecha `RETURNING id` de Postgres para obtener el ID de la corrida limpia sin tener que rastrearla por texto concatenado como en MySQL:
```sql
INSERT INTO ingesta_corrida (proveedor_id, estado, origen, nota)
VALUES ($1, 'EN_PROCESO', $2, $3)
RETURNING id;
```

### Nodo 2 · Normalización en doble columna (Nodo `Code` JS)
Es el corazón del valor de negocio para facilitar la posterior vinculación (HU-CMP-07). Genera tanto `nombre_normalizado` (con espacios para búsqueda por tokens) como `clave_compacta` (sin ningún espacio ni separador, vital para que `GN/100`, `Mouse Genius GN/100` y `mau gen gn 100` coincidan exactamente bajo la subcadena `GN100`):
```javascript
return $input.all().map(item => {
  const raw = item.json;
  const base = String(raw.descripcion ?? '')
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .toUpperCase();

  // Para similitud y tokens: separadores convertidos a espacio.
  const normalizado = base.replace(/[^A-Z0-9]+/g, ' ').replace(/\s+/g, ' ').trim();

  // Para coincidencia exacta por subcadena: sin separadores ni espacios.
  const compacta = base.replace(/[^A-Z0-9]+/g, '');

  return {
    json: {
      proveedor_id: Number(raw.proveedor_id),
      codigo_proveedor: String(raw.codigo ?? '').trim(),
      nombre_original: String(raw.descripcion ?? '').slice(0, 255),
      nombre_normalizado: normalizado.slice(0, 255),
      clave_compacta: compacta.slice(0, 255),
      marca: raw.marca ? String(raw.marca).slice(0, 100) : null,
      costo_neto: raw.precio != null ? Number(raw.precio) : null,
      existencia: raw.stock != null ? Number(raw.stock) : null
    }
  };
});
```

### Nodo 3 · Upsert de Artículos (Postgres `Execute Query`)
Reemplaza el `ON DUPLICATE KEY UPDATE` de MySQL por `ON CONFLICT` y elimina cualquier parche de `SUBSTRING($1, 2)`:
```sql
INSERT INTO proveedor_articulo (
  proveedor_id, codigo_proveedor, nombre_original, nombre_normalizado,
  clave_compacta, marca, costo_neto, existencia, fecha_dato
)
VALUES ($1, $2, $3, $4, $5, $6, $7, $8, now())
ON CONFLICT (proveedor_id, codigo_proveedor)
DO UPDATE SET
  nombre_original    = EXCLUDED.nombre_original,
  nombre_normalizado = EXCLUDED.nombre_normalizado,
  clave_compacta     = EXCLUDED.clave_compacta,
  marca              = EXCLUDED.marca,
  costo_neto         = EXCLUDED.costo_neto,
  existencia         = EXCLUDED.existencia,
  fecha_dato         = now();
```
*Regla de oro del negocio:* **`articulo_id` jamás se incluye en el `INSERT` ni en el `DO UPDATE SET`**. Entra en `NULL` y la vinculación la decide exclusivamente un humano en el sistema.

### Nodo 4 · Cerrar Corrida (Postgres `Execute Query`)
Usa el `id` devuelto por el Nodo 1:
```sql
UPDATE ingesta_corrida
SET estado = 'OK', terminada_en = now(), filas_leidas = $2
WHERE id = $1;
```

---

## 7. Hoja de Ruta Inmediata (Orden de Ejecución)

1. **Verificación del servicio:** Con el túnel SSH activo desde Atlas OS (`ssh -L 5678:127.0.0.1:5678 atiz@arch`), ingresar a `http://localhost:5678` y crear la cuenta de propietario de n8n.
2. **Preparación de `lacentral_demo`:** Ejecutar los scripts SQL de la Sección 5 en el motor PostgreSQL 18 para crear la base `lacentral_demo`, las dos tablas (`ingesta_corrida`, `proveedor_articulo`) y el usuario restringido `n8n_ingesta`.
3. **Relevamiento de proveedores de La Central:** Antes de programar scrapers web complejos, relevar qué proveedores ya entregan lista de precios en Excel/CSV (esos se resuelven por importación directa de archivos en HU-CMP-08 sin necesidad de scraping web).
4. **Prueba en seco con archivo local:** Probar el flujo de 4 nodos en n8n leyendo un archivo CSV/JSON controlado dentro de `/data/archivos` y escribiendo en `lacentral_demo`.
5. **Primer scraper real:** Recién validado el paso anterior, conectar el proveedor con arquitectura web más limpia.