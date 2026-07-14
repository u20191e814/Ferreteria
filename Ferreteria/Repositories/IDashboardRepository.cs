using Dapper; 
using Ferreteria.DTOs;
using Microsoft.Data.SqlClient;

namespace Ferreteria.Repositories
{
    public interface IDashboardRepository
    {
        Task<DashboardDto> GetDashboardAsync();
    }

    public class DashboardRepository : IDashboardRepository
    {
        private readonly string _connectionString;
        private readonly IPedidoRepository _pedidoRepo;
         

        public DashboardRepository(IConfiguration configuration, IPedidoRepository pedidoRepo )
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _pedidoRepo = pedidoRepo;
          
        }

        public async Task<DashboardDto> GetDashboardAsync()
        {
            try
            {
                using (SqlConnection cn = new SqlConnection(_connectionString))
                {
                    // Ventas de hoy
                    string ventasHoyQuery = @"   SELECT ISNULL(SUM(Total), 0)  FROM [TiendaDB].[dbo].Ventas 
                    WHERE CAST(FechaVenta as DATE) = CAST(GETDATE() as DATE) AND Estado = 'COMPLETADA'";

                    var ventasHoy = await cn.QuerySingleAsync<decimal>(ventasHoyQuery);

                    // Pedidos pendientes (INICIO o PROCESO)
                    string pedidosPendientesQuery = @" SELECT COUNT(*)   FROM [TiendaDB].[dbo].Pedidos 
                                                        WHERE Estado IN ('INICIO', 'PROCESO')";

                    var pedidosPendientes = await cn.QuerySingleAsync<int>(pedidosPendientesQuery);

                    // Stock bajo
                    string stockBajoQuery = @"  SELECT COUNT(*)    FROM [TiendaDB].[dbo].Productos  WHERE StockActual <= StockMinimo AND Activo = 1";

                    var stockBajo = await cn.QuerySingleAsync<int>(stockBajoQuery);

                    // Últimos pedidos
                    var ultimosPedidos = await _pedidoRepo.GetAllAsync();
                    var ultimos5Pedidos = ultimosPedidos.Take(5);

                    // Productos más vendidos
                    string masVendidosQuery = @"  SELECT TOP 5 
                    p.Id, p.Codigo, p.Nombre,  c.Nombre as Categoria,
                    um.Nombre as UnidadMedida, um.Abreviatura as AbreviaturaUnidad,
                    p.PrecioVenta, p.StockActual, p.StockMinimo, p.UbicacionAlmacen, p.Activo,  SUM(vd.Cantidad) as CantidadVendida
                    FROM [TiendaDB].[dbo].VentaDetalles vd
                    INNER JOIN [TiendaDB].[dbo].Productos p ON vd.ProductoId = p.Id
                    LEFT JOIN [TiendaDB].[dbo].Categorias c ON p.CategoriaId = c.Id
                    LEFT JOIN [TiendaDB].[dbo].UnidadesMedida um ON p.UnidadMedidaId = um.Id
                    INNER JOIN [TiendaDB].[dbo].Ventas v ON vd.VentaId = v.Id
                    WHERE v.FechaVenta >= DATEADD(DAY, -30, GETDATE())
                    GROUP BY p.Id, p.Codigo, p.Nombre, c.Nombre, um.Nombre, um.Abreviatura,
                        p.PrecioVenta, p.StockActual, p.StockMinimo, p.UbicacionAlmacen, p.Activo
                    ORDER BY CantidadVendida DESC";

                    var productosMasVendidos = await cn.QueryAsync<ProductoListDto>(masVendidosQuery);

                    return new DashboardDto
                    {
                        VentasHoy = ventasHoy,
                        PedidosPendientes = pedidosPendientes,
                        ProductosStockBajo = stockBajo,
                        UltimosPedidos = ultimos5Pedidos.ToList(),
                        ProductosMasVendidos = productosMasVendidos.ToList()
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DashboardRepository.GetDashboardAsync: " + ex.Message);
                return null;
            }
             

            
        }
    }

}
