using Dapper; 
using Ferreteria.DTOs;
using Ferreteria.Entities;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
  
    public interface IProductoRepository
    { 
        Task<Producto> GetByIdAsync(int id);
        Task<ProductoDetailDto> GetDetailByIdAsync(int id);   
        Task<(IEnumerable<ProductoListDto>, int)> GetAllAsync(string search = null, int? categoriaId = null, int? proveedorId =null, int? estado = null, int page = 1, int pageSize = 20);
         
        Task<int> CreateAsync(ProductoCreateDto dto, int usuarioId);
        Task<bool> UpdateAsync(ProductoUpdateDto dto);   
        Task<bool> DeleteAsync(int id);  
        Task<(IEnumerable<ProductoListDto>, int)> GetStockBajoAsync(string search = null, int? categoriaId = null, int? proveedorId = null, int? estado = null, int page = 1, int pageSize = 20);
      
        Task<bool> ExisteCodigoAsync(string codigo, int? excluirId = null); 
        Task<IEnumerable<UnidadMedidaDto>> GetUnidadesMedidaAsync(); 
        Task<bool> TieneMovimientosAsync(int productoId);
        Task<bool> TieneVentasAsync(int productoId); 
        Task<IEnumerable<MovimientoInventarioDto>> GetHistorialMovimientosAsync(int productoId, int? limite = null);
        Task<AjusteStockResponseDto> AjustarStockAsync(AjusteStockDto dto, int usuarioId);
    }

    public class ProductoRepository : IProductoRepository
    {
       
        private readonly string _connectionString;
        public ProductoRepository(IConfiguration configuration )
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            
        }
        public async Task<IEnumerable<UnidadMedidaDto>> GetUnidadesMedidaAsync()
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    const string query = "SELECT Id, Nombre, Abreviatura FROM [TiendaDB].[dbo].UnidadesMedida WHERE Activo = 1 ORDER BY Nombre";
                   
                    return await cn.QueryAsync<UnidadMedidaDto>(query);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.GetUnidadesMedidaAsync: " + ex.Message);
                return null;
            }
           
        }
        public async Task<Producto> GetByIdAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @" SELECT p.*, um.Nombre as UnidadMedidaNombre, um.Abreviatura, c.Nombre as catNombre, r.nombre as Proveedor
                    FROM [TiendaDB].[dbo].Productos p  LEFT JOIN [TiendaDB].[dbo].UnidadesMedida um ON p.UnidadMedidaId = um.Id
                    LEFT JOIN [TiendaDB].[dbo].Categorias c ON p.CategoriaId = c.Id LEFT JOIN [TiendaDB].[dbo].Proveedor r ON r.id_proveedor = p.idProveedor
                    WHERE p.Id = @Id ";

                 
                    return await cn.QueryFirstOrDefaultAsync<Producto>(query, new { Id = id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.GetByIdAsync: " + ex.Message);
                return null;
            }
            
        }

        public async Task<(IEnumerable<ProductoListDto>, int  )> GetAllAsync(string search = null, int? categoriaId = null, int? proveedorId = null, int? estado = null, int page = 1, int pageSize = 20)
        {
            try
            {
                IEnumerable<ProductoListDto> lista = null;
                string squeryCantidad = " select count (1) from [TiendaDB].[dbo].Productos p left join [TiendaDB].[dbo].[Proveedor] pr on (p.idProveedor=pr.id_proveedor)\r\n " +
                    "               LEFT JOIN [TiendaDB].[dbo].Categorias c ON p.CategoriaId = c.Id\r\n              " +
                    "  LEFT JOIN [TiendaDB].[dbo].UnidadesMedida um ON p.UnidadMedidaId = um.Id WHERE ";
                var query = @"
                SELECT ROW_NUMBER() OVER (ORDER BY p.Id desc) AS listorder,  p.Id, p.Codigo, p.Nombre, p.Descripcion ,pr.nombre as NombreProveedor,    c.Nombre as CategoriaNombre, um.Nombre as UnidadMedida, um.Abreviatura as UnidadMedidaAbreviatura,
                    p.PrecioVenta, p.StockActual, p.StockMinimo,  p.UbicacionAlmacen, p.Activo
                FROM [TiendaDB].[dbo].Productos p left join [TiendaDB].[dbo].[Proveedor] pr on (p.idProveedor=pr.id_proveedor)
                LEFT JOIN [TiendaDB].[dbo].Categorias c ON p.CategoriaId = c.Id
                LEFT JOIN [TiendaDB].[dbo].UnidadesMedida um ON p.UnidadMedidaId = um.Id
                WHERE ";

                var parameters = new DynamicParameters();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += " AND (p.Nombre LIKE @Search OR p.Codigo LIKE @Search OR p.Descripcion LIKE @Search)";
                    squeryCantidad += " AND (p.Nombre LIKE @Search OR p.Codigo LIKE @Search OR p.Descripcion LIKE @Search)";
                    parameters.Add("Search", $"%{search}%");
                }

                if (categoriaId.HasValue)
                {
                    query += " AND p.CategoriaId = @CategoriaId";
                    squeryCantidad += " AND p.CategoriaId = @CategoriaId";
                    parameters.Add("CategoriaId", categoriaId.Value);
                }
                if (proveedorId.HasValue)
                {
                    query += " AND p.idProveedor = @idProveedor";
                    squeryCantidad += " AND p.idProveedor = @idProveedor";
                    parameters.Add("idProveedor", proveedorId.Value);
                }
                if (estado.HasValue)
                {
                    query += " AND p.Activo = @Activo";
                    squeryCantidad += " AND p.Activo = @Activo";
                    parameters.Add("Activo", estado.Value);
                }
                parameters.Add("@Pagina", page);
                parameters.Add("@Registros", pageSize);
                int total = 0;
                squeryCantidad = squeryCantidad.Replace("WHERE  AND", "WHERE");
                if (squeryCantidad.EndsWith("WHERE "))
                {
                    squeryCantidad = squeryCantidad.Replace("WHERE","");
                }
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    total= await cn.QueryFirstOrDefaultAsync<int>(squeryCantidad, parameters);
                }
                query += " ORDER BY p.Id desc OFFSET ((@Pagina - 1) * @Registros) ROWS FETCH NEXT @Registros ROWS ONLY ";
                query = query.Replace("WHERE  AND", "WHERE").Replace("WHERE  ORDER BY", "ORDER BY");

                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    lista= await cn.QueryAsync<ProductoListDto>(query, parameters);
                }
                return (lista, total);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAllAsync: {ex.Message}");
                return (null,0);
            }
            
        }
        public async Task<(IEnumerable<ProductoListDto>, int )> GetStockBajoAsync(string search = null, int? categoriaId = null, int? proveedorId = null, int? estado = null, int page = 1, int pageSize = 20)
        {
            try
            {
                IEnumerable<ProductoListDto> lista = null;
                string squeryCantidad = " select count (1) from [TiendaDB].[dbo].Productos p left join [TiendaDB].[dbo].[Proveedor] pr on (p.idProveedor=pr.id_proveedor)\r\n " +
                    "               LEFT JOIN [TiendaDB].[dbo].Categorias c ON p.CategoriaId = c.Id\r\n              " +
                    "  LEFT JOIN [TiendaDB].[dbo].UnidadesMedida um ON p.UnidadMedidaId = um.Id WHERE  p.StockActual <= p.StockMinimo";
                string query = @"
                SELECT ROW_NUMBER() OVER (ORDER BY p.Id desc) AS listorder,  p.Id, p.Codigo, p.Nombre, p.Descripcion ,pr.nombre as NombreProveedor,    c.Nombre as CategoriaNombre, um.Nombre as UnidadMedida, um.Abreviatura as UnidadMedidaAbreviatura,
                    p.PrecioVenta, p.StockActual, p.StockMinimo,  p.UbicacionAlmacen, p.Activo
                FROM [TiendaDB].[dbo].Productos p left join [TiendaDB].[dbo].[Proveedor] pr on (p.idProveedor=pr.id_proveedor)
                LEFT JOIN [TiendaDB].[dbo].Categorias c ON p.CategoriaId = c.Id
                LEFT JOIN [TiendaDB].[dbo].UnidadesMedida um ON p.UnidadMedidaId = um.Id
                WHERE  p.StockActual <= p.StockMinimo ";
                //p.Activo = 1 AND
                var parameters = new DynamicParameters();
                
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query += " AND (p.Nombre LIKE @Search OR p.Codigo LIKE @Search OR p.Descripcion LIKE @Search)";
                    squeryCantidad += " AND (p.Nombre LIKE @Search OR p.Codigo LIKE @Search OR p.Descripcion LIKE @Search)";
                    parameters.Add("Search", $"%{search}%");
                }

                if (categoriaId.HasValue)
                {
                    query += " AND p.CategoriaId = @CategoriaId";
                    squeryCantidad += " AND p.CategoriaId = @CategoriaId";
                    parameters.Add("CategoriaId", categoriaId.Value);
                }
                if (proveedorId.HasValue)
                {
                    query += " AND p.idProveedor = @idProveedor";
                    squeryCantidad += " AND p.idProveedor = @idProveedor";
                    parameters.Add("idProveedor", proveedorId.Value);
                }
                if (estado.HasValue)
                {
                    query += " AND p.Activo = @Activo";
                    squeryCantidad += " AND p.Activo = @Activo";
                    parameters.Add("Activo", estado.Value);
                }
                parameters.Add("@Pagina", page);
                parameters.Add("@Registros", pageSize);
                int total = 0;
                squeryCantidad = squeryCantidad.Replace("WHERE  AND", "WHERE");
                if (squeryCantidad.EndsWith("WHERE "))
                {
                    squeryCantidad = squeryCantidad.Replace("WHERE", "");
                }
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    total = await cn.QueryFirstOrDefaultAsync<int>(squeryCantidad, parameters);
                }
                query += " ORDER BY p.Id desc OFFSET ((@Pagina - 1) * @Registros) ROWS FETCH NEXT @Registros ROWS ONLY ";
               
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    lista = await cn.QueryAsync<ProductoListDto>(query, parameters);
                }
                return (lista, total);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ProductoRepository.GetStockBajoAsync: {ex.Message}");
                return (null, 0);
            }
           
        }

        public async Task<int> CreateAsync(ProductoCreateDto dto, int usuarioId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    const string query = @" INSERT INTO [TiendaDB].[dbo].Productos (Codigo, Nombre, Descripcion, CategoriaId, UnidadMedidaId, 
                    PrecioCompra, PrecioVenta, StockMinimo, StockActual, UbicacionAlmacen,    UsuarioCreacionId, idProveedor)
                    VALUES (@Codigo, @Nombre, @Descripcion, @CategoriaId, @UnidadMedidaId, @PrecioCompra, @PrecioVenta, @StockMinimo, @StockInicial, @UbicacionAlmacen, @UsuarioCreacionId,@idProveedor);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                    
                    var id = await cn.QuerySingleAsync<int>(query, new
                    {
                        dto.Codigo,
                        dto.Nombre,
                        dto.Descripcion,
                        dto.CategoriaId,
                        dto.UnidadMedidaId,
                        dto.PrecioCompra,
                        dto.PrecioVenta,
                        dto.StockMinimo,
                        StockInicial = dto.StockInicial,
                        dto.UbicacionAlmacen,
                        UsuarioCreacionId = usuarioId,
                        dto.idProveedor
                    });

                    // Registrar movimiento inicial
                    if (dto.StockInicial > 0)
                    {
                        const string movQuery = @"  INSERT INTO [TiendaDB].[dbo].MovimientosInventario   (ProductoId, TipoMovimiento, Cantidad, StockAnterior, StockNuevo,  Referencia, Observaciones, UsuarioId)
                        VALUES (@ProductoId, 'ENTRADA', @Cantidad, 0, @StockNuevo,  'INVENTARIO_INICIAL', 'Stock inicial del producto', @UsuarioId)";

                        await cn.ExecuteAsync(movQuery, new
                        {
                            ProductoId = id,
                            Cantidad = dto.StockInicial,
                            StockNuevo = dto.StockInicial,
                            UsuarioId = usuarioId
                        });
                    }

                    return id;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.CreateAsync: " + ex.Message);
                return 0;
            }
           
        }
         

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = "UPDATE [TiendaDB].[dbo].Productos SET Activo = 0 WHERE Id = @Id";
                     
                    var affected = await cn.ExecuteAsync(query, new { Id = id });
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.DeleteAsync: " + ex.Message);
                return false;
            }
            
        }

        
        
        public async Task<ProductoDetailDto> GetDetailByIdAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    const string query = @"  SELECT   p.Id, p.Codigo, p.Nombre, p.Descripcion,  p.CategoriaId, c.Nombre as CategoriaNombre,
                        p.UnidadMedidaId, um.Nombre as UnidadMedidaNombre, um.Abreviatura as UnidadMedidaAbreviatura,
                        p.PrecioCompra, p.PrecioVenta, p.StockMinimo, p.StockActual,  p.UbicacionAlmacen, p.IdProveedor, pr.Nombre as ProveedorNombre,
                        p.Activo, p.FechaCreacion,  u.Nombre + ' ' + u.Apellido as UsuarioCreacion
                    FROM [TiendaDB].[dbo].Productos p  LEFT JOIN [TiendaDB].[dbo].Categorias c ON p.CategoriaId = c.Id
                    LEFT JOIN [TiendaDB].[dbo].UnidadesMedida um ON p.UnidadMedidaId = um.Id
                    LEFT JOIN [TiendaDB].[dbo].Proveedor pr ON p.IdProveedor = pr.id_proveedor
                    LEFT JOIN [TiendaDB].[dbo].Usuarios u ON p.UsuarioCreacionId = u.Id
                    WHERE p.Id = @Id";

                    
                    return await cn.QueryFirstOrDefaultAsync<ProductoDetailDto>(query, new { Id = id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.GetDetailByIdAsync: " + ex.Message);
                return null;
            }
           
        }

        public async Task<bool> UpdateAsync(ProductoUpdateDto dto)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"  UPDATE [TiendaDB].[dbo].Productos SET Codigo = @Codigo,
                    Nombre = @Nombre, Descripcion = @Descripcion, CategoriaId = @CategoriaId,  UnidadMedidaId = @UnidadMedidaId,
                    PrecioCompra = @PrecioCompra,  PrecioVenta = @PrecioVenta,  StockMinimo = @StockMinimo,  UbicacionAlmacen = @UbicacionAlmacen,
                    IdProveedor = @IdProveedor,  Activo = @Activo
                     WHERE Id = @Id";


                    var affected = await cn.ExecuteAsync(query, dto);
                    return affected > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.UpdateAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<bool> ExisteCodigoAsync(string codigo, int? excluirId = null)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    var query = "SELECT COUNT(1) FROM [TiendaDB].[dbo].Productos WHERE Codigo = @Codigo AND Activo = 1";
                    if (excluirId.HasValue)
                        query += " AND Id != @ExcluirId";


                    var count = await cn.ExecuteScalarAsync<int>(query, new { Codigo = codigo, ExcluirId = excluirId });
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.ExisteCodigoAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<bool> TieneMovimientosAsync(int productoId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = "SELECT COUNT(1) FROM [TiendaDB].[dbo].MovimientosInventario WHERE ProductoId = @ProductoId";
                  
                    return await cn.ExecuteScalarAsync<int>(query, new { ProductoId = productoId }) > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.TieneMovimientosAsync: " + ex.Message);
                return false;
            }
           
        }

        public async Task<bool> TieneVentasAsync(int productoId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @" SELECT COUNT(1) FROM [TiendaDB].[dbo].VentaDetalles vd  INNER JOIN [TiendaDB].[dbo].Ventas v ON vd.VentaId = v.Id
                        WHERE vd.ProductoId = @ProductoId AND v.Estado = 'COMPLETADA'";
                    
                    return await cn.ExecuteScalarAsync<int>(query, new { ProductoId = productoId }) > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.TieneVentasAsync: " + ex.Message);
                return false;
            }
           
        }

        public async Task<IEnumerable<MovimientoInventarioDto>> GetHistorialMovimientosAsync(int productoId, int? limite = null)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    var query = @" SELECT   m.Id, m.ProductoId, m.TipoMovimiento, m.Cantidad,   m.StockAnterior, m.StockNuevo, m.Referencia, m.Observaciones,
                        m.FechaMovimiento,  u.Nombre + ' ' + u.Apellido as UsuarioNombre
                    FROM [TiendaDB].[dbo].MovimientosInventario m
                    INNER JOIN [TiendaDB].[dbo].Usuarios u ON m.UsuarioId = u.Id
                    WHERE m.ProductoId = @ProductoId
                    ORDER BY m.FechaMovimiento DESC";

                    if (limite.HasValue)
                        query += $" OFFSET 0 ROWS FETCH NEXT {limite.Value} ROWS ONLY";

                    
                    return await cn.QueryAsync<MovimientoInventarioDto>(query, new { ProductoId = productoId });

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.GetHistorialMovimientosAsync: " + ex.Message);
                return null;
            }
        }

        public async Task<AjusteStockResponseDto> AjustarStockAsync(AjusteStockDto dto, int usuarioId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {
                        // Obtener stock actual
                        const string getStock = "SELECT StockActual FROM [TiendaDB].[dbo].Productos WHERE Id = @Id";
                        var stockActual = await cn.QuerySingleAsync<decimal>(getStock, new { Id = dto.ProductoId }, transaction);

                        // Validar que el stock calculado coincida
                        if (stockActual != dto.StockAnterior)
                        {
                            return new AjusteStockResponseDto
                            {
                                Success = false,
                                Message = "El stock ha cambiado. Recargue la página e intente nuevamente.",
                                StockAnterior = stockActual,
                                StockNuevo = stockActual
                            };
                        }

                        // Actualizar producto
                        const string updateProd = "UPDATE [TiendaDB].[dbo].Productos SET StockActual = @NuevoStock WHERE Id = @Id";
                        await cn.ExecuteAsync(updateProd, new { Id = dto.ProductoId, NuevoStock = dto.NuevoStock }, transaction);

                        // Registrar movimiento
                        const string insertMov = @"   INSERT INTO [TiendaDB].[dbo].MovimientosInventario 
                                (ProductoId, TipoMovimiento, Cantidad, StockAnterior, StockNuevo,   Referencia, Observaciones, UsuarioId, FechaMovimiento)
                            VALUES    (@ProductoId, @TipoMovimiento, ABS(@Cantidad), @StockAnterior, @StockNuevo,   @Referencia, @Observaciones, @UsuarioId, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() as int);";

                        var cantidad = dto.Diferencia;
                        var tipoMov = cantidad >= 0 ? (cantidad == 0 ? "AJUSTE" : "ENTRADA") : "SALIDA";

                        await cn.ExecuteAsync(insertMov, new
                        {
                            dto.ProductoId,
                            TipoMovimiento = tipoMov,
                            Cantidad = cantidad,
                            dto.StockAnterior,
                            StockNuevo = dto.NuevoStock,
                            Referencia = "AJUSTE_MANUAL",
                            Observaciones = dto.Motivo,
                            UsuarioId = usuarioId
                        }, transaction);

                        transaction.Commit();

                        return new AjusteStockResponseDto
                        {
                            Success = true,
                            Message = "Stock ajustado correctamente",
                            StockAnterior = dto.StockAnterior,
                            StockNuevo = dto.NuevoStock,
                            FechaMovimiento = DateTime.Now
                        };
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ProductoRepository.AjustarStockAsync: " + ex.Message);
                return null;
            }
             
            
        }
    }
}
