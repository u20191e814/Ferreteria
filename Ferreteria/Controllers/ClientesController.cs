using Ferreteria.Entities;
using Ferreteria.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Controllers
{
     

    [Authorize]
    public class ClientesController : Controller
    {
        private readonly IClienteRepository _repo;
        private readonly ILogger<ClientesController> _logger;

        public ClientesController(IClienteRepository repo, ILogger<ClientesController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        // GET: /Clientes
        public async Task<IActionResult> Index(string busqueda, string tipoDocumento, bool? esFrecuente)
        {
            var clientes = await _repo.GetAllAsync(busqueda, tipoDocumento, esFrecuente);

            // Guardar filtros para la vista
            ViewBag.Busqueda = busqueda;
            ViewBag.TipoDocumento = tipoDocumento;
            ViewBag.EsFrecuente = esFrecuente;

            return View(clientes);
        }

        // GET: /Clientes/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var cliente = await _repo.GetByIdAsync(id);
            if (cliente == null)
            {
                TempData["Error"] = "Cliente no encontrado";
                return RedirectToAction(nameof(Index));
            }

            return View(cliente);
        }

        // GET: /Clientes/Create
        public IActionResult Create()
        {
            return View(new Cliente());
        }

        // POST: /Clientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cliente cliente, bool guardarYNuevo = false)
        {
            // Validar modelo
            if (!ModelState.IsValid)
            {
                return View(cliente);
            }

            // Validar documento único
            var existe = await _repo.ExisteDocumentoAsync(cliente.TipoDocumento, cliente.NumeroDocumento);
            if (existe)
            {
                ModelState.AddModelError("NumeroDocumento", "Ya existe un cliente con este número de documento");
                return View(cliente);
            }

            // Validar formato de documento
            if (!ValidarFormatoDocumento(cliente.TipoDocumento, cliente.NumeroDocumento))
            {
                ModelState.AddModelError("NumeroDocumento", "El formato del documento no es válido");
                return View(cliente);
            }

            try
            {
                // Asegurar valores por defecto
                cliente.Activo = true;
                if (!cliente.EsFrecuente)
                {
                    cliente.DescuentoPreferencial = 0;
                }

                var id = await _repo.CreateAsync(cliente);
                _logger.LogInformation("Cliente creado: {ClienteId} - {Nombre}", id, cliente.NombreCompleto);

                TempData["Success"] = $"Cliente '{cliente.NombreCompleto}' creado exitosamente";

                if (guardarYNuevo)
                {
                    return RedirectToAction(nameof(Create));
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando cliente");
                ModelState.AddModelError("", "Ocurrió un error al guardar el cliente");
                return View(cliente);
            }
        }

        // GET: /Clientes/Edit/5
        

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cliente cliente)
        {
            if (id != cliente.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(cliente);
            }

            // Verificar que el cliente existe
            var existeCliente = await _repo.GetByIdAsync(id);
            if (existeCliente == null)
            {
                TempData["Error"] = "Cliente no encontrado";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Mantener datos originales que no se editan
                cliente.TipoDocumento = existeCliente.TipoDocumento;
                cliente.NumeroDocumento = existeCliente.NumeroDocumento;
                cliente.FechaCreacion = existeCliente.FechaCreacion;

                // Ajustar descuento si no es frecuente
                if (!cliente.EsFrecuente)
                {
                    cliente.DescuentoPreferencial = 0;
                }

                var actualizado = await _repo.UpdateAsync(cliente);

                if (actualizado)
                {
                    _logger.LogInformation("Cliente actualizado: {ClienteId}", id);
                    TempData["Success"] = $"Cliente '{cliente.NombreCompleto}' actualizado exitosamente";
                }
                else
                {
                    TempData["Error"] = "No se pudo actualizar el cliente";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando cliente {ClienteId}", id);
                ModelState.AddModelError("", "Ocurrió un error al actualizar el cliente");
                return View(cliente);
            }
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var cliente = await _repo.GetByIdAsync(id);
                if (cliente == null)
                {
                    TempData["Error"] = "Cliente no encontrado";
                    return RedirectToAction(nameof(Index));
                }

                var eliminado = await _repo.DeleteAsync(id);

                if (eliminado)
                {
                    _logger.LogInformation("Cliente eliminado: {ClienteId} - {Nombre}", id, cliente.NombreCompleto);
                    TempData["Success"] = $"Cliente '{cliente.NombreCompleto}' eliminado exitosamente";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar el cliente";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando cliente {ClienteId}", id);
                TempData["Error"] = "Ocurrió un error al eliminar el cliente";
            }

            return RedirectToAction(nameof(Index));
        }

        // AJAX: /Clientes/Buscar?texto=xxx

        public async Task<IActionResult> Edit(int id)
        {
            var cliente = await _repo.GetByIdAsync(id);
            if (cliente == null)
            {
                TempData["Error"] = "Cliente no encontrado";
                return RedirectToAction(nameof(Index));
            }

            return View(cliente);
        }
        [HttpGet]
        public async Task<IActionResult> Buscar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 3)
            {
                return Json(new List<object>());
            }

            var clientes = await _repo.BuscarPorTextoAsync(texto);

            var resultado = clientes.Select(c => new
            {
                c.Id,
                c.NombreCompleto,
                c.TipoDocumento,
                c.NumeroDocumento,
                c.Telefono,
                c.Email,
                c.EsFrecuente,
                c.DescuentoPreferencial
            });

            return Json(resultado);
        }

        // AJAX: /Clientes/ValidarDocumento
        [HttpGet]
        public async Task<IActionResult> ValidarDocumento(string tipo, string numero, int? excluirId = null)
        {
            if (string.IsNullOrEmpty(tipo) || string.IsNullOrEmpty(numero))
            {
                return Json(new { valido = false, mensaje = "Datos incompletos" });
            }

            // Validar formato
            if (!ValidarFormatoDocumento(tipo, numero))
            {
                return Json(new { valido = false, mensaje = "Formato de documento inválido" });
            }

            // Verificar duplicado
            var existe = await _repo.ExisteDocumentoAsync(tipo, numero, excluirId);

            if (existe)
            {
                return Json(new { valido = false, mensaje = "Este documento ya está registrado" });
            }

            return Json(new { valido = true, mensaje = "Documento válido" });
        }

        #region Métodos Privados

        private bool ValidarFormatoDocumento(string tipo, string numero)
        {
            switch (tipo)
            {
                case "DNI":
                    // 8 dígitos numéricos
                    return System.Text.RegularExpressions.Regex.IsMatch(numero, @"^\d{8}$");

                case "RUC":
                    // 11 dígitos numéricos (empieza con 10, 15, 16, 17, 20)
                    return System.Text.RegularExpressions.Regex.IsMatch(numero, @"^(10|15|16|17|20)\d{9}$");

                case "CE":
                    // Carnet de extranjería: alfanumérico, mínimo 5 caracteres
                    return numero.Length >= 5 && System.Text.RegularExpressions.Regex.IsMatch(numero, @"^[a-zA-Z0-9]+$");

                default:
                    return false;
            }
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerCliente(int id)
        {
            var cliente = await _repo.GetByIdAsync(id);

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
        #endregion
    }

}
