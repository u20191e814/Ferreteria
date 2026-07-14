namespace Ferreteria.DTOs
{
    public class UnidadMedidaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Abreviatura { get; set; }
        public string NombreCompleto => $"{Nombre} ({Abreviatura})";
    }
}
