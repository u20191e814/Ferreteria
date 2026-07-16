using Dapper; 
using Ferreteria.DTOs;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    public interface IVentaRepository
    {
        Task<int> CreateAsync(VentaCreateDto dto, int usuarioId);
        Task<(IEnumerable<VentaListDto>, int )> GetAllAsync(DateTime fechaInicio , DateTime fechaFin ,string? metodoPago = null, string? estado = null, string? clienteBusqueda = null, int? usuarioId = null, int page = 1, int pageSize = 20, string? tipoComprobante =null);
        Task<ReporteVentasDto> GetReporteAsync(DateTime fechaInicio, DateTime fechaFin, string agrupacion, int? usuarioId = null);

        Task<VentaDetailDto> GetByIdAsync(int id);
        Task<bool> AnularAsync(int id, int usuarioId, string motivo);
        
        Task<IEnumerable<VentaAnulacionDto>> GetHistorialAnulacionesAsync(int ventaId);
        Task<ClienteEstadisticasDto> GetEstadisticasClienteAsync(int clienteId);
        Task<(IEnumerable<VentaListDto>, int)> GetVentasPorClienteAsync(int clienteId, int page = 1, int pageSize = 10);

    }


    public class VentaRepository : IVentaRepository
    {
       
        private readonly string _connectionString;
        public VentaRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
           
        }

        public async Task<int> CreateAsync(VentaCreateDto dto, int usuarioId)
        {
            try
            {
                string inicial = "B-";
                if (dto.TipoComprobante == "PROFORMA")
                {
                     dto.IncluyeIGV = false;
                    inicial = "P-"; 
                }
                else if (dto.TipoComprobante == "BOLETA")
                {
                    dto.IncluyeIGV = true;
                    inicial = "B-";
                }
                else if (dto.TipoComprobante == "FACTURA")
                {
                    dto.IncluyeIGV = true;
                    inicial = "F-";
                }
                
                using (SqlConnection cn = new(_connectionString))
                {
                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {
                        // Generar número de factura
                        string getNumero = $@"  SELECT '{inicial}'   + 
                          RIGHT('00000000' + CAST(ISNULL(MAX(CAST(SUBSTRING(NumeroFactura, 3, 8) as INT)), 0) + 1 as VARCHAR), 8)
                           FROM [TiendaDB].[dbo].Ventas  WHERE TipoComprobante = '{dto.TipoComprobante}'";

                        var numeroFactura = await cn.QuerySingleAsync<string>(getNumero, transaction: transaction);

                        // Calcular totales y validar stock
                        decimal subTotal = 0;
                        foreach (var det in dto.Detalles)
                        {
                            det.Descuento = (det.PrecioUnitario * det.Cantidad) * (dto.PorcentajeDescuentoClient / 100);

                            subTotal += (det.PrecioUnitario * det.Cantidad) - det.Descuento;
                        }
                        decimal nuevoimpuesto = 0.18m;
                        if (!dto.IncluyeIGV)
                        { 
                            nuevoimpuesto = 0.0m;
                        }
                        var impuestos = subTotal * nuevoimpuesto;
                        var descuentoTotal = dto.Detalles.Sum(j => j.Descuento);
                        var total = (subTotal + impuestos);

                        // Insertar venta
                        const string insertVenta = @" INSERT INTO [TiendaDB].[dbo].Ventas (NumeroFactura, PedidoId, ClienteId, UsuarioVendedorId,
                        SubTotal, Descuento, Impuestos, Total, MetodoPago, Observaciones, TipoComprobante)
                    VALUES (@NumeroFactura, @PedidoId, @ClienteId, @UsuarioVendedorId,  @SubTotal, @Descuento, @Impuestos, @Total, @MetodoPago, @Observaciones, @TipoComprobante);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                        var ventaId = await cn.QuerySingleAsync<int>(insertVenta, new
                        {
                            NumeroFactura = numeroFactura,
                           
                            dto.PedidoId,
                            dto.ClienteId,
                            UsuarioVendedorId = usuarioId,
                            SubTotal = subTotal,
                            Impuestos = impuestos,
                            Total = total,
                            Descuento = descuentoTotal,
                            dto.MetodoPago,
                            dto.Observaciones,
                            dto.TipoComprobante,
                        }, transaction);

                        // Insertar detalles y actualizar stock
                        foreach (var det in dto.Detalles)
                        {
                            const string getProd = "SELECT PrecioVenta, UnidadMedidaId, StockActual FROM [TiendaDB].[dbo].Productos WHERE Id = @Id";
                            var prod = await cn.QuerySingleAsync<dynamic>(getProd, new { Id = det.ProductoId }, transaction);

                            // Insertar detalle
                            const string insertDet = @" INSERT INTO [TiendaDB].[dbo].VentaDetalles (VentaId, ProductoId, Cantidad, PrecioUnitario, 
                            UnidadMedidaId, Descuento, SubTotal)
                        VALUES (@VentaId, @ProductoId, @Cantidad, @PrecioUnitario,   @UnidadMedidaId, @Descuento, @SubTotal)";

                            await cn.ExecuteAsync(insertDet, new
                            {
                                VentaId = ventaId,
                                det.ProductoId,
                                det.Cantidad,
                                PrecioUnitario = (decimal)prod.PrecioVenta,
                                UnidadMedidaId = (int)prod.UnidadMedidaId,
                                det.Descuento,
                                SubTotal = ((decimal)prod.PrecioVenta * det.Cantidad) - det.Descuento
                            }, transaction);

                            // Actualizar stock
                            var nuevoStock = (decimal)prod.StockActual - det.Cantidad;
                            const string updateStock = "UPDATE [TiendaDB].[dbo].Productos SET StockActual = @NuevoStock WHERE Id = @Id";
                            await cn.ExecuteAsync(updateStock, new { Id = det.ProductoId, NuevoStock = nuevoStock }, transaction);

                            // Registrar movimiento
                            const string insertMov = @" INSERT INTO [TiendaDB].[dbo].MovimientosInventario (ProductoId, TipoMovimiento, Cantidad, 
                            StockAnterior, StockNuevo, Referencia, UsuarioId)
                        VALUES (@ProductoId, 'SALIDA', @Cantidad, @StockAnterior, @StockNuevo,   @Referencia, @UsuarioId)";

                            await cn.ExecuteAsync(insertMov, new
                            {
                                det.ProductoId,
                                det.Cantidad,
                                StockAnterior = (decimal)prod.StockActual,
                                StockNuevo = nuevoStock,
                                Referencia = $"VENTA-{numeroFactura}",
                                UsuarioId = usuarioId
                            }, transaction);
                        }

                        // Si viene de pedido, actualizar estado
                        if (dto.PedidoId.HasValue)
                        {
                            string updatePedido = "UPDATE [TiendaDB].[dbo].Pedidos SET Estado = 'FINALIZADO' WHERE Id = @Id";
                            await cn.ExecuteAsync(updatePedido, new { Id = dto.PedidoId.Value }, transaction);
                        }

                        transaction.Commit();
                        return ventaId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.CreateAsync: " + ex.Message);
                return 0;
            }
             
           
        }

        public async Task<(IEnumerable<VentaListDto>, int )> GetAllAsync(DateTime fechaInicio , DateTime fechaFin , string? metodoPago = null, string? estado = null, string? clienteBusqueda = null, int? usuarioId = null, int page = 1, int pageSize = 20 ,  string? tipoComprobante = null)
        {
            try
            {
                IEnumerable<VentaListDto> lista =null;
                var query = @"
                SELECT ROW_NUMBER() OVER (ORDER BY v.Id desc) AS listorder,
                    v.Id, v.NumeroFactura, v.FechaVenta, v.Total, v.MetodoPago, v.Estado,v.TipoComprobante,
                    c.NombreCompleto as ClienteNombre,
                    u.Nombre + ' ' + u.Apellido as VendedorNombre
                FROM [TiendaDB].[dbo].Ventas v
                INNER JOIN [TiendaDB].[dbo].Clientes c ON v.ClienteId = c.Id
                INNER JOIN [TiendaDB].[dbo].Usuarios u ON v.UsuarioVendedorId = u.Id
                WHERE v.FechaVenta between @FechaInicio  and @FechaFin ";

                string squeryCantidad = @" SELECT COUNT(1)
                FROM [TiendaDB].[dbo].Ventas v
                INNER JOIN [TiendaDB].[dbo].Clientes c ON v.ClienteId = c.Id  INNER JOIN [TiendaDB].[dbo].Usuarios u ON v.UsuarioVendedorId = u.Id
                WHERE v.FechaVenta between @FechaInicio  and @FechaFin ";
                //v.Estado = 'COMPLETADA'
                var parameters = new DynamicParameters();
                parameters.Add("FechaInicio", fechaInicio);
                parameters.Add("FechaFin", fechaFin.AddSeconds(86399));


                if (usuarioId.HasValue)
                {
                    query += " and v.UsuarioVendedorId = @UsuarioId";
                    squeryCantidad += " and v.UsuarioVendedorId = @UsuarioId";
                    parameters.Add("UsuarioId", usuarioId.Value);
                }
                if (!string.IsNullOrEmpty(metodoPago))
                {
                    query += string.Format(" and v.MetodoPago = '{0}'", metodoPago);
                    squeryCantidad += string.Format(" and v.MetodoPago = '{0}'", metodoPago);
                }
                if (!string.IsNullOrEmpty(estado))
                {
                    query += string.Format(" and v.Estado = '{0}'", estado);
                    squeryCantidad += string.Format(" and v.Estado = '{0}'", estado);
                }
                if (!string.IsNullOrEmpty(clienteBusqueda))
                {
                    query += string.Format(" and ( c.NombreCompleto like '%{0}%' or  c.NumeroDocumento like '%{0}%' ) ", clienteBusqueda);
                    squeryCantidad += string.Format(" and ( c.NombreCompleto like '%{0}%' or  c.NumeroDocumento like '%{0}%' ) ", clienteBusqueda);
                }
                if (!string.IsNullOrEmpty(tipoComprobante))
                {
                    query += string.Format(" and v.TipoComprobante = '{0}'", tipoComprobante);
                    squeryCantidad += string.Format(" and v.TipoComprobante = '{0}'", tipoComprobante);
                }
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

                query += " ORDER BY v.Id desc OFFSET ((@Pagina - 1) * @Registros) ROWS FETCH NEXT @Registros ROWS ONLY ";
                parameters.Add("@Pagina", page);
                parameters.Add("@Registros", pageSize);

                using (SqlConnection cn = new SqlConnection (_connectionString))
                {
                    lista = await cn.QueryAsync<VentaListDto>(query, parameters);
                }
                return (lista, total);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ventas.GetAllAsync: "+ex.Message);
                return (null, 0);
            }
           
        }

        public async Task<ReporteVentasDto> GetReporteAsync(DateTime fechaInicio, DateTime fechaFin, string agrupacion, int? usuarioId = null)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    var groupBy = agrupacion switch
                    {
                        "DIARIA" => "CAST(v.FechaVenta as DATE)",
                        "SEMANAL" => "DATEPART(WEEK, v.FechaVenta)",
                        "MENSUAL" => "CAST(YEAR(v.FechaVenta) as VARCHAR) + '-' + CAST(MONTH(v.FechaVenta) as VARCHAR)",
                        _ => "CAST(v.FechaVenta as DATE)"
                    };

                    var query = $@"  SELECT    {groupBy} as Periodo,  COUNT(*) as CantidadVentas,
                    SUM(v.Total) as TotalVentas, SUM(v.Descuento) as TotalDescuentos, AVG(v.Total) as PromedioVenta
                    FROM [TiendaDB].[dbo].Ventas v
                    WHERE v.FechaVenta BETWEEN @FechaInicio AND @FechaFin
                    AND v.Estado = 'COMPLETADA'";

                    if (usuarioId.HasValue)
                        query += " AND v.UsuarioVendedorId = @UsuarioId";

                    query += $" GROUP BY {groupBy} ORDER BY {groupBy}";


                    var result = await cn.QueryAsync<dynamic>(query, new { FechaInicio = fechaInicio, FechaFin = fechaFin, UsuarioId = usuarioId });

                    if (!result.Any())
                        return new ReporteVentasDto { Fecha = fechaInicio, Periodo = agrupacion };
                    // Simplificado para el ejemplo
                    return new ReporteVentasDto
                    {
                        Fecha = fechaInicio,
                        Periodo = agrupacion,
                        CantidadVentas = result.Sum(r => (int)r.CantidadVentas),
                        TotalVentas = result.Sum(r => (decimal)r.TotalVentas),
                        TotalDescuentos = result.Sum(r => (decimal)r.TotalDescuentos),
                        PromedioVenta = result.Average(r => (decimal)r.PromedioVenta)
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.GetReporteAsync: " + ex.Message);
                return null;
            }
            
        }
        public async Task<VentaDetailDto> GetByIdAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"  SELECT  v.Id, v.NumeroFactura, v.FechaVenta, v.Total, v.MetodoPago, v.Estado,v.TipoComprobante ,
                        v.SubTotal, v.Impuestos, v.Descuento, v.Observaciones,  c.NombreCompleto as ClienteNombre, 
                        c.Direccion as ClienteDireccion,  c.Telefono as ClienteTelefono,  c.Email as ClienteEmail,
                        c.NumeroDocumento as ClienteDocumento,  u.Nombre + ' ' + u.Apellido as VendedorNombre
                        FROM [TiendaDB].[dbo].Ventas v
                        INNER JOIN [TiendaDB].[dbo].Clientes c ON v.ClienteId = c.Id
                        INNER JOIN [TiendaDB].[dbo].Usuarios u ON v.UsuarioVendedorId = u.Id
                        WHERE v.Id = @Id";


                    var venta = await cn.QueryFirstOrDefaultAsync<VentaDetailDto>(query, new { Id = id });

                    if (venta != null)
                    {
                        venta.Detalles = (await GetDetallesVentaAsync(id)).ToList();
                    }

                    return venta;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.GetByIdAsync: " + ex.Message);
                return null;
            }
            
        }

        private async Task<IEnumerable<VentaDetalleDto>> GetDetallesVentaAsync(int ventaId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @" SELECT    vd.Id, vd.ProductoId, p.Nombre as ProductoNombre, p.Codigo as ProductoCodigo,
                        vd.Cantidad, um.Nombre as UnidadMedida, um.Abreviatura as AbreviaturaUnidad, vd.PrecioUnitario, vd.Descuento, vd.SubTotal
                    FROM [TiendaDB].[dbo].VentaDetalles vd INNER JOIN [TiendaDB].[dbo].Productos p ON vd.ProductoId = p.Id
                    INNER JOIN [TiendaDB].[dbo].UnidadesMedida um ON vd.UnidadMedidaId = um.Id
                    WHERE vd.VentaId = @VentaId";

                   
                    return await cn.QueryAsync<VentaDetalleDto>(query, new { VentaId = ventaId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.GetDetallesVentaAsync: " + ex.Message);
                return null;
            }
            
        }



         
        public async Task<bool> AnularAsync(int ventaId, int usuarioId, string motivo)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                   
                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {

                        string getDetalles = @" SELECT vd.ProductoId, vd.Cantidad, p.Nombre as ProductoNombre, p.StockActual
                            FROM [TiendaDB].[dbo].VentaDetalles vd  INNER JOIN [TiendaDB].[dbo].Productos p ON vd.ProductoId = p.Id
                            WHERE vd.VentaId = @VentaId";

                        var detalles = await cn.QueryAsync<dynamic>(getDetalles, new { VentaId = ventaId }, transaction);

                        // Revertir stock por cada producto
                        var stockRevertido = new List<ProductoStockRevertidoDto>();

                        foreach (var det in detalles)
                        {
                            var stockAnterior = (decimal)det.StockActual;
                            var cantidadRevertir = (decimal)det.Cantidad;
                            var stockNuevo = stockAnterior + cantidadRevertir;

                            // Actualizar stock
                            string updateStock = @"   UPDATE [TiendaDB].[dbo].Productos  SET StockActual = @NuevoStock WHERE Id = @ProductoId";

                            await cn.ExecuteAsync(updateStock, new { ProductoId = det.ProductoId, NuevoStock = stockNuevo }, transaction);

                            // Registrar movimiento de inventario (entrada por anulación)
                            string insertMovimiento = @" INSERT INTO [TiendaDB].[dbo].MovimientosInventario  (ProductoId, TipoMovimiento, Cantidad, StockAnterior, StockNuevo,    Referencia, Observaciones, UsuarioId, FechaMovimiento)
                            VALUES (@ProductoId, 'ENTRADA_ANULACION', @Cantidad, @StockAnterior, @StockNuevo,   @Referencia, @Observaciones, @UsuarioId, GETDATE())";

                            await cn.ExecuteAsync(insertMovimiento, new
                            {
                                ProductoId = det.ProductoId,
                                Cantidad = cantidadRevertir,
                                StockAnterior = stockAnterior,
                                StockNuevo = stockNuevo,
                                Referencia = $"ANULACION-VENTA-{ventaId}",
                                Observaciones = $"Revertido por anulación de venta. Motivo: {motivo}",
                                UsuarioId = usuarioId
                            }, transaction);

                            stockRevertido.Add(new ProductoStockRevertidoDto
                            {
                                ProductoId = det.ProductoId,
                                ProductoNombre = det.ProductoNombre,
                                CantidadRevertida = cantidadRevertir,
                                StockAnterior = stockAnterior,
                                StockNuevo = stockNuevo
                            });
                        }

                        // Actualizar estado de la venta
                        string updateVenta = @"  UPDATE [TiendaDB].[dbo].Ventas  SET Estado = 'ANULADA',  FechaAnulacion= GETDATE(),
                        UsuarioAnulacionId = @UsuarioId, MotivoAnulacion = @Motivo,  Observaciones = ISNULL(Observaciones, '') + ' [ANULADA: ' + @Motivo + ' - ' + CONVERT(VARCHAR, GETDATE(), 120) + ']'
                         WHERE Id = @VentaId";

                        await cn.ExecuteAsync(updateVenta, new  { VentaId = ventaId,  UsuarioId = usuarioId, Motivo = motivo            }, transaction);

                        // Si la venta venía de un pedido, revertir el pedido a "PROCESO"
                        string checkPedido = @" SELECT PedidoId FROM [TiendaDB].[dbo].Ventas WHERE Id = @VentaId AND PedidoId IS NOT NULL";
                        var pedidoId = await cn.QuerySingleOrDefaultAsync<int?>(checkPedido, new { VentaId = ventaId }, transaction);

                        if (pedidoId.HasValue)
                        {
                            string updatePedido = @"   UPDATE [TiendaDB].[dbo].Pedidos  SET Estado = 'PROCESO',
                            Observaciones = ISNULL(Observaciones, '') + ' [Venta anulada, pedido reactivado - ' + CONVERT(VARCHAR, GETDATE(), 120) + ']'
                            WHERE Id = @PedidoId";

                            await cn.ExecuteAsync(updatePedido, new { PedidoId = pedidoId.Value }, transaction);
                        }



                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.AnularAsync: " + ex.Message);
                return false;
            }
           
        }

        public async Task<IEnumerable<VentaAnulacionDto>> GetHistorialAnulacionesAsync(int ventaId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"  SELECT  v.Id as VentaId,  v.MotivoAnulacion as Motivo,  v.FechaAnulacion,
                        v.UsuarioAnulacionId as UsuarioAnulacionId, u.Nombre + ' ' + u.Apellido as UsuarioAnulacionNombre
                    FROM [TiendaDB].[dbo].Ventas v  INNER JOIN [TiendaDB].[dbo].Usuarios u ON v.UsuarioAnulacionId = u.Id
                    WHERE v.Id = @VentaId AND v.Estado = 'ANULADA'";

                     
                    return await cn.QueryAsync<VentaAnulacionDto>(query, new { VentaId = ventaId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.GetHistorialAnulacionesAsync: " + ex.Message);
                return null;
            }
             
        }
        public async Task<ClienteEstadisticasDto> GetEstadisticasClienteAsync(int clienteId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"
                        SELECT 
                            COUNT(1) AS CantidadVentas,
                            ISNULL(SUM(Total), 0) AS TotalComprado,
                            MAX(FechaVenta) AS UltimaCompra
                        FROM [TiendaDB].[dbo].Ventas
                        WHERE ClienteId = @ClienteId AND Estado = 'COMPLETADA'";

                    var result = await cn.QueryFirstOrDefaultAsync<dynamic>(query, new { ClienteId = clienteId });

                    return new ClienteEstadisticasDto
                    {
                        CantidadVentas = (int)result.CantidadVentas,
                        TotalComprado = (decimal)result.TotalComprado,
                        UltimaCompra = result.UltimaCompra == null ? (DateTime?)null : (DateTime)result.UltimaCompra
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.GetEstadisticasClienteAsync: " + ex.Message);
                return new ClienteEstadisticasDto { CantidadVentas = 0, TotalComprado = 0, UltimaCompra = null };
            }
        }


        public async Task<(IEnumerable<VentaListDto>, int)> GetVentasPorClienteAsync(int clienteId, int page = 1, int pageSize = 10)
        {
            try
            {
                if (page <= 0) page = 1;
                if (pageSize <= 0) pageSize = 10;

                var countQuery = @"SELECT COUNT(1) FROM [TiendaDB].[dbo].Ventas v WHERE v.ClienteId = @ClienteId";
                var query = @"
                    SELECT v.Id, v.NumeroFactura, v.FechaVenta, v.Total, v.MetodoPago, v.Estado, v.TipoComprobante,
                           c.NombreCompleto as ClienteNombre,
                           u.Nombre + ' ' + u.Apellido as VendedorNombre
                    FROM [TiendaDB].[dbo].Ventas v
                    INNER JOIN [TiendaDB].[dbo].Clientes c ON v.ClienteId = c.Id
                    INNER JOIN [TiendaDB].[dbo].Usuarios u ON v.UsuarioVendedorId = u.Id
                    WHERE v.ClienteId = @ClienteId
                    ORDER BY v.FechaVenta DESC
                    OFFSET ((@Page - 1) * @PageSize) ROWS FETCH NEXT @PageSize ROWS ONLY";

                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    var total = await cn.QueryFirstOrDefaultAsync<int>(countQuery, new { ClienteId = clienteId });
                    var items = await cn.QueryAsync<VentaListDto>(query, new { ClienteId = clienteId, Page = page, PageSize = pageSize });
                    return (items, total);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("VentaRepository.GetVentasPorClienteAsync: " + ex.Message);
                return (Enumerable.Empty<VentaListDto>(), 0);
            }
        }
    }
}
