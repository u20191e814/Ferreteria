using Dapper; 
using Ferreteria.Entities;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    
    public interface IClienteRepository
    {
        
        Task<Cliente> GetByIdAsync(int id);
        
        Task<IEnumerable<Cliente>> GetAllAsync(string busqueda = null, string tipoDocumento = null, bool? esFrecuente = null);
        Task<int> CreateAsync(Cliente cliente);
        Task<bool> UpdateAsync(Cliente cliente);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExisteDocumentoAsync(string tipoDocumento, string numeroDocumento, int? excluirId = null);
        Task<IEnumerable<Cliente>> BuscarPorTextoAsync(string texto);
    }

    public class ClienteRepository : IClienteRepository
    {
        
        private readonly string _connectionString;
        public ClienteRepository(IConfiguration configuration )
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            
        }

        public async Task<Cliente> GetByIdAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new (_connectionString))
                {
                    string query = @"  SELECT * FROM [TiendaDB].[dbo].Clientes  WHERE Id = @Id AND Activo = 1";

                    return await cn.QueryFirstOrDefaultAsync<Cliente>(query, new { Id = id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClienteRepository.GetByIdAsync: "+ ex.Message);
                return null;
            } 
        }

        

        public async Task<IEnumerable<Cliente>> GetAllAsync(string busqueda = null, string tipoDocumento = null, bool? esFrecuente = null)
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var query = @" SELECT * FROM  [TiendaDB].[dbo].Clientes  WHERE 1=1";

                    var parameters = new DynamicParameters();

                    if (!string.IsNullOrWhiteSpace(busqueda))
                    {
                        query += @" AND (  NombreCompleto LIKE @Busqueda OR  NumeroDocumento LIKE @Busqueda OR   Email LIKE @Busqueda OR Telefono LIKE @Busqueda      )";
                        parameters.Add("Busqueda", $"%{busqueda}%");
                    }

                    if (!string.IsNullOrEmpty(tipoDocumento))
                    {
                        query += " AND TipoDocumento = @TipoDocumento";
                        parameters.Add("TipoDocumento", tipoDocumento);
                    }

                    if (esFrecuente.HasValue)
                    {
                        query += " AND EsFrecuente = @EsFrecuente";
                        parameters.Add("EsFrecuente", esFrecuente.Value);
                    }

                    query += " AND Activo = 1 ORDER BY Id desc ";

                  
                    return await cn.QueryAsync<Cliente>(query, parameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClienteRepository.GetAllAsync: " + ex.Message);
                return null;
            }
            
        }

        public async Task<IEnumerable<Cliente>> BuscarPorTextoAsync(string texto)
        {
            
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    if (string.IsNullOrWhiteSpace(texto) || texto.Length < 3)
                        return new List<Cliente>();

                    string query = @"  SELECT TOP 20 * FROM [TiendaDB].[dbo].Clientes  
                     WHERE ( NombreCompleto LIKE @Texto OR   NumeroDocumento LIKE @Texto OR Email LIKE @Texto )
                    AND Activo = 1
                    ORDER BY 
                    CASE 
                        WHEN NombreCompleto LIKE @TextoExacto THEN 0
                        WHEN NumeroDocumento LIKE @TextoExacto THEN 1
                        ELSE 2
                    END,
                    NombreCompleto";

                    
                    return await cn.QueryAsync<Cliente>(query, new
                    {
                        Texto = $"%{texto}%",
                        TextoExacto = $"{texto}%"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClienteRepository.BuscarPorTextoAsync: " + ex.Message);
                return null;
            }
        }

        public async Task<int> CreateAsync(Cliente cliente)
        {
            
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @" INSERT INTO [TiendaDB].[dbo].Clientes ( TipoDocumento, NumeroDocumento, NombreCompleto, Direccion, 
                    Telefono, Email, EsFrecuente, DescuentoPreferencial, Activo, FechaCreacion )  
                    VALUES ( @TipoDocumento, @NumeroDocumento, @NombreCompleto, @Direccion,
                    @Telefono, @Email, @EsFrecuente, @DescuentoPreferencial, 1, GETDATE() );
                    SELECT CAST(SCOPE_IDENTITY() as int);"; 

                    var id = await cn.QuerySingleAsync<int>(query, cliente);
                    return id;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClienteRepository.CreateAsync: " + ex.Message);
                return 0;
            }
        }

        public async Task<bool> UpdateAsync(Cliente cliente)
        {
            
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = @" UPDATE [TiendaDB].[dbo].Clientes SET
                    NombreCompleto = @NombreCompleto, Direccion = @Direccion,  Telefono = @Telefono,
                    Email = @Email,  EsFrecuente = @EsFrecuente,  DescuentoPreferencial = @DescuentoPreferencial,
                    Activo = @Activo
                    WHERE Id = @Id";

                    var affected = await cn.ExecuteAsync(query, cliente);
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClienteRepository.UpdateAsync: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        { 
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    string query = "delete from [TiendaDB].[dbo].[Clientes]  WHERE Id = @Id"; 
                    var affected = await cn.ExecuteAsync(query, new { Id = id });
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClienteRepository.DeleteAsync: " + ex.Message);
                return false;
            }
        }

        public async Task<bool> ExisteDocumentoAsync(string tipoDocumento, string numeroDocumento, int? excluirId = null)
        {
           
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var query = @"  SELECT COUNT(1) FROM [TiendaDB].[dbo].Clientes 
                    WHERE TipoDocumento = @TipoDocumento  AND NumeroDocumento = @NumeroDocumento  AND Activo = 1";

                    if (excluirId.HasValue)
                    {
                        query += " AND Id != @ExcluirId";
                    }
                    
                    var count = await cn.ExecuteScalarAsync<int>(query, new
                    {
                        TipoDocumento = tipoDocumento,
                        NumeroDocumento = numeroDocumento,
                        ExcluirId = excluirId
                    });

                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ClienteRepository.ExisteDocumentoAsync: " + ex.Message);
                return false;
            }
        }
    }
}
