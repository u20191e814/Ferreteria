namespace Ferreteria.DTOs
{
    public class ProveedorDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public int CantidadProductos { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

    public class ProveedorCreateDto
    {
        public string Nombre { get; set; }
    }
    public class ProveedorUpdateDto : ProveedorCreateDto
    {
        public long Id { get; set; }
    }

    public class ProveedorListDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public int CantidadProductos { get; set; }
        public string FechaCreacionFormateada { get; set; }
    }
}
