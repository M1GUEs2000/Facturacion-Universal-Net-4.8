# CLAUDE.md — Facturación Universal .NET 4.8

API REST de facturación electrónica ecuatoriana (SRI) en .NET 4.8 / ASP.NET WebAPI 2. Reescritura de [[facturacion-universal]] (.NET 8) adaptada al servidor de la empresa.

## Build & Run

```powershell
dotnet restore
dotnet build
dotnet run --project Facturacion.Api
```

Requisitos: .NET Framework 4.8, SQL Server (BD `FacturacionCore` en `92.204.64.145`). Antes del primer arranque: ejecutar `Facturacion.Infraestructura/Scripts/schema.sql` y copiar `secrets.config.example` → `secrets.config` con credenciales reales.

## Estructura de proyectos

| Proyecto | Rol |
|---|---|
| `Facturacion.Core` | Dominio puro sin dependencias: entidades, enums, interfaces, casos de uso, OrquestadorEmision/Reintento, GeneradorClaveAcceso, ErrorOr |
| `Facturacion.Infraestructura` | Dapper + SqlClient (repositorios), FirmaXadesNetCore (firma), iText 7 (RIDE), HttpClient SOAP (SRI), storage filesystem |
| `Facturacion.Api` | WebAPI 2: controllers, JWT, FluentValidation, CompositionRoot (MS.Extensions.DI) |
| `Facturacion.Tests` | xUnit + FluentAssertions + NSubstitute |

**Dirección de dependencia:** Api → Core ← Infraestructura. Core nunca referencia las otras capas.

## Reglas de código (OBLIGATORIAS)

### Manejo de errores

- **Dominio (Core):** nunca lanzar exceptions como flujo de control — usar `ErrorOr<T>`. Los errores tipados viven en `Errores.cs`.
- **Excepciones reales (infra/IO):** todo `catch` registra con contexto — `_logger.LogError(ex, "Error en {Metodo}", nameof(X))`. Nunca `catch {}` vacío; mínimo `throw`.

### Seguridad

- **SQL**: siempre parametrizado con Dapper (`@param`) — nunca concatenar input en queries
- **Credenciales**: nunca hardcodeadas — `secrets.config` (en `.gitignore`) o variables de entorno
- **Cert password (.p12)**: cifrado AES-256-GCM en BD, nunca texto plano
- **Input externo**: validar en el boundary con FluentValidation antes de llegar al caso de uso

### Reglas críticas del dominio SRI

- **ICE siempre antes que IVA** en `<impuestos>` y `<totalConImpuestos>` — requisito XSD del SRI
- **`iva_base` = `precio_total_sin_impuesto` + `ice_valor`** — el IVA se aplica sobre precio + ICE
- **`precioUnitario` con 6 decimales**, resto de monetarios con 2
- **XML sin BOM** — `UTF8Encoding(false)`
- **Orden `<infoFactura>`**: `guiaRemision` antes de `razonSocialComprador`; `moneda` después de `importeTotal`
- **Secuencial atómico** — `IncrementarYObtenerAsync` (UPDATE ... OUTPUT), nunca read-modify-write
- **INSERT antes del SRI** — el documento existe en BD desde el inicio; el orquestador hace UPDATEs por checkpoint
- **Estados que no bloquean secuencial**: `PENDIENTE` y `NO_AUTORIZADO`

## Cerebro del proyecto

> ⚠️ **OBLIGATORIO leer el vault PRIMERO.** Todo lo que no sea código técnico vive ahí: estado actual, pendientes, decisiones de arquitectura, análisis y contexto de negocio. No re-derivar esa información leyendo código.

### Archivos de entrada obligatorios (leer en este orden)

| Archivo | Qué contiene |
|---|---|
| `d:\Obsidian\Bovedá\proyectos\facturacion-universal-net48\README.md` | Estado actual, decisiones clave, stack |
| `d:\Obsidian\Bovedá\proyectos\facturacion-universal-net48\arquitectura-facturacion-universal-net48.md` | Mapa de módulos y qué nodos están ✅ vs ⚠️ |
| `d:\Obsidian\Bovedá\proyectos\facturacion-universal-net48\tareas.md` | Tareas pendientes (P-XXX / CU-XXXXX) |
| `d:\Obsidian\Bovedá\CLAUDE.md` | Convenciones del vault — leer solo si hay dudas de estructura |

**Referencia de arquitectura:** el proyecto origen [[facturacion-universal]] (.NET 8) tiene los nodos `flujo-core`, `flujo-infraestructura`, `flujo-api`, `bd-facturacion`, `funcionamiento-sri` ya documentados — son la especificación a replicar. Ante dudas de diseño, consultarlos antes que reinventar.

**Flujo de sesión:**
1. Leer `README.md` → estado actual
2. Leer `arquitectura-facturacion-universal-net48.md` → qué nodos están ✅ vs 🔄
3. Ir al nodo `nodos/[modulo].md` si está ✅ — confiar en él, no leer código fuente
4. Si el nodo está 🔄 → leer código fuente y documentar el nodo al terminar
5. Al cerrar sesión → actualizar `## 📌 Estado actual` en README

### Post-commit — mantener nodos sincronizados

Después de cada `git push`, GitHub Actions postea un comentario en el commit con los nodos del vault que pueden estar desactualizados. **Revisar ese comentario y actualizar los nodos afectados en Obsidian antes de cerrar la sesión.** Ver `.github/node-map.yml` para el mapeo completo.
