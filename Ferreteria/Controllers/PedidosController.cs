using Ferreteria.DTOs;
using Ferreteria.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ferreteria.Controllers
{
    [Authorize]
    public class PedidosController : Controller
    {
        private readonly IPedidoRepository _repo;
        private readonly IProductoRepository _productoRepo;
        private readonly ILogger<PedidosController> _logger;
        public PedidosController(IPedidoRepository repo,
            IProductoRepository productoRepo ,
            ILogger<PedidosController> logger)
        {
            _productoRepo = productoRepo;
            _repo = repo;
            _logger = logger;
        }
        public async Task<IActionResult> Index( string estado, int? clienteId, DateTime? fechaDesde, DateTime? fechaHasta, string busqueda)
        {

            if (fechaDesde==null ||  fechaDesde.Value ==DateTime.MinValue)
            {
                fechaDesde =  DateTime.Now.AddDays(-2);
            }
            if (fechaHasta == null || fechaHasta.Value == DateTime.MinValue)
            {
                fechaHasta = DateTime.Now;
            }
            var filtros = new PedidoFiltroDto
            {
                Estado = estado,
                ClienteId = clienteId,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Busqueda = busqueda
            };

            var pedidos = await _repo.GetAllAsync(filtros);

            // ViewBags para filtros
            ViewBag.EstadoSeleccionado = estado;
            ViewBag.ClienteId = clienteId;
            ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
            ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");
            ViewBag.Busqueda = busqueda;

            return View(pedidos);
        }
         
        public async Task<IActionResult> Details(int id)
        {
            var pedido = await _repo.GetByIdAsync(id);
            if (pedido == null)
            {
                TempData["Error"] = "Pedido no encontrado";
                return RedirectToAction(nameof(Index));
            }

            // Obtener historial de cambios (opcional - si tienes repositorio de historial)
            // ViewBag.Historial = await _historialRepo.GetByPedidoIdAsync(id);

            return View(pedido);
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PedidoCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var numeroPedido = await _repo.CreateAsync(dto, usuarioId);
            return RedirectToAction(nameof(Details), new { id = numeroPedido });
        }

        [HttpPost]
        public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado, string observacion)
        {
            var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            await _repo.UpdateEstadoAsync(id, nuevoEstado, usuarioId, observacion);
            return RedirectToAction(nameof(Details), new { id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var pedido = await _repo.GetByIdAsync(id);
                if (pedido == null)
                {
                    TempData["Error"] = "Pedido no encontrado";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que puede eliminarse
                if (pedido.Estado != "INICIO")
                {
                    TempData["Error"] = "Solo se pueden eliminar pedidos en estado INICIO";
                    return RedirectToAction(nameof(Details), new { id });
                }

                await _repo.DeleteAsync(id);
                TempData["Success"] = $"Pedido {pedido.NumeroPedido} eliminado correctamente";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando pedido {PedidoId}", id);
                TempData["Error"] = "Error al eliminar: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        [HttpPost]
        public async Task<IActionResult> AgregarProductos(int id, List<PedidoDetalleCreateDto> detalles)
        {
            await _repo.AgregarProductosAsync(id, detalles);
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProducto(int id)
        {
            var producto = await _productoRepo.GetByIdAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            return Json(new
            {
                id = producto.Id,
                codigo = producto.Codigo,
                nombre = producto.Nombre,
                precioVenta = producto.PrecioVenta,
                stockActual = producto.StockActual,
                unidadMedidaId = producto.UnidadMedidaId,
                unidadMedida = producto.UnidadMedidaNombre,
                abreviaturaUnidad = producto.Abreviatura
            });
        }

        [HttpGet]
        public async Task<IActionResult> BuscarProductos(string termino)
        {
            if (string.IsNullOrWhiteSpace(termino) || termino.Length < 2)
                return Json(new List<object>());

            try
            {
                // Asumiendo que tienes un método de búsqueda en ProductoRepository
                // Si no existe, implementar búsqueda básica
                var productos = await _productoRepo.GetAllAsync(termino, null);

                var resultado = productos.Item1.Select(p => new
                {
                    p.Id,
                    p.Codigo,
                    p.Nombre,
                    p.PrecioVenta,
                    p.StockActual,
                    p.UnidadMedida
                });

                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error buscando productos");
                return StatusCode(500, new { error = "Error al buscar productos" });
            }
        }


        [HttpGet]
        public async Task<IActionResult> ValidarParaVenta(int id)
        {
            try
            {
                var pedido = await _repo.GetByIdAsync(id);
                if (pedido == null)
                    return NotFound(new { error = "Pedido no encontrado" });

                if (pedido.Estado != "FINALIZADO")
                    return BadRequest(new { error = "El pedido debe estar finalizado", estado = pedido.Estado });

                var existeVenta = await _repo.ExisteVentaDePedidoAsync(id);
                if (existeVenta)
                    return BadRequest(new { error = "Este pedido ya fue convertido en venta", yaConvertido = true });

                // Retornar datos para pre-llenar la venta
                return Json(new
                {
                    valido = true,
                    pedidoId = id,
                    clienteId = pedido.ClienteId, // Necesitarías agregar ClienteId a PedidoDetailDto
                    clienteNombre = pedido.ClienteNombre,
                    total = pedido.Total,
                    subTotal = pedido.SubTotal,
                    impuestos = pedido.Impuestos,
                    detalles = pedido.Detalles.Select(d => new
                    {
                        d.ProductoId,
                        d.ProductoNombre,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Descuento,
                        d.SubTotal
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando pedido {PedidoId} para venta", id);
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvertirAVenta(int id, [FromForm] VentaCreateDto dto)
        {
            try
            {
                var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                 
                var pedido = await _repo.GetByIdAsync(id);
                if (pedido == null)
                {
                    TempData["Error"] = "Pedido no encontrado";
                    return RedirectToAction(nameof(Details), new { id });
                }

                if (pedido.Estado != "FINALIZADO")
                {
                    TempData["Error"] = "Solo pedidos finalizados pueden convertirse en ventas";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var resultado = await _repo.ConvertirAVentaAsync(id, usuarioId, dto);

                TempData["Success"] = $"¡Venta generada exitosamente! Comprobante: {resultado.NumeroCompleto} por S/ {resultado.Total:N2}";

                // Redirigir a la vista de la venta creada (asumiendo que existe VentasController)
                return RedirectToAction("Details", "Ventas", new { id = resultado.VentaId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error convirtiendo pedido {PedidoId} a venta", id);
                TempData["Error"] = "Error al convertir a venta: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }

}
