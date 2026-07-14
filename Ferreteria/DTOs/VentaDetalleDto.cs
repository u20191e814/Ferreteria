namespace Ferreteria.DTOs
{
    public class VentaDetalleDto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; }
        public string ProductoCodigo { get; set; }
        public decimal Cantidad { get; set; }
        public string UnidadMedida { get; set; }
        public string AbreviaturaUnidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; }
        public decimal SubTotal { get; set; }
        public decimal Total { get { return Cantidad * PrecioUnitario; } }
    }

    public class VentaDetailDto : VentaListDto
    {
        public string Observaciones { get; set; }
        public List<VentaDetalleDto> Detalles { get; set; } = new();
        public string ClienteDireccion { get; set; }
        public string ClienteTelefono { get; set; }
        public string ClienteEmail { get; set; }
        public string ClienteDocumento { get; set; }

        public DateTime? FechaAnulacion { get; set; }
        public string MotivoAnulacion { get; set; }
        public string UsuarioAnulacionNombre { get; set; }
        public bool EstaAnulada => Estado == "ANULADA";
    }
}
