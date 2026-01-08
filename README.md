# 🏨 Hotel XML Data Integrator

Este proyecto es una solución **ETL (Extract, Transform, Load)** automatizada desarrollada en **.NET (C#)**. Su objetivo es orquestar la ingesta masiva de datos operativos hoteleros provenientes de archivos XML hacia una base de datos SQL Server.

---

## 🚀 Funcionalidades Principales

* **Detección Automática:** Monitorea un directorio de entrada y detecta archivos XML.
* **Clasificación Inteligente:** Identifica el tipo de archivo (*Statistics, Customer, City Ledger, Revenue*) basándose en el nombre.
* **Limpieza de Datos:** Sanitiza el XML (elimina declaraciones de encoding conflictivas) antes de enviarlo a la BD.
* **Procesamiento Transaccional:** Utiliza *Stored Procedures* optimizados para insertar y aplanar la estructura jerárquica de los XML.
* **Gestión de Archivos:**
    * ✅ **Éxito:** Mueve los archivos procesados a una carpeta histórica (`/processed`).
    * ❌ **Error:** Aísla los archivos fallidos en una carpeta de auditoría (`/error`).

## 🛠️ Stack Tecnológico

* **Lenguaje:** C# (.NET 8.0 / 10.0)
* **Tipo de App:** Console Application (Orquestador)
* **Base de Datos:** Microsoft SQL Server
* **Librerías:** `Microsoft.Data.SqlClient`

---

## 📂 Estructura del Proyecto

El sistema está diseñado para operar bajo la siguiente estructura de directorios física (configurable en `Program.cs`):

```bash
E:\arubavc\
│
├── 📁 apps\
│   └── 📁 HotelImporter\       # Código Fuente y Ejecutable (.exe)
│       ├── Program.cs          # Lógica del Orquestador
│       ├── DatabaseHelper.cs   # Capa de Acceso a Datos (ADO.NET)
│       └── HotelImporter.csproj
│
└── 📁 files\                   # Directorio de Trabajo (Data Lake)
    ├── 📄 *.xml                # Archivos de Entrada (Input)
    ├── 📁 processed\           # Archivos procesados correctamente (Histórico)
    └── 📁 error\               # Archivos que fallaron (Para revisión manual)


    ## 💾 Base de Datos (Mapeo)

El sistema inyecta datos en 4 tablas principales mediante 4 Stored Procedures dedicados. La lógica es acumulativa (**Append Only**).

| Tipo de Archivo (XML) | Tabla SQL Destino | Stored Procedure | Descripción |
| :--- | :--- | :--- | :--- |
| `*STATISTICS*.xml` | `Hotel_Statistics` | `sp_Import_Hotel_Statistics` | Ocupación, Pax, No-Shows. |
| `*CUSTOMER*.xml` | `Hotel_Customers` | `sp_Import_Hotel_Customers` | Perfiles, Direcciones, Emails (Aplanado). |
| `*CITY_LEDGER*.xml` | `Hotel_CityLedger` | `sp_Import_Hotel_CityLedger` | Facturación, Cuentas por Cobrar. |
| `*REVENUE*.xml` | `Hotel_Revenue` | `sp_Import_Hotel_Revenue` | Desglose financiero (Header/Detail aplanado). |

## ⚙️ Configuración

### 1. Cadena de Conexión
La conexión a la base de datos se define en `DatabaseHelper.cs`. Asegúrese de apuntar a la instancia correcta (Desarrollo/QA/Prod).

```csharp
// Ejemplo de configuración actual
private static string _connectionString = @"Server=ESCRITORIOJS\SQL_JSANTANA;Database=arubavcImport;Integrated Security=True;TrustServerCertificate=True;";

### 2. Rutas de Carpetas
Las rutas de entrada y salida se definen en Program.cs:

static string inputFolder = @"E:\arubavc\files";
static string processedFolder = @"E:\arubavc\files\processed";
static string errorFolder = @"E:\arubavc\files\error";

###  Ejecución y Despliegue
En Desarrollo (VS Code / Visual Studio)
Para correr el orquestador manualmente:

Bash

cd E:\arubavc\apps\HotelImporter
dotnet run


Para Despliegue (QA / Producción)
Para generar el ejecutable (.exe) independiente que no requiere código fuente:

Bash

dotnet publish --configuration Release --output "E:\arubavc\deploy"