using Dapper; 
using Ferreteria.DTOs;
using Ferreteria.Entities;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    public interface IProveedorRepository
    {
        Task<ProveedorDto> GetByIdAsync(long id);
        Task<IEnumerable<ProveedorDto>> GetAllAsync(string busqueda = null);
        Task<long> CreateAsync(string nombre);
        Task<bool> ExisteNombreAsync(string nombre); 
        Task<long> CreateAsync(ProveedorCreateDto dto);
        Task<bool> UpdateAsync(ProveedorUpdateDto dto);
        Task<bool> DeleteAsync(long id);
        Task<bool> ExisteNombreAsync(string nombre, long? excluirId = null);
        Task<int> GetCantidadProductosAsync(long proveedorId);
        Task<bool> TieneProductosAsync(long proveedorId);
    }

    public class ProveedorRepository : IProveedorRepository
    {

        private readonly string _connectionString;
        public ProveedorRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");

        }

        public async Task<ProveedorDto> GetByIdAsync(long id)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = "SELECT id_proveedor as Id, Nombre , FechaCreacion FROM [TiendaDB].[dbo].Proveedor WHERE id_proveedor = @Id";
                   
                    return await cn.QueryFirstOrDefaultAsync<ProveedorDto>(query, new { Id = id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.GetByIdAsync: " + ex.Message);
                return null;
            }
           
        }

        public async Task<IEnumerable<ProveedorDto>> GetAllAsync(string busqueda = null)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var query = "SELECT id_proveedor as Id, Nombre, FechaCreacion FROM [TiendaDB].[dbo].Proveedor WHERE 1=1";
                    var parameters = new DynamicParameters();

                    if (!string.IsNullOrWhiteSpace(busqueda))
                    {
                        query += " AND Nombre LIKE @Busqueda";
                        parameters.Add("Busqueda", $"%{busqueda}%");
                    }

                    query += " ORDER BY Nombre"; 
                    return await cn.QueryAsync<ProveedorDto>(query, parameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.GetAllAsync: " + ex.Message);
                return null;
            }
           
        }

        public async Task<long> CreateAsync(string nombre)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @" INSERT INTO [TiendaDB].[dbo].Proveedor (Nombre) VALUES (@Nombre);
                        SELECT CAST(SCOPE_IDENTITY() as bigint);";

                   
                    return await cn.QuerySingleAsync<long>(query, new { Nombre = nombre });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.CreateAsync: " + ex.Message);
                return 0;
            }
            
        }

        public async Task<bool> ExisteNombreAsync(string nombre)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = "SELECT COUNT(1) FROM [TiendaDB].[dbo].Proveedor WHERE Nombre = @Nombre";
                    
                    var count = await cn.ExecuteScalarAsync<int>(query, new { Nombre = nombre });
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.ExisteNombreAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<long> CreateAsync(ProveedorCreateDto dto)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @"   INSERT INTO [TiendaDB].[dbo].Proveedor (nombre)  VALUES (@Nombre);
                        SELECT CAST(SCOPE_IDENTITY() as bigint);";

                   
                    return await cn.QuerySingleAsync<long>(query, new { Nombre = dto.Nombre });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.CreateAsync: " + ex.Message);
                return 0;
            }
            
        }

        public async Task<bool> UpdateAsync(ProveedorUpdateDto dto)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @" UPDATE [TiendaDB].[dbo].Proveedor SET nombre = @Nombre WHERE id_proveedor = @Id";
                                        
                    var affected = await cn.ExecuteAsync(query, new { Id = dto.Id, Nombre = dto.Nombre });
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.UpdateAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<bool> DeleteAsync(long id)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    if (await TieneProductosAsync(id))
                    {
                        throw new InvalidOperationException("No se puede eliminar el proveedor porque tiene productos asociados");
                    }

                    const string query = "DELETE FROM [TiendaDB].[dbo].Proveedor WHERE id_proveedor = @Id";
                    
                    var affected = await cn.ExecuteAsync(query, new { Id = id });
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.DeleteAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<bool> ExisteNombreAsync(string nombre, long? excluirId = null)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var query = "SELECT COUNT(1) FROM [TiendaDB].[dbo].Proveedor WHERE nombre = @Nombre";
                    if (excluirId.HasValue)
                        query += " AND id_proveedor != @ExcluirId";

                  
                    var count = await cn.ExecuteScalarAsync<int>(query, new { Nombre = nombre, ExcluirId = excluirId });
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.ExisteNombreAsync: " + ex.Message);
                return false;
            }
            
        }
        public async Task<int> GetCantidadProductosAsync(long proveedorId)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @" SELECT COUNT(1)  FROM [TiendaDB].[dbo].Productos   WHERE IdProveedor = @ProveedorId AND Activo = 1";
                                        
                    return await cn.ExecuteScalarAsync<int>(query, new { ProveedorId = proveedorId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProveedorRepository.GetCantidadProductosAsync: " + ex.Message);
                return 0;
            }
            
        }

        public async Task<bool> TieneProductosAsync(long proveedorId)
        {
            return await GetCantidadProductosAsync(proveedorId) > 0;
        }
    }
}
