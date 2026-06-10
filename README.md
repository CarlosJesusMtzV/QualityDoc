# QualityDoc

**Sistema Integral de Gestión de Documentos de Calidad** — plataforma **multi-empresa (SaaS)** construida como una **arquitectura políglota de microservicios**: 3 lenguajes de backend (.NET, Node.js, PHP) y 3 motores de base de datos (SQL Server, PostgreSQL, MongoDB), orquestados con **Docker** y con **Nginx como puerta de enlace**.

Cada módulo es independiente y dueño de su propia base de datos; se integran entre sí por **API REST** y por **base de datos compartida** (auditoría).

---

## 📄 Documentación

| Documento | Archivo |
|---|---|
| Documentación técnica y funcional completa | `docs/QualityDoc-Documentacion.docx` · `docs/QualityDoc-Documentacion.pdf` |
| Guion de defensa (qué decir + preguntas frecuentes) | `docs/QualityDoc-Guion-Defensa.docx` · `docs/QualityDoc-Guion-Defensa.pdf` |
| Presentación | `docs/QualityDoc-Presentacion.pptx` |
| Diagramas UML (casos de uso, clases, secuencia, despliegue) | `docs/uml/` |
| Diseño y reglas de desarrollo | `docs/DISENO.md` · `AGENTS.md` |

---

## 🧩 Arquitectura (7 contenedores)

Entrada única por **Nginx** en **http://localhost:8080**.

| Servicio | Tecnología | Puerto (host→cont.) | Función |
|---|---|---|---|
| **qd_nginx** | Nginx 1.27 | **8080 → 80** | Gateway (`/`→.NET, `/portal`→PHP, `/search`→Node, `/files`→archivos) |
| **qd_web** | ASP.NET Core MVC (.NET 10) | interno | Administración, auth/RBAC, flujo documental, dashboard, reportes |
| **qd_node** | Node.js 20 / TypeScript | 3000 | Dueño de MongoDB: indexa metadatos (HTTP 202) y resuelve la búsqueda |
| **qd_php** | PHP 8.3 + Apache | interno | Portal de consulta (solo lectura) y reportes |
| **qd_sqlserver** | SQL Server 2022 | 1450 → 1433 | Núcleo: empresas, usuarios, áreas, documentos, versiones |
| **qd_postgres** | PostgreSQL 16 | 5440 → 5432 | Auditoría (logs y accesos) |
| **qd_mongo** | MongoDB 7 | 27018 → 27017 | Metadatos en JSON para la búsqueda |

- **Red interna `qd_net`**: los contenedores se comunican por su nombre de servicio.
- **Volúmenes** (`sql_data`, `pg_data`, `mongo_data`): persistencia de las 3 bases.
- **Carpeta compartida `./storage`**: archivos físicos (la escribe .NET, la lee Node, la sirve Nginx).

### Orquestación dividida en 3 archivos

El `docker-compose.yml` principal une 3 archivos por afinidad mediante `include`:

| Archivo | Servicios |
|---|---|
| `docker-compose.app.yml` | web (.NET) + sqlserver + postgres |
| `docker-compose.search.yml` | node-search + mongo |
| `docker-compose.portal.yml` | php-portal + nginx |

---

## ⚙️ Funcionalidad

- **Multi-empresa (multi-tenant)** con aislamiento por empresa y por **área**.
- **6 roles:** SuperAdmin, Admin, **Autorizador**, Revisor, Creador, Lector.
- **Flujo documental:** el **Creador** redacta → el **Revisor** lo manda a revisión → el **Autorizador** lo aprueba o rechaza. Estados: Borrador / En revisión / Aprobado (Vigente) / Rechazado / Obsoleto.
- **Versionado SemVer:** `v1.0.0` al crear; aprobar una edición sube de mayor (`v2.0.0`→`v3.0.0`); rechazar sube de menor (`v2.0.0`→`v2.1.0`). **Nunca se sobrescribe un archivo.**
- **Búsqueda por metadatos** (texto, etiquetas, código, autor) con fragmentos resaltados.
- **Vista previa** del documento + su **estructura JSON** y el **contenido extraído** legible.
- **Validación de contenido** al subir: se exige texto (no se permite subir solo una imagen).
- **Auditoría** (Admin/SuperAdmin) y **reportes en PDF** (inventario, cumplimiento, KPIs, historial).
- **Portal público (PHP)** de consulta y reportes de cumplimiento.

---

## ✅ Requisitos

- **Docker Desktop** con Compose **v2.20+** (para `include`). Verifica con `docker compose version`.
- *(Opcional para desarrollo)* Visual Studio 2022 + **.NET 10 SDK**.

---

## 🚀 Puesta en marcha con Docker (recomendado)

```bash
# 1) Asegúrate de tener el archivo .env con las contraseñas/secretos (ver más abajo).
# 2) Levanta todo (los 3 archivos compose se combinan solos):
docker compose up -d --build

# 3) Verifica que los 7 contenedores estén arriba:
docker compose ps
```

