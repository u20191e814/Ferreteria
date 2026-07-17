namespace Ferreteria.DTOs
{
    public class ReporteVentasFiltroDto
    {
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? UsuarioId { get; set; }
        public string Agrupacion { get; set; } // DIARIA, SEMANAL, MENSUAL
    }

    public class ReporteVentasDto
    {
        public DateTime Fecha { get; set; }
        public string Periodo { get; set; }
        public int CantidadVentas { get; set; }
        public decimal TotalVentas { get; set; }
        public decimal TotalDescuentos { get; set; }
        public decimal PromedioVenta { get; set; }
        public List<VentaPorUsuarioDto> VentasPorUsuario { get; set; }
    }

    public class VentaPorUsuarioDto
    {
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public int CantidadVentas { get; set; }
        public decimal TotalVentas { get; set; }
    }
    public class TopProductoDto
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; }
        public int CantidadVendida { get; set; }
        public decimal TotalVentas { get; set; }
    }
    public class DashboardDto
    {
        public decimal VentasHoy { get; set; }
        public int PedidosPendientes { get; set; }
        public int ProductosStockBajo { get; set; }
        public List<PedidoListDto> UltimosPedidos { get; set; }
        public List<ProductoListDto> ProductosMasVendidos { get; set; }
    }
}
