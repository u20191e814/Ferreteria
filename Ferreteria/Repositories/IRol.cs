using Dapper;
using Ferreteria.DTOs;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    public interface IRol
    {
        Task<List<RolDto>> GetRol();

    }
    public class RolRepository : IRol
    {
        private readonly string _connectionString;
        public RolRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }
        public async Task<List<RolDto>> GetRol()
        {
            List<RolDto> lista = new List<RolDto>();
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = "SELECT   [Id]    ,[Nombre]   FROM [TiendaDB].[dbo].[Roles] ";
                    var tr = await cn.QueryAsync<RolDto>(query);
                    lista = tr.ToList();    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener la tienda: {ex.Message}");
            }
            return lista;
        }
    }
}
