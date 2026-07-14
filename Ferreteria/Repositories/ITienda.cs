using Dapper;
using Ferreteria.DTOs;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    public interface ITienda
    {
        Task<TiendaDto> GetTienda();

    }
    public class TiendaRepository  : ITienda
    {
        private readonly string _connectionString;
        public TiendaRepository(IConfiguration configuration )
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<TiendaDto> GetTienda()
        {
            TiendaDto tienda = null;
            try
            {
                using (SqlConnection cn= new (_connectionString))
                {
                    string query = "SELECT TOP 1 nombre, direccion, ruc, correo, celular, whatsapp, descripcion FROM [TiendaDB].[dbo].[Tienda] ";
                    tienda = await cn.QueryFirstOrDefaultAsync<TiendaDto>(query);   
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener la tienda: {ex.Message}");
            }
            return tienda;
        }
    }
}
