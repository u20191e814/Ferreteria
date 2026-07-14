namespace Ferreteria.Entities
{ 

    public class UsuarioListDto
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public bool Activo { get; set; }
        public string Telefono { get; set; }
        public DateTime UltimoAcceso { get; set; }
        public DateTime FechaCreacion { get; set; }
        public long RolId { get; set; }
        public string PasswordHash { get; set;  }

    }

    // Nuevo: UsuarioFilterDto.cs (opcional, para filtros futuros)
    public class UsuarioFilterDto
    {
        public string Busqueda { get; set; }
        public string Rol { get; set; }
        public bool? Activo { get; set; }
    }

    public class UsuarioCreateDto
    {
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public int RolId { get; set; }
        public string Telefono { get; set; }
    }

    public class UsuarioUpdateDto
    {
        public string Telefono { get; set; }
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public long RolId { get; set; }
    }
}
