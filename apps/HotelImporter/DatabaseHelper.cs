using System;
using System.Data;
using Microsoft.Data.SqlClient;


namespace HotelImporter
{
    public static class DatabaseHelper
    {
        
        // Si tienes usuario y clave, usa: "Server=.;Database=arubavcImport;User Id=sa;Password=tuClave;"
        //private static string _connectionString = "Server=localhost;Database=arubavcImport;Integrated Security=True;";
        private static string _connectionString = @"Server=.\SQL_JSANTANA;Database=arubavcImport;Integrated Security=True;TrustServerCertificate=True;";

        public static void SetConnectionString(string connString)
        {
            _connectionString = connString;
        }

        public static void ExecuteImportSp(string spName, string fileName, string xmlContent)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(spName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                     cmd.Parameters.AddWithValue("@FileName", fileName);
                    cmd.Parameters.AddWithValue("@XmlData", xmlContent);
 
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ---------------------------------------------------------------------
        // Ejecuta un Stored Procedure SIN parámetros (post-proceso).
        // Se utiliza para los SPs que se disparan después de una importación
        // específica (ej. SP_Cargar_Proyeccion_Directores tras resfutureoccupancy).
        // ---------------------------------------------------------------------
        public static void ExecuteSimpleSp(string spName, int commandTimeoutSeconds = 300)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                using (SqlCommand cmd = new SqlCommand(spName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = commandTimeoutSeconds;
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}