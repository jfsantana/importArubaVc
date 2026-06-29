# Manual Técnico - Proceso de Importación HotelImporter

## 1) Objetivo
Este documento describe el flujo técnico completo del importador, incluyendo:
- rutas de entrada/salida
- lógica de procesamiento por tipo de archivo
- extracción de forecast desde `.aud`
- reglas de limpieza de tabla
- excepciones y manejo de errores
- envío de notificaciones por correo

---

## 2) Componente principal
- Aplicación: `HotelImporter` (.NET)
- Punto de entrada: `Program.Main()`
- Acceso a BD: `DatabaseHelper`

---

## 3) Configuración (.env)
Variables usadas por el proceso:

- `INPUT_FOLDER`: carpeta donde se leen XML a procesar.
- `PROCESSED_FOLDER`: carpeta destino de archivos procesados correctamente.
- `ERROR_FOLDER`: carpeta destino de archivos con error.
- `AUDIT_ROOT_FOLDER`: carpeta raíz de forecast `.aud`.
- `DB_SERVER`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`: conexión SQL Server.
- `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASS`, `EMAIL_FROM`, `EMAIL_TO`: notificación por correo.

### Prioridad de carga de `.env`
1. Junto al ejecutable (`AppDomain.CurrentDomain.BaseDirectory`)  
2. Directorio actual (`Directory.GetCurrentDirectory()`)

---

## 4) Flujo general del proceso

1. Carga configuración (`LoadEnvConfig`).
2. Asegura carpetas (`EnsureDirectories`).
3. Prepara forecast desde `.aud` (`PrepareFutureOccupancyFromAudit`).
4. Lista `*.xml` en `INPUT_FOLDER`.
5. Si no hay archivos:
   - envía correo de ejecución sin pendientes
   - termina.
6. Si hay archivos, procesa uno por uno:
   - identifica SP por nombre de archivo
   - lee XML
   - ejecuta reglas especiales (forecast)
   - ejecuta SP de importación
   - mueve archivo a `processed` o `error`
7. Envía correo resumen HTML con éxitos/fallos.

---

## 5) Tipos de archivo y SP asociados

| Patrón en nombre de archivo | SP de importación |
|---|---|
| `RESFUTUREOCCUPANCY` | `sp_Import_Hotel_FutureOccupancy` |
| `STATISTICS` | `sp_Import_Hotel_Statistics` |
| `CUSTOMER` | `sp_Import_Hotel_Customers` |
| `CITY_LEDGER` | `sp_Import_Hotel_CityLedger` |
| `REVENUE` | `sp_Import_Hotel_Revenue` |
| `DETAIL_AVAIL` | `sp_Import_Hotel_ForecastOcc` |

Si el nombre no coincide con ningún patrón, se lanza excepción:  
`Tipo de archivo no reconocido por el nombre.`

---

## 6) Lógica específica de Forecast (`RESFUTUREOCCUPANCY`)

### 6.1 Extracción desde `.aud`
Se ejecuta antes del bucle principal:

- Calcula día objetivo: `DateTime.Today.AddDays(-1)`
- Formato carpeta: `MMddyy` (ej: `061026`)
- Ruta esperada: `AUDIT_ROOT_FOLDER\MMddyy`
- Busca `*.aud` y toma en orden por fecha de modificación descendente.

Para cada `.aud`:
- genera nombre objetivo:  
  `resfutureoccupancy_<MMddyy>_<nombreAud>.xml`
- evita duplicados si ya existe en:
  - `INPUT_FOLDER`
  - `PROCESSED_FOLDER`
  - `ERROR_FOLDER`
- abre zip `.aud`, busca la primera entrada `.xml`
- extrae el xml a `INPUT_FOLDER`

### 6.2 Carga en BD para Forecast
Cuando en el bucle se procesa un archivo `RESFUTUREOCCUPANCY`:

1. Evalúa si el XML contiene filas (`G_CONSIDERED_DATE`).
2. **Solo si contiene filas**, limpia tabla:
   - `DELETE FROM [dbo].[Hotel_FutureOccupancy]`
3. Ejecuta SP de importación:
   - `sp_Import_Hotel_FutureOccupancy`
4. Ejecuta post-proceso:
   - `SP_Cargar_Proyeccion_Directores`

### 6.3 Tabla impactada por forecast
- Tabla principal de carga: **`dbo.Hotel_FutureOccupancy`**

---

## 7) Manejo de excepciones y comportamiento

## 7.1 Errores por archivo (bucle principal)
Cada archivo se procesa dentro de `try/catch`:
- Si ocurre error:
  - se imprime `[ERROR] <mensaje>`
  - archivo se mueve a `ERROR_FOLDER`
  - se registra como fallido en el correo

## 7.2 Errores en preparación de `.aud`
`PrepareFutureOccupancyFromAudit()` tiene `try/catch` global:
- Si falla acceso/red/zip/xml:
  - registra `[AUDIT] Error preparando XML desde .aud: ...`
  - continúa ejecución general

## 7.3 Casos de salida temprana (audit)
- `AUDIT_ROOT_FOLDER` vacío
- carpeta diaria inexistente
- no hay `.aud`
- todos duplicados
- `.aud` sin xml válido

## 7.4 Validación de XML forecast antes de limpiar
`HasFutureOccupancyRows(xmlContent)`:
- parsea XML con `XDocument`
- verifica existencia de `G_CONSIDERED_DATE`
- si XML inválido o sin filas, **no limpia la tabla**

---

## 8) Notificación por correo

Se envía correo HTML en dos escenarios:

1. Sin archivos pendientes (`Total Archivos = 0`)
2. Con procesamiento (resumen de éxitos/fallos por archivo)

Condición para enviar correo:
- `SMTP_HOST` y `EMAIL_TO` no vacíos

SMTP actual recomendado:
- Host: `smtp.gmail.com`
- Puerto: `587`
- SSL/TLS habilitado

---

## 9) Rutas operativas esperadas

- Entrada XML: `INPUT_FOLDER` (ej. `O:\`)
- Procesados: `PROCESSED_FOLDER` (ej. `O:\importArubaVc\files\processed`)
- Errores: `ERROR_FOLDER` (ej. `O:\importArubaVc\files\error`)
- Forecast audit: `AUDIT_ROOT_FOLDER` (ej. `\\10.130.112.12\aualb\audit`)

---

## 10) Ejecución manual

### Producción (binario publicado)
1. `cd O:\importArubaVc\deploy`
2. `./HotelImporter.exe`

Con log a archivo:
- `./HotelImporter.exe *>> ./run_manual.log`

### Desarrollo (código fuente)
1. `cd O:\importArubaVc\apps\HotelImporter`
2. `dotnet run`

---

## 11) Checklist operativo rápido

- [ ] `.env` correcto en entorno de ejecución
- [ ] acceso a `INPUT_FOLDER`, `PROCESSED_FOLDER`, `ERROR_FOLDER`
- [ ] acceso a `AUDIT_ROOT_FOLDER\MMddyy`
- [ ] credenciales SQL válidas
- [ ] SPs existentes en BD
- [ ] SMTP válido para notificaciones

---

## 12) Observaciones importantes

- El proceso depende del nombre de archivo para elegir SP.
- Forecast tiene lógica especial (extracción `.aud`, limpieza condicional de tabla y post-SP).
- Si un archivo falla, no detiene el lote completo: se aísla en `error` y se continúa.
