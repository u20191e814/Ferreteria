namespace Ferreteria.DTOs
{
    public class LoginRequestDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    

    public class UsuarioDto
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; }
        public string Email { get; set; }
        public string Rol { get; set; }
        public string Permisos { get; set; }
    }
}
