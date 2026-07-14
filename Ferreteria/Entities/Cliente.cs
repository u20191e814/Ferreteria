using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Entities
{
  
    public class Cliente
    {
        public int Id { get; set; }
        public string? TipoDocumento { get; set; }
        public string? NumeroDocumento { get; set; }
        public string NombreCompleto { get; set; }
        public string Direccion { get; set; }
        public string Telefono { get; set; }
        public string? Email { get; set; }
        public bool EsFrecuente { get; set; }
        public decimal DescuentoPreferencial { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
