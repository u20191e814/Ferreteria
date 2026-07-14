using Ferreteria.DTOs;
using Ferreteria.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Controllers
{
    [Authorize]
    public class ProveedoresController : Controller
    {
        private readonly IProveedorRepository _repo;
        private readonly ILogger<ProveedoresController> _logger;

        public ProveedoresController(IProveedorRepository repo, ILogger<ProveedoresController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        
        public async Task<IActionResult> Index(string busqueda)
        {
            var proveedores = await _repo.GetAllAsync(busqueda);
            if (proveedores!=null && proveedores.Count() >0  )
            {
                foreach (var item in proveedores)
                {
                    item.CantidadProductos = await _repo.GetCantidadProductosAsync(item.Id);
                }
            }
            ViewBag.Busqueda = busqueda;

            return View(proveedores);
        }

        
        public IActionResult Create()
        {
            return View(new ProveedorCreateDto());
        }

     
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProveedorCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }
 
            if (await _repo.ExisteNombreAsync(dto.Nombre))
            {
                ModelState.AddModelError("Nombre", "Ya existe un proveedor con este nombre");
                return View(dto);
            }

            try
            {
                var id = await _repo.CreateAsync(dto);
                _logger.LogInformation("Proveedor creado: {ProveedorId} - {Nombre}", id, dto.Nombre);

                TempData["Success"] = $"Proveedor '{dto.Nombre}' creado exitosamente";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando proveedor");
                ModelState.AddModelError("", "Ocurrió un error al guardar el proveedor");
                return View(dto);
            }
        }

       
        public async Task<IActionResult> Edit(long id)
        {
            var proveedor = await _repo.GetByIdAsync(id);
            if (proveedor == null)
            {
                TempData["Error"] = "Proveedor no encontrado";
                return RedirectToAction(nameof(Index));
            }

            
            var cantidadProductos = await _repo.GetCantidadProductosAsync(id);
            ViewBag.CantidadProductos = cantidadProductos;

            var dto = new ProveedorUpdateDto
            {
                Id = proveedor.Id,
                Nombre = proveedor.Nombre
            };

            return View(dto);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProveedorUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CantidadProductos = await _repo.GetCantidadProductosAsync(dto.Id);
                return View(dto);
            }

             
            var existente = await _repo.GetByIdAsync(dto.Id);
            if (existente == null)
            {
                TempData["Error"] = "Proveedor no encontrado";
                return RedirectToAction(nameof(Index));
            }

            
            if (await _repo.ExisteNombreAsync(dto.Nombre, dto.Id))
            {
                ModelState.AddModelError("Nombre", "Ya existe otro proveedor con este nombre");
                ViewBag.CantidadProductos = await _repo.GetCantidadProductosAsync(dto.Id);
                return View(dto);
            }

            try
            {
                var actualizado = await _repo.UpdateAsync(dto);
                if (actualizado)
                {
                    _logger.LogInformation("Proveedor actualizado: {ProveedorId}", dto.Id);
                    TempData["Success"] = $"Proveedor '{dto.Nombre}' actualizado exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] = "No se pudo actualizar el proveedor";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando proveedor {ProveedorId}", dto.Id);
                ModelState.AddModelError("", "Ocurrió un error al actualizar");
                ViewBag.CantidadProductos = await _repo.GetCantidadProductosAsync(dto.Id);
                return View(dto);
            }
        }

        // GET: /Proveedores/Delete/5
        public async Task<IActionResult> Delete(long id)
        {
            var proveedor = await _repo.GetByIdAsync(id);
            if (proveedor == null)
            {
                TempData["Error"] = "Proveedor no encontrado";
                return RedirectToAction(nameof(Index));
            }

            // Verificar dependencias
            var cantidadProductos = await _repo.GetCantidadProductosAsync(id);
            ViewBag.CantidadProductos = cantidadProductos;
            ViewBag.PuedeEliminar = cantidadProductos == 0;

            return View(proveedor);
        }

        // POST: /Proveedores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            try
            {
                var proveedor = await _repo.GetByIdAsync(id);
                if (proveedor == null)
                {
                    TempData["Error"] = "Proveedor no encontrado";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que no tenga productos
                if (await _repo.TieneProductosAsync(id))
                {
                    TempData["Error"] = "No se puede eliminar el proveedor porque tiene productos asociados";
                    return RedirectToAction(nameof(Delete), new { id });
                }

                var eliminado = await _repo.DeleteAsync(id);
                if (eliminado)
                {
                    _logger.LogInformation("Proveedor eliminado: {ProveedorId} - {Nombre}", id, proveedor.Nombre);
                    TempData["Success"] = $"Proveedor '{proveedor.Nombre}' eliminado exitosamente";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar el proveedor";
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando proveedor {ProveedorId}", id);
                TempData["Error"] = "Ocurrió un error al eliminar el proveedor";
            }

            return RedirectToAction(nameof(Index));
        }

         
        [HttpGet]
        public async Task<IActionResult> Buscar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
            {
                var todos = await _repo.GetAllAsync();
                return Json(todos.Select(p => new { p.Id, p.Nombre }));
            }

            var proveedores = await _repo.GetAllAsync(texto);
            return Json(proveedores.Select(p => new { p.Id, p.Nombre }));
        }

        // GET: /Proveedores/Obtener/5
        [HttpGet]
        public async Task<IActionResult> Obtener(long id)
        {
            var proveedor = await _repo.GetByIdAsync(id);
            if (proveedor == null) return NotFound();
            return Json(proveedor);
        }

        // GET: /Proveedores/ValidarNombre?nombre=xxx&excluirId=5
        [HttpGet]
        public async Task<IActionResult> ValidarNombre(string nombre, long? excluirId = null)
        {
            if (string.IsNullOrEmpty(nombre))
                return Json(new { valido = false, mensaje = "Nombre requerido" });

            var existe = await _repo.ExisteNombreAsync(nombre, excluirId);
            return Json(new { valido = !existe, mensaje = existe ? "Nombre ya existe" : "Nombre disponible" });
        }
    }
}
