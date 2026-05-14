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

                    // Pasamos los parámetros estándar que definimos en los 4 SPs
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    cmd.Parameters.AddWithValue("@XmlData", xmlContent);

                    // Ejecutamos (esto puede tardar unos milisegundos)
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}