using System;
using System.IO;

namespace HotelImporter
{
    class Program
    {
        
        static string inputFolder;
        static string processedFolder;
        static string errorFolder; // Carpeta para aislar archivos fallidos

        static void Main(string[] args)
        {
            LoadEnvConfig();

            Console.Title = "Hotel Data Importer - Orquestador";
            Console.WriteLine("==========================================");
            Console.WriteLine("   INICIANDO ORQUESTADOR DE INTEGRACIÓN   ");
            Console.WriteLine("==========================================");
            Console.WriteLine($"[INFO] Monitoreando ruta: {inputFolder}");

            EnsureDirectories();

            string[] files = Directory.GetFiles(inputFolder, "*.xml");
            Console.WriteLine($"[INFO] Archivos detectados: {files.Length}");

            if (files.Length == 0)
            {
                Console.WriteLine("\nNo hay nada pendiente. Presiona ENTER para salir.");
                //Console.ReadLine();
                return;
            }

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
                    

                    Console.WriteLine($"    Ejecutando SP: {spToUse}...");
                    DatabaseHelper.ExecuteImportSp(spToUse, fileName, xmlContent);

                    MoveFile(filePath, processedFolder);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("    [OK] Importación exitosa.");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    [ERROR] {ex.Message}");
                    Console.ResetColor();

                    MoveFile(filePath, errorFolder);
                }
            }

            Console.WriteLine("\n==========================================");
            Console.WriteLine("PROCESO TERMINADO. Presiona ENTER.");
           // Console.ReadLine();
        }

        static string IdentifyStoredProcedure(string fileName)
        {
            string nameUpper = fileName.ToUpper();

            if (nameUpper.Contains("STATISTICS")) return "sp_Import_Hotel_Statistics";
            if (nameUpper.Contains("CUSTOMER"))   return "sp_Import_Hotel_Customers";
            if (nameUpper.Contains("CITY_LEDGER")) return "sp_Import_Hotel_CityLedger";
            if (nameUpper.Contains("REVENUE"))    return "sp_Import_Hotel_Revenue";

            return null; // Retorna null si no sabe qué es
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

        static void EnsureDirectories()
        {
            if (!Directory.Exists(inputFolder)) Directory.CreateDirectory(inputFolder);
            if (!Directory.Exists(processedFolder)) Directory.CreateDirectory(processedFolder);
            if (!Directory.Exists(errorFolder)) Directory.CreateDirectory(errorFolder);
        }

        static void LoadEnvConfig()
        {
            inputFolder = @"E:\arubavc\files";
            processedFolder = @"E:\arubavc\files\processed";
            errorFolder = @"E:\arubavc\files\error";
            
            string dbServer = @".\SQL_JSANTANA";
            string dbName = "arubavcImport";
            string dbUser = "";
            string dbPassword = "";

            string envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            
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
                        
                        if (key == "DB_SERVER") dbServer = val;
                        if (key == "DB_NAME") dbName = val;
                        if (key == "DB_USER") dbUser = val;
                        if (key == "DB_PASSWORD") dbPassword = val;
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