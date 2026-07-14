namespace Ferreteria.DTOs
{
    public class AjusteStockDto
    {
        public int ProductoId { get; set; }
        public decimal NuevoStock { get; set; }
        public string TipoMovimiento { get; set; } = "AJUSTE"; // AJUSTE, ENTRADA, SALIDA
        public string Motivo { get; set; }
        public decimal StockAnterior { get; set; }
        public decimal Diferencia => NuevoStock - StockAnterior;
    }

    public class AjusteStockResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal StockAnterior { get; set; }
        public decimal StockNuevo { get; set; }
        public DateTime FechaMovimiento { get; set; }
    }
}
