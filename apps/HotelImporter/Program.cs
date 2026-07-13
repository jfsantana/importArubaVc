using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Mail;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace HotelImporter
{
    class Program
    {

        static string inputFolder = string.Empty;
        static string processedFolder = string.Empty;
        static string errorFolder = string.Empty; // Carpeta para aislar archivos con error
        static string auditRootFolder = string.Empty; // Nueva ruta de auditoria para .aud
        static string cxcAuditRootFolder = string.Empty; // Ruta de auditoría para XML 029_
        
        // 2. CONFIGURACIÓN DE CORREO
        static string smtpHost = string.Empty;
        static int smtpPort = 0;
        static string smtpUser = string.Empty;
        static string smtpPass = string.Empty;
        static string emailFrom = string.Empty;
        static string emailTo = string.Empty; // Destinatarios separados por coma
        static int dbCommandTimeoutSeconds = 300;
        static int futureOccupancyTimeoutSeconds = 1800;


        static void Main(string[] args)
        {
            try
            {
                LoadEnvConfig();

            Console.Title = "Hotel Data Importer - Orquestador";
            Console.WriteLine("==========================================");
            Console.WriteLine("   INICIANDO ORQUESTADOR DE INTEGRACIÓN   ");
            Console.WriteLine("==========================================");
            Console.WriteLine($"[INFO] Monitoreando ruta: {inputFolder}");

            EnsureDirectories();
            PrepareFutureOccupancyFromAudit();
            PrepareCuentasPorCobrarFromAudit();

            string[] files = Directory.GetFiles(inputFolder, "*.xml");
            Console.WriteLine($"[INFO] Archivos detectados: {files.Length}");

            if (files.Length == 0)
            {
                Console.WriteLine("\nNo hay archivos pendientes.");
                SendEmailReport($@"
<html><body style='font-family:Arial,sans-serif;'>
<h2 style='color:#333;'>Reporte de Ejecución - HotelImporter</h2>
<div style='padding:15px;background:#f5f5f5;border-radius:5px;'>
  <p><strong>Fecha:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
  <p><strong>Total Archivos:</strong> 0</p>
  <p style='color:gray;'>No había archivos pendientes para procesar en esta ejecución.</p>
</div>
</body></html>");
                return;
            }

            // Contadores y buffer para el reporte HTML
            int successCount = 0;
            int errorCount = 0;
            StringBuilder htmlRows = new StringBuilder();

            // 4. BUCLE DE PROCESAMIENTO
            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"\n--> Procesando: {fileName}");

                try
                {
                    string spToUse = IdentifyStoredProcedure(fileName);
                    
                    if (string.IsNullOrEmpty(spToUse))
                        throw new Exception("Tipo de archivo no reconocido por el nombre.");

                    string xmlContent = File.ReadAllText(filePath);
                    xmlContent = xmlContent.Replace("encoding=\"UTF-8\"", "").Replace("encoding=\"utf-8\"", "");
                    

                    if (fileName.ToUpper().Contains("RESFUTUREOCCUPANCY"))
                    {
                        if (HasFutureOccupancyRows(xmlContent))
                        {
                            Console.WriteLine("    Limpiando tabla destino de Future Occupancy...");
                            DatabaseHelper.ExecuteSqlNonQuery("DELETE FROM [dbo].[Hotel_FutureOccupancy]");
                            Console.WriteLine("    [OK] Tabla [dbo].[Hotel_FutureOccupancy] limpiada.");
                        }
                        else
                        {
                            Console.WriteLine("    [INFO] El XML no contiene filas de ocupación futura. No se limpia la tabla.");
                        }
                    }

                    int timeoutToUse = fileName.ToUpper().Contains("RESFUTUREOCCUPANCY")
                        ? futureOccupancyTimeoutSeconds
                        : dbCommandTimeoutSeconds;

                    Console.WriteLine($"    Ejecutando SP: {spToUse} (timeout {timeoutToUse}s)...");
                    DatabaseHelper.ExecuteImportSp(spToUse, fileName, xmlContent, timeoutToUse);

                    // ------------------------------------------------------------------
                    // POST-PROCESO ESPECÍFICO
                    // Solo cuando el archivo procesado es resfutureoccupancy*****.xml
                    // se dispara el SP que recalcula la proyección de directores.
                    // Para CUALQUIER otro archivo, este bloque NO se ejecuta.
                    // ------------------------------------------------------------------
                    string postProcessInfo = string.Empty;
                    if (fileName.ToUpper().Contains("RESFUTUREOCCUPANCY"))
                    {
                        const string postSp = "SP_Cargar_Proyeccion_Directores";
                        Console.WriteLine($"    Ejecutando post-proceso: {postSp}...");
                        DatabaseHelper.ExecuteSimpleSp(postSp);
                        postProcessInfo = $" + {postSp}";
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"    [OK] Post-proceso ejecutado.");
                        Console.ResetColor();
                    }

                    MoveFile(filePath, processedFolder);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("    [OK] Importación exitosa.");
                    Console.ResetColor();
                    
                    successCount++;
                    htmlRows.Append($"<tr style='background-color: #e8f5e9;'><td>{fileName}</td><td style='color:green;font-weight:bold;'>EXITOSO</td><td>{spToUse}{postProcessInfo}</td></tr>");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    [ERROR] {ex.Message}");
                    Console.ResetColor();

                    MoveFile(filePath, errorFolder);
                    
                    errorCount++;
                    htmlRows.Append($"<tr style='background-color: #ffebee;'><td>{fileName}</td><td style='color:red;font-weight:bold;'>ERROR</td><td>{ex.Message}</td></tr>");
                }
            }
            
            // 5. ENVIAR REPORTE
            string htmlBody = $@"
<html>
<body style='font-family: Arial, sans-serif;'>
    <h2 style='color: #333;'>Reporte de Ejecución - HotelImporter</h2>
    <div style='margin-bottom: 20px; padding: 15px; background-color: #f5f5f5; border-radius: 5px;'>
        <p><strong>Fecha:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
        <p><strong>Total Archivos:</strong> {files.Length}</p>
        <p><strong>Exitosos:</strong> <span style='color: green;'>{successCount}</span></p>
        <p><strong>Fallidos:</strong> <span style='color: red;'>{errorCount}</span></p>
    </div>
    <table style='border-collapse: collapse; width: 100%;'>
        <thead>
            <tr style='background-color: #333; color: white;'>
                <th style='padding: 10px; border: 1px solid #ddd;'>Archivo</th>
                <th style='padding: 10px; border: 1px solid #ddd;'>Estado</th>
                <th style='padding: 10px; border: 1px solid #ddd;'>Detalle / SP</th>
            </tr>
        </thead>
        <tbody>{htmlRows}</tbody>
    </table>
</body>
</html>";

            SendEmailReport(htmlBody);

                                Console.WriteLine("\n==========================================");
                                Console.WriteLine("PROCESO TERMINADO. Presiona ENTER.");
                             // Console.ReadLine();
                        }
                        catch (Exception ex)
                        {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[FATAL] Error no controlado en orquestador: {ex.Message}");
                                Console.WriteLine(ex.ToString());
                                Console.ResetColor();

                                SendEmailReport($@"
<html><body style='font-family:Arial,sans-serif;'>
<h2 style='color:#b71c1c;'>[FATAL] HotelImporter detenido por excepción no controlada</h2>
<div style='padding:15px;background:#ffebee;border-radius:5px;'>
    <p><strong>Fecha:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    <p><strong>Mensaje:</strong> {ex.Message}</p>
    <pre style='white-space:pre-wrap;background:#fff;padding:10px;border-radius:4px;border:1px solid #f0c0c0;'>{WebUtility.HtmlEncode(ex.ToString())}</pre>
</div>
</body></html>");
                        }
        }

        static string IdentifyStoredProcedure(string fileName)
        {
            string nameUpper = fileName.ToUpper();

            // IMPORTANTE: el check de RESFUTUREOCCUPANCY debe ir ANTES que cualquier
            // otro que pueda colisionar por substring. Como su nombre es único
            // (resfutureoccupancy*****.xml) no hay riesgo, pero lo dejamos arriba
            // para evitar confusiones futuras.
            if (nameUpper.Contains("RESFUTUREOCCUPANCY")) return "sp_Import_Hotel_FutureOccupancy";

            if (nameUpper.Contains("STATISTICS"))   return "sp_Import_Hotel_Statistics";
            if (nameUpper.Contains("CUSTOMER"))     return "sp_Import_Hotel_Customers";
            if (nameUpper.Contains("CITY_LEDGER"))  return "sp_Import_Hotel_CityLedger";
            if (nameUpper.Contains("REVENUE"))      return "sp_Import_Hotel_Revenue";
            if (nameUpper.Contains("DETAIL_AVAIL")) return "sp_Import_Hotel_ForecastOcc";
            if (nameUpper.Contains("029_"))         return "sp_Import_Hotel_CuentasPorCobrar";

            return string.Empty; // Retorna vacío si no sabe qué es
        }

        static bool HasFutureOccupancyRows(string xmlContent)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlContent);
                return doc.Descendants("G_CONSIDERED_DATE").Any();
            }
            catch
            {
                // Si el XML está dañado o no se puede parsear,
                // no limpiamos para evitar borrar datos válidos por error.
                return false;
            }
        }

        static void MoveFile(string source, string destFolder)
        {
            try
            {
                string fileName = Path.GetFileName(source);
                string destPath = Path.Combine(destFolder, fileName);

                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(source, destPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [FATAL] No se pudo mover el archivo: {ex.Message}");
            }
        }

        static void SendEmailReport(string body)
        {
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(emailTo)) return;

            try
            {
                Console.WriteLine("\n[MAIL] Enviando reporte...");
                using (SmtpClient client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    if (!string.IsNullOrEmpty(smtpUser))
                        client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    MailMessage mail = new MailMessage();
                    mail.From = new MailAddress(string.IsNullOrEmpty(emailFrom) ? smtpUser : emailFrom);
                    mail.Subject = $"[HotelImporter] Reporte de Ejecución - {DateTime.Now:yyyy-MM-dd}";
                    mail.Body = body;
                    mail.IsBodyHtml = true; // Habilitar HTML

                    foreach (var addr in emailTo.Split(','))
                        if (!string.IsNullOrWhiteSpace(addr)) mail.To.Add(addr.Trim());

                    client.Send(mail);
                    Console.WriteLine("[MAIL] Correo enviado exitosamente.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MAIL ERROR] No se pudo enviar el correo: {ex.Message}");
            }
        }

        static void EnsureDirectories()
        {
            if (!Directory.Exists(inputFolder)) Directory.CreateDirectory(inputFolder);
            if (!Directory.Exists(processedFolder)) Directory.CreateDirectory(processedFolder);
            if (!Directory.Exists(errorFolder)) Directory.CreateDirectory(errorFolder);
        }

        static void PrepareFutureOccupancyFromAudit()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(auditRootFolder))
                {
                    Console.WriteLine("[AUDIT] AUDIT_ROOT_FOLDER no configurado. Se omite preparación de forecast.");
                    return;
                }

                Console.WriteLine($"[AUDIT] Ruta raíz configurada: {auditRootFolder}");

                DateTime targetDay = DateTime.Today.AddDays(-1);
                string dailyFolderName = targetDay.ToString("MMddyy"); // Formato requerido: MMDDAA (ej: 061026)
                string dailyAuditFolder = Path.Combine(auditRootFolder, dailyFolderName);

                Console.WriteLine($"[AUDIT] Fecha objetivo: {targetDay:yyyy-MM-dd}");
                Console.WriteLine($"[AUDIT] Carpeta diaria esperada: {dailyAuditFolder}");

                if (!Directory.Exists(dailyAuditFolder))
                {
                    Console.WriteLine($"[AUDIT] No existe carpeta del dia anterior con formato MMDDAA: {dailyAuditFolder}");
                    return;
                }

                string[] audFiles = Directory
                    .GetFiles(dailyAuditFolder, "*.aud")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray();

                Console.WriteLine($"[AUDIT] Archivos .aud detectados: {audFiles.Length}");

                if (audFiles.Length == 0)
                {
                    Console.WriteLine($"[AUDIT] No hay archivos .aud en: {dailyAuditFolder}");
                    return;
                }

                int duplicateCount = 0;
                int noXmlCount = 0;

                foreach (var audFile in audFiles)
                {
                    Console.WriteLine($"[AUDIT] Evaluando .aud: {audFile}");

                    string targetFileName = $"resfutureoccupancy_{dailyFolderName}_{Path.GetFileNameWithoutExtension(audFile)}.xml";
                    string targetInputPath = Path.Combine(inputFolder, targetFileName);
                    string targetProcessedPath = Path.Combine(processedFolder, targetFileName);
                    string targetErrorPath = Path.Combine(errorFolder, targetFileName);

                    bool existsInInput = File.Exists(targetInputPath);
                    bool existsInProcessed = File.Exists(targetProcessedPath);
                    bool existsInError = File.Exists(targetErrorPath);

                    Console.WriteLine($"[AUDIT] Destino generado: {targetFileName}");
                    Console.WriteLine($"[AUDIT] Existe en input: {existsInInput} -> {targetInputPath}");
                    Console.WriteLine($"[AUDIT] Existe en processed: {existsInProcessed} -> {targetProcessedPath}");
                    Console.WriteLine($"[AUDIT] Existe en error: {existsInError} -> {targetErrorPath}");

                    if (existsInInput || existsInProcessed || existsInError)
                    {
                        Console.WriteLine($"[AUDIT] Ya preparado anteriormente: {targetFileName}");
                        duplicateCount++;
                        continue;
                    }

                    using (FileStream fs = File.OpenRead(audFile))
                    using (ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        Console.WriteLine($"[AUDIT] Entradas dentro del .aud: {archive.Entries.Count}");
                        foreach (var entry in archive.Entries.Take(5))
                        {
                            Console.WriteLine($"[AUDIT] Entry detectada: {entry.FullName}");
                        }

                        var xmlEntry = archive.Entries.FirstOrDefault(e =>
                            !string.IsNullOrEmpty(e.Name) &&
                            e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));

                        if (xmlEntry == null)
                        {
                            Console.WriteLine($"[AUDIT] El archivo no contiene XML: {Path.GetFileName(audFile)}");
                            noXmlCount++;
                            continue;
                        }

                        Console.WriteLine($"[AUDIT] XML seleccionado para extraer: {xmlEntry.FullName}");
                        Console.WriteLine($"[AUDIT] Extrayendo hacia: {targetInputPath}");

                        using (Stream source = xmlEntry.Open())
                        using (FileStream dest = File.Create(targetInputPath))
                        {
                            source.CopyTo(dest);
                        }

                        Console.WriteLine($"[AUDIT] XML extraido desde {Path.GetFileName(audFile)} -> {targetFileName}");
                        return;
                    }
                }

                if (duplicateCount > 0 && duplicateCount == audFiles.Length)
                {
                    Console.WriteLine("[AUDIT] Todos los .aud detectados fueron omitidos porque ya existía su XML preparado o procesado.");
                    return;
                }

                if (noXmlCount > 0)
                {
                    Console.WriteLine("[AUDIT] Se revisaron .aud pero ninguno aportó un XML válido para preparar forecast.");
                    return;
                }

                Console.WriteLine("[AUDIT] No fue posible preparar XML de ocupacion futura desde los .aud detectados.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIT] Error preparando XML desde .aud: {ex.Message}");
            }
        }

        static void PrepareCuentasPorCobrarFromAudit()
        {
            try
            {
                string auditPathToUse = string.IsNullOrWhiteSpace(cxcAuditRootFolder)
                    ? auditRootFolder
                    : cxcAuditRootFolder;

                if (string.IsNullOrWhiteSpace(auditPathToUse))
                {
                    Console.WriteLine("[AUDIT-CXC] AUDIT_ROOT_FOLDER no configurado. Se omite preparación de CxC.");
                    return;
                }

                Console.WriteLine($"[AUDIT-CXC] Buscando archivo 029_*.xml en auditoría...");

                DateTime targetDay = DateTime.Today.AddDays(-1);
                string dailyFolderName = targetDay.ToString("MMddyy");
                string dailyAuditFolder = Path.Combine(auditPathToUse, dailyFolderName);

                Console.WriteLine($"[AUDIT-CXC] Carpeta audit: {dailyAuditFolder}");

                if (!Directory.Exists(dailyAuditFolder))
                {
                    Console.WriteLine($"[AUDIT-CXC] Carpeta no existe: {dailyAuditFolder}");
                    return;
                }

                string sourceFile = Directory
                    .GetFiles(dailyAuditFolder, "029_*.xml")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(sourceFile))
                {
                    Console.WriteLine($"[AUDIT-CXC] No se encontró archivo 029_*.xml en {dailyAuditFolder}");
                    return;
                }

                string targetFileName = $"029_{dailyFolderName}_{Path.GetFileNameWithoutExtension(sourceFile)}.xml";
                string targetInputPath = Path.Combine(inputFolder, targetFileName);
                string targetProcessedPath = Path.Combine(processedFolder, targetFileName);
                string targetErrorPath = Path.Combine(errorFolder, targetFileName);

                if (File.Exists(targetInputPath) || File.Exists(targetProcessedPath) || File.Exists(targetErrorPath))
                {
                    Console.WriteLine($"[AUDIT-CXC] Archivo CxC ya existe como preparado/procesado/error. Se omite.");
                    return;
                }

                Console.WriteLine($"[AUDIT-CXC] Copiando: {sourceFile} -> {targetInputPath}");
                File.Copy(sourceFile, targetInputPath, overwrite: false);
                Console.WriteLine($"[AUDIT-CXC] [OK] Archivo CxC preparado para importación.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIT-CXC] Error preparando CxC: {ex.Message}");
            }
        }

        static void LoadEnvConfig()
        {
            inputFolder = @"E:\arubavc\files";
            processedFolder = @"E:\arubavc\files\processed";
            errorFolder = @"E:\arubavc\files\error";
            auditRootFolder = @"\\10.130.112.12\aualb\audit";
            cxcAuditRootFolder = @"\\10.130.112.12\Audit";
            
            string dbServer = @".\SQL_JSANTANA";
            string dbName = "arubavcImport";
            string dbUser = "";
            string dbPassword = "";
            
            // Valores por defecto SMTP (Vacíos para no enviar si no se configuran)
            smtpHost = "";
            smtpPort = 0;

            // Busca el archivo .env en el directorio de ejecución
            // Prioridad: Junto al ejecutable (QA/Prod) -> Directorio actual (Dev/dotnet run)
            string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
            if (!File.Exists(envPath)) envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            
            if (File.Exists(envPath))
            {
                Console.WriteLine($"[CONFIG] Sobrescribiendo configuración desde: {envPath}");
                foreach (var line in File.ReadAllLines(envPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#")) continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();

                        if (key == "INPUT_FOLDER") inputFolder = val;
                        if (key == "PROCESSED_FOLDER") processedFolder = val;
                        if (key == "ERROR_FOLDER") errorFolder = val;
                        if (key == "AUDIT_ROOT_FOLDER") auditRootFolder = val;
                        if (key == "CXC_AUDIT_ROOT_FOLDER") cxcAuditRootFolder = val;
                        if (key == "AUDIT_BASE_PATH") cxcAuditRootFolder = val;
                        
                        if (key == "DB_SERVER") dbServer = val;
                        if (key == "DB_NAME") dbName = val;
                        if (key == "DB_USER") dbUser = val;
                        if (key == "DB_PASSWORD") dbPassword = val;
                        if (key == "DB_COMMAND_TIMEOUT_SECONDS" && int.TryParse(val, out int dbTimeout)) dbCommandTimeoutSeconds = dbTimeout;
                        if (key == "FUTURE_OCCUPANCY_TIMEOUT_SECONDS" && int.TryParse(val, out int futureTimeout)) futureOccupancyTimeoutSeconds = futureTimeout;
                        
                        // Configuración de Correo
                        if (key == "SMTP_HOST") smtpHost = val;
                        if (key == "SMTP_PORT" && int.TryParse(val, out int p)) smtpPort = p;
                        if (key == "SMTP_USER") smtpUser = val;
                        if (key == "SMTP_PASS") smtpPass = val;
                        if (key == "EMAIL_FROM") emailFrom = val;
                        if (key == "EMAIL_TO") emailTo = val;
                    }
                }
            }
            
            string connString;
            if (!string.IsNullOrEmpty(dbUser) && !string.IsNullOrEmpty(dbPassword))
            {
                connString = $"Server={dbServer};Database={dbName};User Id={dbUser};Password={dbPassword};TrustServerCertificate=True;";
            }
            else
            {
                connString = $"Server={dbServer};Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
            }
            
            DatabaseHelper.SetConnectionString(connString);
        }
    }
}