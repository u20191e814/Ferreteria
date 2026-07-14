namespace Ferreteria.Entities
{
    public class Producto
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public int? CategoriaId { get; set; }
        public int UnidadMedidaId { get; set; }
        public long idProveedor { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal StockActual { get; set; }
        public string UbicacionAlmacen { get; set; }
        public string Proveedor { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int? UsuarioCreacionId { get; set; }
        public string UnidadMedidaNombre { get; set; }
        public string Abreviatura { get; set; }
        public string catNombre { get; set; }
       
        
        public UnidadMedida UnidadMedida { get; set; }
    }
}
