namespace Ferreteria.DTOs
{
    public class CategoriaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
    }

    public class CategoriaCreateDto
    {
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
    }
     
}
