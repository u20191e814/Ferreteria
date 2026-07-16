using Ferreteria.DTOs;
using Ferreteria.Entities;
using Ferreteria.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Ferreteria.Controllers
{
    [Authorize]
    public class VentasController : Controller
    {
        private readonly IVentaRepository _ventaRepo;
        private readonly IClienteRepository _clienteRepo;
        private readonly IProductoRepository _productoRepo;
        private readonly ILogger<VentasController> _logger;
        private readonly IUsuarioRepository _usuarioRepo;
        private readonly ITienda _repoTienda;
        public VentasController(IVentaRepository repo,
            IClienteRepository clienteRepo,
            IProductoRepository productoRepo,
        IUsuarioRepository usuarioRepo,
        ITienda repoTienda ,
            ILogger<VentasController> logger)
        {
            _ventaRepo = repo;
            _clienteRepo = clienteRepo;
            _productoRepo = productoRepo;
            _usuarioRepo = usuarioRepo;
            _repoTienda  = repoTienda;
            _logger = logger;
        }

        public async Task<IActionResult> Index(    DateTime fechaInicio,    DateTime fechaFin,    string metodoPago,
                    string estado, string clienteBusqueda, string tipoComprobante,      int? usuarioId,int page = 1,  int pageSize = 10)                 
        {
            if (fechaInicio == DateTime.MinValue || fechaInicio ==null)
            {
                fechaInicio = DateTime.Now.Date.AddDays(-30);
            }
            if (fechaFin == DateTime.MinValue || fechaFin == null)
            {
                fechaFin = DateTime.Now.Date;
            }
             
            ViewBag.MetodoPago = metodoPago;
            ViewBag.UsuarioId = usuarioId;
            
            var esAdmin = User.IsInRole("Administrador");
            var userIdActual = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var filtroUsuarioId = esAdmin ? usuarioId : userIdActual;

            var ventas = await _ventaRepo.GetAllAsync(fechaInicio, fechaFin, metodoPago, estado, clienteBusqueda, filtroUsuarioId, page, pageSize, tipoComprobante);

           
            if (esAdmin)
            {
                var vendedores = await _usuarioRepo.GetActivosAsync();
                ViewBag.Vendedores = vendedores;
            }
            else
            {
                ViewBag.Vendedores = new List<UsuarioListDto>(); // Lista vacía para no-admins
            }
             
            ViewBag.Estado = estado;
            ViewBag.ClienteBusqueda = clienteBusqueda;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = ventas.Item2;
            return View(ventas.Item1);
        }

       

        public IActionResult Create(int? pedidoId)
        {
            ViewBag.PedidoId = pedidoId;
            return View();
        }
       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VentaCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var ventaId = await _ventaRepo.CreateAsync(dto, usuarioId);
            return RedirectToAction(nameof(Index));
        }
        
        public async Task<IActionResult> Reporte(DateTime fechaInicio, DateTime fechaFin, string agrupacion = "DIARIA")
        {
            if (fechaInicio ==DateTime.MinValue)
            {
                fechaInicio = DateTime.Now.AddDays(-30);
            }
            if (fechaFin == DateTime.MinValue)
            {
                fechaFin = DateTime.Now;
            }
            var usuarioId = User.IsInRole("Administrador") ? (int?)null : int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var reporte = await _ventaRepo.GetReporteAsync(fechaInicio, fechaFin, agrupacion, usuarioId);
            return View(reporte);
        }

        [HttpGet]
        public async Task<IActionResult> BuscarProductos(string texto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
                {
                    return Json(new List<object>());
                }

                _logger.LogInformation("Buscando productos con texto: {Texto}", texto);

                var productos = await _productoRepo.GetAllAsync(texto);

                var resultado = productos.Item1.Select(p => new
                {
                    id = p.Id,
                    codigo = p.Codigo,
                    nombre = p.Nombre,
                    descripcion = p.Descripcion,
                    precioVenta = p.PrecioVenta,
                    stockActual = p.StockActual,
                    stockMinimo = p.StockMinimo,
                    unidadMedida = p.UnidadMedida,
                    abreviaturaUnidad = p.UnidadMedidaAbreviatura,
                    categoria = p.CategoriaNombre,
                    ubicacionAlmacen = p.UbicacionAlmacen,
                    stockBajo = p.StockBajo
                    
                });

                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en BuscarProductos");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // GET: /Ventas/ObtenerProducto?id=5
        [HttpGet]
        public async Task<IActionResult> ObtenerProducto(int id)
        {
            try
            {
                _logger.LogInformation("Obteniendo producto ID: {Id}", id);

                var producto = await _productoRepo.GetByIdAsync(id);

                if (producto == null)
                {
                    _logger.LogWarning("Producto no encontrado: {Id}", id);
                    return NotFound(new { message = "Producto no encontrado" });
                }

                return Json(new
                {
                    id = producto.Id,
                    codigo = producto.Codigo,
                    nombre = producto.Nombre,
                    descripcion = producto.Descripcion,
                    precioVenta = producto.PrecioVenta,
                    precioCompra = producto.PrecioCompra,
                    stockActual = producto.StockActual,
                    stockMinimo = producto.StockMinimo,
                    unidadMedidaId = producto.UnidadMedidaId,
                    unidadMedidaNombre = producto.UnidadMedidaNombre,
                    unidadMedidaAbreviatura = producto.Abreviatura,
                    categoriaId = producto.CategoriaId,
                    categoriaNombre = producto.catNombre,
                    ubicacionAlmacen = producto.UbicacionAlmacen,
                    stockBajo = producto.StockActual <= producto.StockMinimo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ObtenerProducto");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> BuscarClientes(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
            {
                return Json(new List<object>());
            }

            var clientes = await _clienteRepo.BuscarPorTextoAsync(texto);

            var resultado = clientes.Select(c => new
            {
                c.Id,
                c.NombreCompleto,
                c.TipoDocumento,
                c.NumeroDocumento,
                c.Telefono,
                c.Email,
                c.EsFrecuente,
                c.DescuentoPreferencial,
                c.Direccion
            });

            return Json(resultado);
        }

        // GET: /Ventas/ObtenerCliente/5
        [HttpGet]
        public async Task<IActionResult> ObtenerCliente(int id)
        {
            var cliente = await _clienteRepo.GetByIdAsync(id);

            if (cliente == null)
            {
                return NotFound();
            }

            return Json(new
            {
                cliente.Id,
                cliente.NombreCompleto,
                cliente.TipoDocumento,
                cliente.NumeroDocumento,
                cliente.Telefono,
                cliente.Email,
                cliente.Direccion,
                cliente.EsFrecuente,
                cliente.DescuentoPreferencial
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            var venta = await _ventaRepo.GetByIdAsync(id); // Necesitas agregar este método al repository
            if (venta == null)
            {
                TempData["Error"] = "Venta no encontrada";
                return RedirectToAction(nameof(Index));
            }
            return View(venta);
        }
 
        [HttpGet]
        public async Task<IActionResult> VistaPreviaPDF(int id, string formato = "a4")
        {
            var venta = await _ventaRepo.GetByIdAsync(id);
            if (venta == null) return NotFound();

            var esTicket = formato?.ToLower() == "80mm";
            var viewName = esTicket ? "PdfTicketModerno" : "PdfA4Moderno";

            return View(viewName, venta);
        }
        public async Task<IActionResult> CapturaTicket(int id)
        {
            var venta = await _ventaRepo.GetByIdAsync(id);
            if (venta == null) return NotFound();
            var tienda = await _repoTienda.GetTienda();
            ViewBag.Tienda = tienda;
            return View(venta);
        }
        public async Task<IActionResult> CapturaA4(int id)
        {
            var venta = await _ventaRepo.GetByIdAsync(id);
            if (venta == null) return NotFound();
            var tienda = await _repoTienda.GetTienda();
            ViewBag.Tienda = tienda;
            return View(venta);
        }

        // GET: Ventas/ValidarAnulacion/5 (AJAX)
        [HttpGet]
        public async Task<IActionResult> ValidarAnulacion(int id)
        {
            try
            {
                var venta = await _ventaRepo.GetByIdAsync(id);
                if (venta == null)
                    return NotFound(new { error = "Venta no encontrada" });

                var puedeAnular =true;

                var mensaje = !puedeAnular ? ObtenerMensajeNoAnulacion(venta) : null;

                return Json(new
                {
                    puedeAnular,
                    mensaje,
                    ventaId = id,
                    numeroFactura = venta.NumeroFactura,
                    total = venta.Total,
                    fechaVenta = venta.FechaVenta,
                    estado = venta.Estado,
                    diasTranscurridos = DateTime.Now.Subtract(venta.FechaVenta).Days
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validando anulación de venta {VentaId}", id);
                return StatusCode(500, new { error = "Error al validar anulación" });
            }
        }

        private string ObtenerMensajeNoAnulacion(VentaDetailDto venta)
        {
            if (venta.Estado != "COMPLETADA")
                return "Solo se pueden anular ventas completadas.";

            var dias = DateTime.Now.Subtract(venta.FechaVenta).Days;
            if (dias > 7)
                return $"No se puede anular. La venta tiene {dias} días de antigüedad (máximo 7 días).";

            return "No se puede anular esta venta. Puede tener notas de crédito asociadas.";
        }

        // POST: Ventas/Anular/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Anular(int id, string motivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivo) || motivo.Length < 10)
                {
                    TempData["Error"] = "Debe ingresar un motivo de anulación (mínimo 10 caracteres)";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                if (usuarioId==0)
                    return Unauthorized();

               
                var resultado = await _ventaRepo.AnularAsync(id, usuarioId, motivo);

                if (resultado)
                {
                    TempData["Success"] = $"Venta anulada correctamente. El stock de los productos ha sido revertido.";
                }
                else
                {
                    TempData["Error"] = "No se pudo anular la venta.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error anulando venta {VentaId}", id);
                TempData["Error"] = "Error al anular la venta: " + ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

         
        [HttpGet]
        public async Task<IActionResult> DetalleAnulacion(int id)
        {
            var venta = await _ventaRepo.GetByIdAsync(id);
            if (venta == null || venta.Estado != "ANULADA")
                return NotFound();

            var historial = await _ventaRepo.GetHistorialAnulacionesAsync(id);
            return Json(historial.FirstOrDefault());
        }

        [HttpGet]
        public async Task<IActionResult> EstadisticasCliente(int clienteId)
        {
            try
            {
                var stats = await _ventaRepo.GetEstadisticasClienteAsync(clienteId);
                if (stats == null)
                    return Json(new ClienteEstadisticasDto { CantidadVentas = 0, TotalComprado = 0, UltimaCompra = null });

                return Json(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo estadísticas del cliente {ClienteId}", clienteId);
                return StatusCode(500, new { error = "Error al obtener estadísticas" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> HistorialPorCliente(int clienteId, int page = 1, int pageSize = 10)
        {
            try
            {
                var result = await _ventaRepo.GetVentasPorClienteAsync(clienteId, page, pageSize);
                var items = result.Item1;
                var total = result.Item2;
                return Json(new { items, total });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo historial del cliente {ClienteId}", clienteId);
                return StatusCode(500, new { error = "Error al obtener historial" });
            }
        }

    }
}