Abre **http://localhost:8080/** e inicia sesión.

> Al arrancar, el módulo .NET **aplica las migraciones de EF Core** (crea las tablas) y **siembra los datos demo** (3 empresas, sus usuarios por área y ~50 documentos reales).

### Variables del `.env`

```dotenv
ASPNETCORE_ENVIRONMENT=Development
SQL_SA_PASSWORD=QualityDoc@2026!
SQL_DB=QualityDocDB
PG_USER=qd_admin
PG_PASSWORD=********
PG_DB=qualitydoc_audit
MONGO_USER=qd_root
MONGO_PASSWORD=********
MONGO_DB=qualitydoc_meta
NODE_API_KEY=node-internal-key-2026
NGINX_FILES_URL=http://localhost:8080/files
PORTAL_EMPRESA_ID=2
SEED_PASSWORD=QualityDoc2026!
```

> El `.env` **no se sube al repositorio** (está en `.gitignore`).

### Plan B (si tu Compose es anterior a v2.20)

```bash
docker compose -f docker-compose.app.yml -f docker-compose.search.yml -f docker-compose.portal.yml up -d --build
```

---

## 🖥️ Ejecutar desde Visual Studio (desarrollo)

1. Levanta solo la infraestructura en Docker:
   ```bash
   docker compose up -d sqlserver postgres mongo node-search
   ```
2. Abre la solución `QualityDoc/QualityDoc.slnx` y ejecuta el proyecto **QualityDoc** (perfil `http`). Usa `appsettings.Development.json` (BDs en `localhost` con los puertos remapeados).

---

## 🔑 Usuarios de prueba (datos sembrados)

Contraseña de todos: **`QualityDoc2026!`**

El correo lleva un **código de rol** antes del `@`: `sa` SuperAdmin, `ad` Admin, `az` Autorizador, `rv` Revisor, `cr` Creador, `lr` Lector.

| Rol | Ejemplo (Empresa Demo, área Calidad) |
|---|---|
| SuperAdmin | `superadmin@qualitydoc.sys` |
| Admin | `roberto.salazar.ad@empresa-demo.com` |
| Autorizador | `maria.lopez.az@empresa-demo.com` |
| Revisor | `juan.perez.rv@empresa-demo.com` |
| Creador | `carlos.ramirez.cr@empresa-demo.com` |
| Lector | `ana.torres.lr@empresa-demo.com` |

Hay además **3 empresas**: `empresa-demo`, `industrias-norte` y `construcciones-sur` (cambia el dominio del correo según la empresa), cada una con sus áreas y usuarios.

---

## 🧭 Cómo se usa (flujo típico)

1. **Creador** → *Documentos* → **Nuevo** → sube un archivo con texto (PDF/DOCX/XLSX/TXT). Queda en `v1.0.0 (Borrador)`.
2. **Revisor** → abre el documento → **Enviar a revisión**.
3. **Autorizador** → abre el documento → **Revisar documento** → busca dentro del texto, revisa el JSON, y **Aprueba** o **Rechaza**.
4. **Cualquiera** → busca una palabra en *Documentos* (fragmentos resaltados) y usa **Ver** para previsualizar + JSON + contenido.
5. **Admin/SuperAdmin** → *Auditoría* y *Reportes* (PDF).
6. **Portal público** → `http://localhost:8080/portal/`.

### URLs de acceso

| Qué | URL |
|---|---|
| App principal (.NET) | `http://localhost:8080/` |
| Portal PHP | `http://localhost:8080/portal/` |
| API de búsqueda (Node) | `http://localhost:8080/search/` |
| Archivos | `http://localhost:8080/files/...` |
| SQL Server (SSMS) | `localhost,1450` |
| PostgreSQL (pgAdmin) | `localhost:5440` |
| MongoDB (Compass) | `localhost:27018` |

---

## ♻️ Reiniciar / resembrar

La siembra solo corre si la base está vacía. Para cargar de cero los datos demo:

```bash
docker compose down -v        # borra los volúmenes (datos)
docker compose up -d --build  # recrea, migra y siembra de nuevo
```

---

## 📁 Estructura del repositorio

```
QualityDoc/
├── QualityDoc/                 # App .NET Core 10 MVC (núcleo y flujo)
├── node-search/                # Microservicio Node.js/TypeScript (búsqueda)
├── php-portal/                 # Portal PHP 8.3 (consulta y reportes)
├── nginx/                      # Configuración del gateway / file server
├── db/                         # Scripts de las 3 bases (referencia)
├── storage/                    # Archivos físicos de los documentos (volumen)
├── docs/                       # Documentación, guion, presentación y UML
├── docker-compose.yml          # Principal (include de los 3 de abajo)
├── docker-compose.app.yml      # web + sqlserver + postgres
├── docker-compose.search.yml   # node-search + mongo
├── docker-compose.portal.yml   # php-portal + nginx
└── .env                        # Secretos (no se versiona)
```
