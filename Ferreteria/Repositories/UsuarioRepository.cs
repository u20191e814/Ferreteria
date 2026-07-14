using Dapper; 
using Ferreteria.DTOs;
using Ferreteria.Entities;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    
    public interface IUsuarioRepository
    {
        
        Task<IEnumerable<UsuarioListDto>> GetAllAsync(string busqueda = null, int? rol = null);
        Task<IEnumerable<UsuarioListDto>> GetActivosAsync();
        Task<UsuarioListDto> GetByIdAsync(int id);
        Task<bool> ExisteEmailAsync(string email, int? excluirId = null);
        Task<int> CreateAsync(UsuarioCreateDto dto);
        Task<bool> UpdateAsync(UsuarioUpdateDto dto);
        Task<bool> DeleteAsync(int id); // Soft delete
        Task<bool> ValidarCredencialesAsync(string email, string passwordHash);
    }
     
    public class UsuarioRepository : IUsuarioRepository
    {
        
        private readonly string _connectionString;
        public UsuarioRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");

        }
        

        public async Task<UsuarioListDto> GetByIdAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @" SELECT     u.Id,  u.Nombre + ' ' + u.Apellido as NombreCompleto,   u.Email,
                        r.Nombre as Rol,  u.Activo, u.RolId, u.PasswordHash, u.Telefono
                    FROM [TiendaDB].[dbo].Usuarios u INNER JOIN [TiendaDB].[dbo].Roles r ON u.RolId = r.Id
                    WHERE u.Id = @Id AND u.Activo = 1";

                     
                    return await cn.QueryFirstOrDefaultAsync<UsuarioListDto>(query, new { Id = id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UsuarioRepository.GetByIdAsync: " + ex.Message);
                return null;
            }
            
        }

        public async Task<IEnumerable<UsuarioListDto>> GetAllAsync(string busqueda = null, int? rol = null)
        {

            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    var query = @" SELECT    u.Id,  u.Nombre + ' ' + u.Apellido as NombreCompleto, u.Email, r.Nombre as Rol,  u.Activo, u.Telefono, u.UltimoAcceso,u. FechaCreacion
                        FROM [TiendaDB].[dbo].Usuarios u
                        INNER JOIN [TiendaDB].[dbo].Roles r ON u.RolId = r.Id
                        WHERE 1=1";

                    var parameters = new DynamicParameters();

                    
                    if (rol.HasValue)
                    {
                        query += " AND u.RolId = @RolId";
                        parameters.Add("RolId", rol);
                    } 
                    if (!string.IsNullOrWhiteSpace(busqueda))
                    {
                        query += @" AND (u.Nombre LIKE @Busqueda 
                        OR u.Apellido LIKE @Busqueda 
                        OR u.Email LIKE @Busqueda
                        OR u.Nombre + ' ' + u.Apellido LIKE @Busqueda)";
                        parameters.Add("Busqueda", $"%{busqueda}%");
                    }
                     

                    query += " ORDER BY u.UltimoAcceso desc";


                    return await cn.QueryAsync<UsuarioListDto>(query, parameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UsuarioRepository.GetAllAsync: " + ex.Message);
                return null;
            }
            
        }

      
        public async Task<IEnumerable<UsuarioListDto>> GetActivosAsync()
        {
            return await GetAllAsync( );
        }

        public async Task<bool> ExisteEmailAsync(string email, int? excluirId = null)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    var query = "SELECT COUNT(1) FROM [TiendaDB].[dbo].Usuarios WHERE Email = @Email AND Activo = 1";

                    if (excluirId.HasValue)
                        query += " AND Id != @ExcluirId";

                   
                    var count = await cn.ExecuteScalarAsync<int>(query, new { Email = email, ExcluirId = excluirId });
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UsuarioRepository.ExisteEmailAsync: " + ex.Message);
                return false;
            }
           
        }

        public async Task<int> CreateAsync(UsuarioCreateDto dto)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"   INSERT INTO [TiendaDB].[dbo].Usuarios (Nombre, Apellido, Email, PasswordHash, RolId, Activo, FechaCreacion, Telefono)
                        VALUES (@Nombre, @Apellido, @Email, @PasswordHash, @RolId, 1, GETDATE(), @Telefono);
                        SELECT CAST(SCOPE_IDENTITY() as int);"; 
                    return await cn.QuerySingleAsync<int>(query, dto);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UsuarioRepository.CreateAsync: " + ex.Message);
                return 0;
            }
           
        }

        public async Task<bool> UpdateAsync(UsuarioUpdateDto dto)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"   UPDATE [TiendaDB].[dbo].Usuarios   SET Nombre = @Nombre, Apellido = @Apellido,
                        Email = @Email, Telefono =@Telefono,PasswordHash=@PasswordHash,   RolId = @RolId  WHERE Id = @Id AND Activo = 1";

                    
                    var affected = await cn.ExecuteAsync(query, dto);
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UsuarioRepository.UpdateAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"  DELETE FROM [TiendaDB].[dbo].Usuarios WHERE Id = @Id";
                     
                    var affected = await cn.ExecuteAsync(query, new { Id = id });
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UsuarioRepository.DeleteAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<bool> ValidarCredencialesAsync(string email, string passwordHash)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"  SELECT COUNT(1)   FROM [TiendaDB].[dbo].Usuarios 
                         WHERE Email = @Email   AND PasswordHash = @PasswordHash  AND Activo = 1";


                    var count = await cn.ExecuteScalarAsync<int>(query, new { Email = email, PasswordHash = passwordHash });
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UsuarioRepository.ValidarCredencialesAsync: " + ex.Message);
                return false;
            }
            
        }
    }
}
