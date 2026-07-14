namespace Ferreteria.DTOs
{
    public class ProductoCreateDto
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string? Descripcion { get; set; }
        public int? CategoriaId { get; set; }
        public int UnidadMedidaId { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal StockInicial { get; set; }
        public string UbicacionAlmacen { get; set; }
        public string? UnidadMedidaAbreviatura { get; set; } 
         public long? idProveedor { get; set; } 
         
    }

    public class ProductoUpdateDto : ProductoCreateDto
    {
        public int Id { get; set; }

        
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; }

        
        public decimal StockActual { get; set; }
        public string? UnidadMedidaNombre { get; set; }
        public string? CategoriaNombre { get; set; }
        public string? ProveedorNombre { get; set; }
    }

    public class ProductoListDto
    {
        public int listorder { get; set; }  
        public int Id { get; set; }
        public string Descripcion { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string CategoriaNombre { get; set; }
        public string UnidadMedida { get; set; }
        public string UnidadMedidaAbreviatura { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
        public bool StockBajo => StockActual <= StockMinimo;
        public string UbicacionAlmacen { get; set; }
        public bool Activo { get; set; }
        public string NombreProveedor { get; set; }
    }
    public class ProductoDetailDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public int? CategoriaId { get; set; }
        public string CategoriaNombre { get; set; }
        public int UnidadMedidaId { get; set; }
        public string UnidadMedidaNombre { get; set; }
        public string UnidadMedidaAbreviatura { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal StockActual { get; set; }
        public string UbicacionAlmacen { get; set; }
        public long? IdProveedor { get; set; }
        public string ProveedorNombre { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCreacion { get; set; }

        // Campos calculados
        public decimal MargenGanancia => PrecioCompra > 0 ? ((PrecioVenta - PrecioCompra) / PrecioCompra) * 100 : 0;
        public bool StockBajo => StockActual <= StockMinimo;
        public decimal ValorInventario => StockActual * PrecioCompra;
    }
    

    public class MovimientoInventarioDto
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public string TipoMovimiento { get; set; }
        public decimal Cantidad { get; set; }
        public decimal StockAnterior { get; set; }
        public decimal StockNuevo { get; set; }
        public string Referencia { get; set; }
        public string Observaciones { get; set; }
        public string UsuarioNombre { get; set; }
        public DateTime FechaMovimiento { get; set; }

        public string TipoMovimientoClass => TipoMovimiento switch
        {
            "ENTRADA" => "badge bg-success",
            "SALIDA" => "badge bg-danger",
            "AJUSTE" => "badge bg-warning text-dark",
            _ => "badge bg-secondary"
        };

        public string TipoMovimientoIcon => TipoMovimiento switch
        {
            "ENTRADA" => "fas fa-arrow-up",
            "SALIDA" => "fas fa-arrow-down",
            "AJUSTE" => "fas fa-balance-scale",
            _ => "fas fa-question"
        };
    }

}
