using System;
using System.IO;

namespace HotelImporter
{
    class Program
    {
        // 1. CONFIGURACIÓN DE RUTAS FÍSICAS
        static string inputFolder = @"E:\arubavc\files";
        static string processedFolder = @"E:\arubavc\files\processed";
        static string errorFolder = @"E:\arubavc\files\error"; // Carpeta para aislar archivos fallidos

        static void Main(string[] args)
        {
            Console.Title = "Hotel Data Importer - Orquestador";
            Console.WriteLine("==========================================");
            Console.WriteLine("   INICIANDO ORQUESTADOR DE INTEGRACIÓN   ");
            Console.WriteLine("==========================================");
            Console.WriteLine($"[INFO] Monitoreando ruta: {inputFolder}");

            // 2. VERIFICACIÓN DE CARPETAS (Las crea si no existen)
            EnsureDirectories();

            // 3. OBTENER ARCHIVOS XML
            string[] files = Directory.GetFiles(inputFolder, "*.xml");
            Console.WriteLine($"[INFO] Archivos detectados: {files.Length}");

            if (files.Length == 0)
            {
                Console.WriteLine("\nNo hay nada pendiente. Presiona ENTER para salir.");
                Console.ReadLine();
                return;
            }

            // 4. BUCLE DE PROCESAMIENTO
            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"\n--> Procesando: {fileName}");

                try
                {
                    // A. Identificar Estrategia (Qué SP usar)
                    string spToUse = IdentifyStoredProcedure(fileName);
                    
                    if (string.IsNullOrEmpty(spToUse))
                        throw new Exception("Tipo de archivo no reconocido por el nombre.");

                    // B. Leer contenido del XML
                    string xmlContent = File.ReadAllText(filePath);
                    // PARCHE PARA SQL SERVER: Eliminamos la declaración de encoding para evitar el error "unable to switch encoding"
                    xmlContent = xmlContent.Replace("encoding=\"UTF-8\"", "").Replace("encoding=\"utf-8\"", "");
                    

                    // C. Enviar a SQL Server
                    Console.WriteLine($"    Ejecutando SP: {spToUse}...");
                    DatabaseHelper.ExecuteImportSp(spToUse, fileName, xmlContent);

                    // D. Mover a carpeta 'Processed' (Éxito)
                    MoveFile(filePath, processedFolder);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("    [OK] Importación exitosa.");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    // MANEJO DE ERRORES
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    [ERROR] {ex.Message}");
                    Console.ResetColor();

                    // Mover a carpeta 'Error' para no bloquear el siguiente
                    MoveFile(filePath, errorFolder);
                }
            }

            Console.WriteLine("\n==========================================");
            Console.WriteLine("PROCESO TERMINADO. Presiona ENTER.");
            Console.ReadLine();
        }

        // Lógica para decidir qué SP usar según el nombre del archivo
        static string IdentifyStoredProcedure(string fileName)
        {
            string nameUpper = fileName.ToUpper();

            if (nameUpper.Contains("STATISTICS")) return "sp_Import_Hotel_Statistics";
            if (nameUpper.Contains("CUSTOMER"))   return "sp_Import_Hotel_Customers";
            if (nameUpper.Contains("CITY_LEDGER")) return "sp_Import_Hotel_CityLedger";
            if (nameUpper.Contains("REVENUE"))    return "sp_Import_Hotel_Revenue";

            return null; // Retorna null si no sabe qué es
        }

        // Utilidad para mover archivos de forma segura (sobrescribiendo si existen)
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
    }
}