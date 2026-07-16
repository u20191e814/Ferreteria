namespace Ferreteria.DTOs
{
    public class VentaCreateDto
    {
        public int? PedidoId { get; set; }
        public int ClienteId { get; set; }
        public string MetodoPago { get; set; }
        public string? Observaciones { get; set; }
        public List<VentaDetalleCreateDto> Detalles { get; set; } = new();

        public decimal Descuento { get; set; }
        public string TipoComprobante { get; set; } = "BOLETA"; // BOLETA, FACTURA
        
        public DateTime FechaEmision { get; set; } = DateTime.Now;
        public decimal SubTotal { get; set; }
        public decimal Impuestos { get; set; }
        public decimal Total { get; set; }
        public decimal PorcentajeDescuentoClient { get; set; }
        public bool IncluyeIGV { get; set; } = true;
    }

    public class VentaDetalleCreateDto
    {
        public int ProductoId { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Cantidad { get; set; }
        public decimal Descuento { get; set; }
        public decimal SubTotal { get; set; }
    }

    public class VentaListDto
    {
        public int listorder { get; set; }  
        public int Id { get; set; }
        public string TipoComprobante { get; set; }
        public string NumeroFactura { get; set; }
        public string ClienteNombre { get; set; }
        public string VendedorNombre { get; set; }
        public DateTime FechaVenta { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; }
        public string Estado { get; set; }
        public decimal Descuento { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Impuestos { get; set; }

        public bool PuedeAnular => Estado == "COMPLETADA" &&
                               DateTime.Now.Subtract(FechaVenta).TotalDays <= 7;

        public string EstadoClass => Estado switch
        {
            "COMPLETADA" => "badge bg-success",
            "ANULADA" => "badge bg-danger",
            "PENDIENTE" => "badge bg-warning",
            _ => "badge bg-secondary"
        };
    }

    public class VentaResultDto
    {
        public int VentaId { get; set; }
        public string Comprobante { get; set; }
        public string NumeroCompleto { get; set; } // Serie-Numero formateado
        public decimal Total { get; set; }
        public DateTime FechaEmision { get; set; }
    }


    // Nuevo: VentaAnulacionDto.cs
    public class VentaAnulacionDto
    {
        public int VentaId { get; set; }
        public string Motivo { get; set; }
        public DateTime FechaAnulacion { get; set; }
        public int UsuarioAnulacionId { get; set; }
        public string UsuarioAnulacionNombre { get; set; }
    }


    public class ClienteEstadisticasDto
    {
        public int CantidadVentas { get; set; }
        public decimal TotalComprado { get; set; }
        public DateTime? UltimaCompra { get; set; }
    }
    public class ProductoStockRevertidoDto
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; }
        public decimal CantidadRevertida { get; set; }
        public decimal StockAnterior { get; set; }
        public decimal StockNuevo { get; set; }
    }
}
