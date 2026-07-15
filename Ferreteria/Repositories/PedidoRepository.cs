using Dapper; 
using Ferreteria.DTOs;
using Ferreteria.Entities;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Ferreteria.Repositories
{
    public interface IPedidoRepository
    {
        Task<PedidoDetailDto> GetByIdAsync(int id);
        Task<IEnumerable<PedidoListDto>> GetAllAsync(string estado = null, int? clienteId = null, DateTime? fechaDesde = null);
        Task<int> CreateAsync(PedidoCreateDto dto, int usuarioId);
        Task<bool> UpdateEstadoAsync(int id, string nuevoEstado, int usuarioId, string observacion = null);
        Task<bool> AgregarProductosAsync(int pedidoId, List<PedidoDetalleCreateDto> detalles);
        Task<bool> DeleteAsync(int id);

        Task<IEnumerable<PedidoDetalleDto>> GetDetallesAsync(int pedidoId);
        
        
        Task<IEnumerable<PedidoListDto>> GetAllAsync(PedidoFiltroDto filtros);

        Task<bool> ExisteVentaDePedidoAsync(int pedidoId);
        Task<VentaResultDto> ConvertirAVentaAsync(int pedidoId, int usuarioId, VentaCreateDto datosVenta);
        Task<bool> ValidarPedidoParaVentaAsync(int pedidoId);
         
    }

    public class PedidoRepository : IPedidoRepository
    {
        
        private readonly string _connectionString;
        public PedidoRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");

        }
        public async Task<bool> ExisteVentaDePedidoAsync(int pedidoId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = "SELECT COUNT(1) FROM [TiendaDB].[dbo].Ventas WHERE PedidoId = @PedidoId";
                  
                    var count = await cn.ExecuteScalarAsync<int>(query, new { PedidoId = pedidoId });
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PedidoRepository.ExisteVentaDePedidoAsync: " + ex.Message);
                return false;
            }
             
        }
        public async Task<bool> ValidarPedidoParaVentaAsync(int pedidoId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @" SELECT Estado, 
                        (SELECT COUNT(1) FROM [TiendaDB].[dbo].Ventas WHERE PedidoId = p.Id) as TieneVenta
                         FROM [TiendaDB].[dbo].Pedidos p WHERE p.Id = @Id";

                     
                    var resultado = await cn.QueryFirstOrDefaultAsync<dynamic>(query, new { Id = pedidoId });

                    if (resultado == null) return false;
                    if (resultado.Estado != "FINALIZADO") return false;
                    if (resultado.TieneVenta > 0) return false;

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PedidoRepository.ValidarPedidoParaVentaAsync: " + ex.Message);
                return false;
            }
            
        }

        public async Task<VentaResultDto> ConvertirAVentaAsync(int pedidoId, int usuarioId, VentaCreateDto datosVenta)
        {

            try
            {
                string inicial = "B-";
                if (datosVenta.TipoComprobante == "PROFORMA")
                {
                    datosVenta.IncluyeIGV = false;
                    inicial = "P-";
                }
                else if (datosVenta.TipoComprobante == "BOLETA")
                {
                    datosVenta.IncluyeIGV = true;
                    inicial = "B-";
                }
                else if (datosVenta.TipoComprobante == "FACTURA")
                {
                    datosVenta.IncluyeIGV = true;
                    inicial = "F-";
                }
                using (SqlConnection cn = new(_connectionString))
                {

                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {
                        // 1. Validar pedido
                        if (!await ValidarPedidoParaVentaAsync(pedidoId))
                            throw new InvalidOperationException("El pedido no está en condiciones de convertirse en venta");

                        // 2. Obtener detalles del pedido
                        var detallesPedido = (await GetDetallesAsync(pedidoId)).ToList();
                         
                        // Generar número de factura
                        string getNumero = $@"  SELECT '{inicial}'  + 
                           RIGHT('00000000' + CAST(ISNULL(MAX(CAST(SUBSTRING(NumeroFactura, 3, 8) as INT)), 0) + 1 as VARCHAR), 8)
                           FROM [TiendaDB].[dbo].Ventas  WHERE TipoComprobante = '{datosVenta.TipoComprobante}'";

                        var numeroFactura = await cn.QuerySingleAsync<string>(getNumero, transaction: transaction);

                        // 4. Crear venta
                        const string insertVenta = @" INSERT INTO [TiendaDB].[dbo].Ventas (
                        NumeroFactura,PedidoId, ClienteId, UsuarioVendedorId, TipoComprobante, 
                        FechaVenta, SubTotal, Impuestos, Total, Observaciones, Estado,MetodoPago )
                    VALUES ( @NumeroFactura, @PedidoId, @ClienteId, @UsuarioVendedorId, @TipoComprobante, 
                        @FechaVenta, @SubTotal, @Impuestos, @Total, @Observaciones, 'COMPLETADA',@MetodoPago );
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                        decimal subTotal = 0;
                        foreach (var det in detallesPedido)
                        {
                            det.Descuento = (det.PrecioUnitario * det.Cantidad) * (datosVenta.PorcentajeDescuentoClient / 100);

                            subTotal += (det.PrecioUnitario * det.Cantidad) - det.Descuento;
                        }
                        decimal nuevoimpuesto = 0.18m;
                        if (!datosVenta.IncluyeIGV)
                        {
                            nuevoimpuesto = 0.0m;
                        }
                        var impuestos = subTotal * nuevoimpuesto;
                        var descuentoTotal = detallesPedido.Sum(j => j.Descuento);
                        var total = (subTotal + impuestos);

                        var ventaId = await cn.QuerySingleAsync<int>(insertVenta, new
                        {
                            NumeroFactura = numeroFactura,
                            PedidoId = pedidoId,
                            ClienteId = datosVenta.ClienteId,
                            UsuarioVendedorId = usuarioId,
                            datosVenta.TipoComprobante,
                            MetodoPago = datosVenta.MetodoPago,
                            FechaVenta = datosVenta.FechaEmision,
                            SubTotal= subTotal,
                            //SubTotal = detallesPedido.Sum(d => d.SubTotal),
                            Impuestos = impuestos,
                            //Impuestos = detallesPedido.Sum(d => d.SubTotal) * 0.18m,
                            //Total = detallesPedido.Sum(d => d.SubTotal) * 1.18m,
                            Total = total,
                            datosVenta.Observaciones
                        }, transaction);

                        // 5. Crear detalles de venta y actualizar stock
                        foreach (var det in detallesPedido)
                        {
                            // Insertar detalle de venta
                            string insertDetalle = @" INSERT INTO [TiendaDB].[dbo].VentaDetalles ( VentaId, ProductoId, Cantidad, PrecioUnitario,UnidadMedidaId, Descuento, SubTotal   )
                            VALUES (@VentaId, @ProductoId, @Cantidad, @PrecioUnitario,@UnidadMedidaId, @Descuento, @SubTotal);";

                            await cn.ExecuteAsync(insertDetalle, new
                            {
                                VentaId = ventaId,
                                det.ProductoId,
                                det.Cantidad,
                                det.PrecioUnitario,
                                det.Descuento,
                                det.SubTotal,
                                UnidadMedidaId = det.UnidadMedidaId
                            }, transaction);

                            // Actualizar stock del producto
                            string updateStock = @"  UPDATE [TiendaDB].[dbo].Productos  SET StockActual = StockActual - @Cantidad WHERE Id = @ProductoId;
                
                            INSERT INTO [TiendaDB].[dbo].MovimientosInventario (  ProductoId, TipoMovimiento, Cantidad, StockAnterior, StockNuevo, Referencia, Observaciones, UsuarioId, FechaMovimiento   )
                            SELECT    @ProductoId, 'SALIDA', @Cantidad, p.StockActual + @Cantidad, p.StockActual,
                                @Referencia, 'Venta generada desde pedido', @UsuarioId, GETDATE()
                            FROM [TiendaDB].[dbo].Productos p  WHERE p.Id = @ProductoId;";

                            await cn.ExecuteAsync(updateStock, new
                            {
                                det.ProductoId,
                                det.Cantidad,
                                Referencia = $"VENTA-{numeroFactura}",
                                UsuarioId = usuarioId
                            }, transaction);
                        }

                        // 6. Marcar pedido como convertido (opcional: cambiar estado o solo referenciar)
                        const string updatePedido = @"   UPDATE [TiendaDB].[dbo].Pedidos 
                         SET Observaciones = ISNULL(Observaciones, '') + ' [Convertido a venta: ' + @Comprobante + ']' WHERE Id = @Id";

                        await cn.ExecuteAsync(updatePedido, new
                        {
                            Id = pedidoId,
                            Comprobante = $"{numeroFactura}"
                        }, transaction);

                        // 7. Registrar en historial de pedidos
                        string historial = @" INSERT INTO [TiendaDB].[dbo].HistorialPedidos  (PedidoId, EstadoAnterior, EstadoNuevo, UsuarioCambioId, Observacion)
                                VALUES (@PedidoId, 'FINALIZADO', 'CONVERTIDO_VENTA', @UsuarioId, @Observacion)";

                        await cn.ExecuteAsync(historial, new
                        {
                            PedidoId = pedidoId,
                            UsuarioId = usuarioId,
                            Observacion = $"Convertido a venta {numeroFactura}"
                        }, transaction);

                        transaction.Commit();

                        return new VentaResultDto
                        {
                            VentaId = ventaId,
                            Comprobante = datosVenta.TipoComprobante,
                            NumeroCompleto = $"{numeroFactura}",
                            Total = total,
                            //Total = detallesPedido.Sum(d => d.SubTotal) * 1.18m,
                            FechaEmision = datosVenta.FechaEmision
                        };
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
                Console.WriteLine("PedidoRepository.ConvertirAVentaAsync: " + ex.Message);
                return null;
            }
            
        }

       
        public async Task<PedidoDetailDto> GetByIdAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @"  SELECT  p.Id, p.NumeroPedido, p.Estado, p.FechaPedido, p.ClienteId,
                    p.FechaEstimadaEntrega, p.FechaFinalizacion, p.SubTotal, p.Descuento, p.Impuestos, p.Total, p.Observaciones,
                    c.NombreCompleto as ClienteNombre, c.NumeroDocumento as ClienteDocumento, u.Nombre + ' ' + u.Apellido as UsuarioCreacion, c.Telefono as ClienteTelefono, c.Direccion as ClienteDireccion
                    FROM [TiendaDB].[dbo].Pedidos p
                    INNER JOIN [TiendaDB].[dbo].Clientes c ON p.ClienteId = c.Id
                    INNER JOIN [TiendaDB].[dbo].Usuarios u ON p.UsuarioCreacionId = u.Id
                    WHERE p.Id = @Id";

                    
                    var pedido = await cn.QueryFirstOrDefaultAsync<PedidoDetailDto>(query, new { Id = id });

                    if (pedido != null)
                    {
                        pedido.Detalles = (await GetDetallesAsync(id)).ToList();
                    }

                    return pedido;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PedidoRepository.GetByIdAsync: " + ex.Message);
                return null;
            }
           
        }

        
        public async Task<int> CreateAsync(PedidoCreateDto dto, int usuarioId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {
                        // Generar número de pedido
                        const string getNumero = @" SELECT 'PED' +  '-' +  RIGHT('00000000' + CAST(COUNT(*) + 1 as VARCHAR), 8)
                    FROM [TiendaDB].[dbo].Pedidos 
                   ";
                        // WHERE CAST(FechaPedido as DATE) = CAST(GETDATE() as DATE)

                        var numeroPedido = await cn.QuerySingleAsync<string>(getNumero, transaction: transaction);

                        // Calcular totales
                        decimal subTotal = 0;
                        foreach (var det in dto.Detalles)
                        {
                            const string getPrecio = "SELECT PrecioVenta FROM [TiendaDB].[dbo].Productos WHERE Id = @Id";
                            var precio = det.PrecioUnitario ?? await cn.QuerySingleAsync<decimal>(getPrecio, new { Id = det.ProductoId }, transaction);
                            subTotal += (precio * det.Cantidad) - det.Descuento;
                        }

                        //var impuestos = subTotal * 0.18m; // IGV 18%
                        var impuestos = subTotal * 0m;
                        var total = subTotal + impuestos;

                        // Insertar pedido
                        const string insertPedido = @"
                    INSERT INTO [TiendaDB].[dbo].Pedidos (NumeroPedido, ClienteId, UsuarioCreacionId, Estado, 
                        FechaEstimadaEntrega, SubTotal, Impuestos, Total, Observaciones)
                    VALUES (@NumeroPedido, @ClienteId, @UsuarioCreacionId, 'INICIO',
                        @FechaEstimadaEntrega, @SubTotal, @Impuestos, @Total, @Observaciones);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                        var pedidoId = await cn.QuerySingleAsync<int>(insertPedido, new
                        {
                            NumeroPedido = numeroPedido,
                            dto.ClienteId,
                            UsuarioCreacionId = usuarioId,
                            dto.FechaEstimadaEntrega,
                            SubTotal = subTotal,
                            Impuestos = impuestos,
                            Total = total,
                            dto.Observaciones
                        }, transaction);

                        // Insertar detalles
                        await InsertarDetalles(cn, transaction, pedidoId, dto.Detalles);

                        // Historial
                        const string historial = @"
                    INSERT INTO [TiendaDB].[dbo].HistorialPedidos (PedidoId, EstadoNuevo, UsuarioCambioId, Observacion)
                    VALUES (@PedidoId, 'INICIO', @UsuarioId, 'Pedido creado')";

                        await cn.ExecuteAsync(historial, new { PedidoId = pedidoId, UsuarioId = usuarioId }, transaction);

                        transaction.Commit();
                        return pedidoId;
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
                Console.WriteLine("PedidoRepository.CreateAsync: " + ex.Message);
                return 0;
            }
             
            
        }

        public async Task<bool> UpdateEstadoAsync(int id, string nuevoEstado, int usuarioId, string observacion = null)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {
                        // Obtener estado actual
                        const string getEstado = "SELECT Estado FROM [TiendaDB].[dbo].Pedidos WHERE Id = @Id";
                        var estadoAnterior = await cn.QuerySingleAsync<string>(getEstado, new { Id = id }, transaction);

                        // Actualizar pedido
                        var updateQuery = "UPDATE [TiendaDB].[dbo].Pedidos SET Estado = @NuevoEstado";
                        if (nuevoEstado == "FINALIZADO")
                            updateQuery += ", FechaFinalizacion = GETDATE()";
                        updateQuery += " WHERE Id = @Id";

                        await cn.ExecuteAsync(updateQuery, new { Id = id, NuevoEstado = nuevoEstado }, transaction);

                        // Historial
                        const string historial = @" INSERT INTO [TiendaDB].[dbo].HistorialPedidos 
                                (PedidoId, EstadoAnterior, EstadoNuevo, UsuarioCambioId, Observacion)
                            VALUES (@PedidoId, @EstadoAnterior, @EstadoNuevo, @UsuarioId, @Observacion)";

                        await cn.ExecuteAsync(historial, new
                        {
                            PedidoId = id,
                            EstadoAnterior = estadoAnterior,
                            EstadoNuevo = nuevoEstado,
                            UsuarioId = usuarioId,
                            Observacion = observacion ?? $"Cambio de estado: {estadoAnterior} -> {nuevoEstado}"
                        }, transaction);

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
                Console.WriteLine("PedidoRepository.UpdateEstadoAsync: " + ex.Message);
                return false;
            }
             
          
        }

        public async Task<bool> AgregarProductosAsync(int pedidoId, List<PedidoDetalleCreateDto> detalles)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {

                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {
                        // Verificar estado
                        const string checkEstado = "SELECT Estado FROM [TiendaDB].[dbo].Pedidos WHERE Id = @Id";
                        var estado = await cn.QuerySingleAsync<string>(checkEstado, new { Id = pedidoId }, transaction);

                        if (estado != "INICIO" && estado != "PROCESO")
                            throw new InvalidOperationException("No se pueden agregar productos a un pedido finalizado o cancelado");

                        // Insertar nuevos detalles
                        await InsertarDetalles(cn, transaction, pedidoId, detalles);

                        // Recalcular totales
                        const string recalcular = @"  UPDATE p SET
                        SubTotal = (SELECT SUM(SubTotal) FROM [TiendaDB].[dbo].PedidoDetalles WHERE PedidoId = p.Id),
                        Total = (SELECT SUM(SubTotal) FROM  [TiendaDB].[dbo].PedidoDetalles WHERE PedidoId = p.Id) * 1.18
                         FROM [TiendaDB].[dbo].Pedidos p  WHERE p.Id = @Id";

                        await cn.ExecuteAsync(recalcular, new { Id = pedidoId }, transaction);

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
                Console.WriteLine("PedidoRepository.AgregarProductosAsync: " + ex.Message);
                return false;
            }
            
        }

        private async Task InsertarDetalles(IDbConnection conn, IDbTransaction trans, int pedidoId, List<PedidoDetalleCreateDto> detalles)
        {
            try
            {
                 
                string insertDetalle = @" INSERT INTO [TiendaDB].[dbo].PedidoDetalles 
                (PedidoId, ProductoId, Cantidad, PrecioUnitario, UnidadMedidaId, Descuento, SubTotal)
                SELECT @PedidoId, @ProductoId, @Cantidad,   ISNULL(@PrecioUnitario, p.PrecioVenta),   p.UnidadMedidaId, @Descuento,
                    (@Cantidad * ISNULL(@PrecioUnitario, p.PrecioVenta)) - @Descuento
                FROM [TiendaDB].[dbo].Productos p
                WHERE p.Id = @ProductoId";

                foreach (var det in detalles)
                {
                    await conn.ExecuteAsync(insertDetalle, new
                    {
                        PedidoId = pedidoId,
                        det.ProductoId,
                        det.Cantidad,
                        det.PrecioUnitario,
                        det.Descuento
                    }, trans);
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("PedidoRepository.InsertarDetalles: " + ex.Message);
                 
            }
           
        }

        public async Task<IEnumerable<PedidoDetalleDto>> GetDetallesAsync(int pedidoId)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    string query = @" SELECT    pd.Id, pd.ProductoId, p.Nombre as ProductoNombre, p.Codigo as ProductoCodigo,
                    pd.Cantidad, um.Nombre as UnidadMedida,   pd.PrecioUnitario, pd.Descuento, pd.SubTotal, pd.FechaAgregado, pd.UnidadMedidaId
                    FROM [TiendaDB].[dbo].PedidoDetalles pd
                    INNER JOIN [TiendaDB].[dbo].Productos p ON pd.ProductoId = p.Id
                    INNER JOIN [TiendaDB].[dbo].UnidadesMedida um ON pd.UnidadMedidaId = um.Id
                    WHERE pd.PedidoId = @PedidoId
                    ORDER BY pd.FechaAgregado";

                    
                    return await cn.QueryAsync<PedidoDetalleDto>(query, new { PedidoId = pedidoId });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PedidoRepository.GetDetallesAsync: " + ex.Message);
                return null;
            }
            
        }
       
        
        public async Task<IEnumerable<PedidoListDto>> GetAllAsync(PedidoFiltroDto filtros)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    var query = @"  SELECT   p.Id, p.NumeroPedido, p.Estado, p.FechaPedido, 
                p.FechaEstimadaEntrega, p.Total, c.NombreCompleto as ClienteNombre, c.NumeroDocumento as ClienteDocumento,
                u.Nombre + ' ' + u.Apellido as UsuarioCreacion,
                (SELECT COUNT(*) FROM [TiendaDB].[dbo].PedidoDetalles WHERE PedidoId = p.Id) as CantidadProductos
                FROM [TiendaDB].[dbo].Pedidos p
                INNER JOIN [TiendaDB].[dbo].Clientes c ON p.ClienteId = c.Id
                INNER JOIN [TiendaDB].[dbo].Usuarios u ON p.UsuarioCreacionId = u.Id
                WHERE 1=1";

                    var parameters = new DynamicParameters();

                    if (!string.IsNullOrEmpty(filtros.Estado))
                    {
                        query += " AND p.Estado = @Estado";
                        parameters.Add("Estado", filtros.Estado);
                    }

                    if (filtros.ClienteId.HasValue)
                    {
                        query += " AND p.ClienteId = @ClienteId";
                        parameters.Add("ClienteId", filtros.ClienteId.Value);
                    }

                    if (filtros.FechaDesde.HasValue)
                    {
                        query += " AND p.FechaPedido >= @FechaDesde";
                        parameters.Add("FechaDesde", filtros.FechaDesde.Value.Date);
                    }

                    if (filtros.FechaHasta.HasValue)
                    {
                        query += " AND p.FechaPedido < @FechaHasta";
                        parameters.Add("FechaHasta", filtros.FechaHasta.Value.Date.AddDays(1));
                    }

                    if (!string.IsNullOrWhiteSpace(filtros.Busqueda))
                    {
                        query += @" AND (p.NumeroPedido LIKE @Busqueda 
                      OR c.NombreCompleto LIKE @Busqueda 
                      OR c.NumeroDocumento LIKE @Busqueda)";
                        parameters.Add("Busqueda", $"%{filtros.Busqueda}%");
                    }

                    query += " ORDER BY p.FechaPedido DESC";

                   
                    return await cn.QueryAsync<PedidoListDto>(query, parameters);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PedidoRepository.GetAllAsync: " + ex.Message);
                return null;
            }
           
        }
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using (SqlConnection cn = new(_connectionString))
                {
                    cn.Open();
                    using var transaction = cn.BeginTransaction();

                    try
                    {
                        // Verificar estado
                        const string checkEstado = "SELECT Estado FROM [TiendaDB].[dbo].Pedidos WHERE Id = @Id";
                        var estado = await cn.QuerySingleAsync<string>(checkEstado, new { Id = id }, transaction);

                        if (estado != "INICIO")
                            throw new InvalidOperationException("Solo se pueden eliminar pedidos en estado INICIO");

                        // Eliminar detalles primero
                        const string deleteDetalles = "DELETE FROM [TiendaDB].[dbo].PedidoDetalles WHERE PedidoId = @Id";
                        await cn.ExecuteAsync(deleteDetalles, new { Id = id }, transaction);

                        // Eliminar historial
                        const string deleteHistorial = "DELETE FROM [TiendaDB].[dbo].HistorialPedidos WHERE PedidoId = @Id";
                        await cn.ExecuteAsync(deleteHistorial, new { Id = id }, transaction);

                        // Eliminar pedido
                        const string deletePedido = "DELETE FROM [TiendaDB].[dbo].Pedidos WHERE Id = @Id";
                        var affected = await cn.ExecuteAsync(deletePedido, new { Id = id }, transaction);

                        transaction.Commit();
                        return affected > 0;
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
                Console.WriteLine("PedidoRepository.DeleteAsync: " + ex.Message);
                return false;
            }
           
            
        }
        // Método legacy para compatibilidad (opcional)
        public async Task<IEnumerable<PedidoListDto>> GetAllAsync(string estado = null, int? clienteId = null, DateTime? fechaDesde = null)
        {
            return await GetAllAsync(new PedidoFiltroDto
            {
                Estado = estado,
                ClienteId = clienteId,
                FechaDesde = fechaDesde
            });
        }


       
    }
}
