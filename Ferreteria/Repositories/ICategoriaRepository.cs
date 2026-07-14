using Dapper; 
using Ferreteria.DTOs;
using Ferreteria.Entities;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    public interface ICategoriaRepository
    {
        Task<CategoriaDto> GetByIdAsync(int id);
        Task<IEnumerable<CategoriaDto>> GetAllAsync(string busqueda = null, bool? activo = null);
        Task<int> CreateAsync(CategoriaCreateDto dto);
        Task<bool> UpdateAsync(CategoriaDto dto);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExisteNombreAsync(string nombre, int? excluirId = null);
        Task<int> TieneProductosAsync(int categoriaId);
    }

    public class CategoriaRepository : ICategoriaRepository
    {
      
        private readonly string _connectionString;
        public CategoriaRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<CategoriaDto> GetByIdAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @"  SELECT Id, Nombre, Descripcion  FROM TiendaDB.[dbo].Categorias   WHERE Id = @Id AND Activo = 1";
                                      
                    return await cn.QueryFirstOrDefaultAsync<CategoriaDto>(query, new { Id = id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CategoriaRepository.GetByIdAsync: " + ex.Message);
                return null;
            }
           
        }

        public async Task<IEnumerable<CategoriaDto>> GetAllAsync(string busqueda = null, bool? activo = null)
        {
           
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var query = "SELECT Id, Nombre, Descripcion FROM TiendaDB.[dbo].Categorias WHERE 1=1";
                    var parameters = new DynamicParameters();

                    if (activo.HasValue)
                    {
                        query += " AND Activo = @Activo";
                        parameters.Add("Activo", activo.Value);
                    }

                    if (!string.IsNullOrWhiteSpace(busqueda))
                    {
                        query += " AND (Nombre LIKE @Busqueda OR Descripcion LIKE @Busqueda)";
                        parameters.Add("Busqueda", $"%{busqueda}%");
                    }

                    query += " ORDER BY Nombre";

                   
                    return await cn.QueryAsync<CategoriaDto>(query, parameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CategoriaRepository.GetAllAsync: " + ex.Message);
                return null;
            }
        }

        public async Task<int> CreateAsync(CategoriaCreateDto dto)
        {
           
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @"  INSERT INTO TiendaDB.[dbo].Categorias (Nombre, Descripcion, Activo, FechaCreacion)
                                        VALUES (@Nombre, @Descripcion, 1, GETDATE());
                                    SELECT CAST(SCOPE_IDENTITY() as int);";

                    
                    return await cn.QuerySingleAsync<int>(query, dto);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CategoriaRepository.CreateAsync: " + ex.Message);
                return 0;
            }
        }

        public async Task<bool> UpdateAsync(CategoriaDto dto)
        {
            
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @"  UPDATE TiendaDB.[dbo].Categorias SET Nombre = @Nombre,  Descripcion = @Descripcion WHERE Id = @Id";
                     
                    var affected = await cn.ExecuteAsync(query, dto);
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CategoriaRepository.UpdateAsync: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        { 
           
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = "UPDATE TiendaDB.[dbo].Categorias SET Activo = 0 WHERE Id = @Id";

                    var affected = await cn.ExecuteAsync(query, new { Id = id });
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CategoriaRepository.DeleteAsync: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> ExisteNombreAsync(string nombre, int? excluirId = null)
        {
           
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var query = "SELECT COUNT(1) FROM TiendaDB.[dbo].Categorias WHERE Nombre = @Nombre AND Activo = 1";
                    if (excluirId.HasValue)
                        query += " AND Id != @ExcluirId"; 
                    var count = await cn.ExecuteScalarAsync<int>(query, new { Nombre = nombre, ExcluirId = excluirId });
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("CategoriaRepository.ExisteNombreAsync: " + ex.Message);
                return false;
            }
        }

        public async Task<int> TieneProductosAsync(int categoriaId)
        { 
            try
            {
                string query = "SELECT COUNT(1) FROM TiendaDB.[dbo].Productos WHERE CategoriaId = @CategoriaId AND Activo = 1";
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var count = await cn.ExecuteScalarAsync<int>(query, new { CategoriaId = categoriaId });
                    return count;
                } 
            }
            catch (Exception ex)
            {
                Console.WriteLine("CategoriaRepository.TieneProductosAsync: " + ex.Message);
                return 0;
            }
        }
    }
}
