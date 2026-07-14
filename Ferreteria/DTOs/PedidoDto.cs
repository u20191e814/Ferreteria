namespace Ferreteria.DTOs
{
    public class PedidoCreateDto
    {
        public int ClienteId { get; set; }
        public DateTime? FechaEstimadaEntrega { get; set; }
        public string Observaciones { get; set; }
        public List<PedidoDetalleCreateDto> Detalles { get; set; } = new();
    }

    public class PedidoDetalleCreateDto
    {
        public int ProductoId { get; set; }
        public decimal Cantidad { get; set; }
        public decimal? PrecioUnitario { get; set; } // Si es null, toma el precio actual
        public decimal Descuento { get; set; }
    }

    public class PedidoUpdateDto
    {
        public int Id { get; set; }
        public string Estado { get; set; }
        public List<PedidoDetalleCreateDto> NuevosDetalles { get; set; } = new();
        public string Observaciones { get; set; }
    }

    public class PedidoListDto
    {
        public int Id { get; set; }
        public string NumeroPedido { get; set; }
        public string ClienteNombre { get; set; }
        public string ClienteDocumento { get; set; }
        public string Estado { get; set; }
        public string EstadoClass => Estado switch
        {
            "INICIO" => "badge bg-warning",
            "PROCESO" => "badge bg-info",
            "FINALIZADO" => "badge bg-success",
            "CANCELADO" => "badge bg-danger",
            _ => "badge bg-secondary"
        };
        public DateTime FechaPedido { get; set; }
        public DateTime? FechaEstimadaEntrega { get; set; }
        public decimal Total { get; set; }
        public int CantidadProductos { get; set; }
        public string UsuarioCreacion { get; set; }

        public bool PuedeEliminar => Estado == "INICIO";
        public bool PuedeEditar => Estado == "INICIO" || Estado == "PROCESO";
        public bool PuedeFinalizar => Estado == "PROCESO";
        public bool PuedeCancelar => Estado == "INICIO" || Estado == "PROCESO";
    }

    public class PedidoDetalleDto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; }
        public string ProductoCodigo { get; set; }
        public decimal Cantidad { get; set; }
        public string UnidadMedida { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal SubTotal { get; set; }
        public int UnidadMedidaId { get; set; }
        public DateTime FechaAgregado { get; set; }
    }

    public class PedidoDetailDto : PedidoListDto
    {
        public int ClienteId { get; set; }
        
        public DateTime? FechaFinalizacion { get; set; }
        public decimal Descuento { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Impuestos { get; set; }
        public string ClienteDireccion  { get; set; }
        public string ClienteTelefono { get; set; } 
        public string Observaciones { get; set; }
        public List<PedidoDetalleDto> Detalles { get; set; }
        public bool PuedeAgregarProductos => Estado == "INICIO" || Estado == "PROCESO";
    }
    public class PedidoFiltroDto
    {
        public string Estado { get; set; }
        public int? ClienteId { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public string Busqueda { get; set; } // Por número de pedido o cliente
    }
}
