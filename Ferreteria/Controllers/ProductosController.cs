using Ferreteria.DTOs;
using Ferreteria.Entities;
using Ferreteria.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Ferreteria.Controllers
{
    [Authorize]
    public class ProductosController : Controller
    {
        private readonly IProductoRepository _repo;
        private readonly ICategoriaRepository _categoriaRepo;
        private readonly IProveedorRepository _proveedorRepo;
        private readonly ILogger<ProductosController> _logger;
        public ProductosController(IProductoRepository repo, IProveedorRepository proveedorRepo,
        ICategoriaRepository categoriaRepo,
        ILogger<ProductosController> logger)
        {
            _repo = repo;
            _categoriaRepo = categoriaRepo;
            _proveedorRepo = proveedorRepo;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string search, int? categoriaId, int? proveedorId, int? estado, int page = 1, int pageSize = 20)
        {
           // var permisos = User.Claims.Where(c => c.Type == "Permisos").Select(c => c.Value).ToList();
            var productos = await _repo.GetAllAsync(search, categoriaId, proveedorId, estado, page, pageSize); 
            ViewBag.Categorias =  await _categoriaRepo.GetAllAsync(activo: true); ;
            ViewBag.categoriaId = categoriaId;
            ViewBag.ProveedorId = proveedorId;  
            ViewBag.Estado = estado;
            ViewBag.Page = page;
            ViewBag.Total = productos.Item2;
            ViewBag.PageSize = pageSize;
            ViewBag.Proveedores = await _proveedorRepo.GetAllAsync();
            return View(productos.Item1);
        }

        public async Task<IActionResult> StockBajo(string search, int? categoriaId, int? proveedorId, int? estado, int page = 1, int pageSize = 20)
        {
            var productos = await _repo.GetStockBajoAsync(search, categoriaId, proveedorId, estado, page, pageSize);
            var categorias = await _categoriaRepo.GetAllAsync(activo: true);
            ViewBag.Categorias = categorias;
            ViewBag.categoriaId = categoriaId;
            
            ViewBag.ProveedorId = proveedorId;
            ViewBag.Estado = estado;
            ViewBag.Page = page;
            ViewBag.Total = productos.Item2;
            ViewBag.PageSize = pageSize;
            ViewBag.Proveedores = await _proveedorRepo.GetAllAsync();
            return View("Index", productos.Item1);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductoCreateDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            await _repo.CreateAsync(dto, usuarioId);
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Create()
        {
            // Cargar unidades de medida desde BD
            var unidades = await _repo.GetUnidadesMedidaAsync();
            ViewBag.UnidadesMedida = unidades;
            var categorias = await _categoriaRepo.GetAllAsync(activo: true);
            ViewBag.Categorias = categorias;
            return View(new ProductoCreateDto());
        }
         

       
        [HttpGet]
        public async Task<IActionResult> BuscarProveedores(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
            {
                return Json(new List<ProveedorDto>());
            }

            var proveedores = await _proveedorRepo.GetAllAsync(texto);
            return Json(proveedores);
        }

       
        [HttpPost]
        public async Task<IActionResult> CrearProveedorRapido([FromBody] ProveedorCreateDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Nombre) || dto.Nombre.Length < 2)
                {
                    return BadRequest(new { error = "Nombre muy corto" });
                }

                if (await _proveedorRepo.ExisteNombreAsync(dto.Nombre))
                {
                    return BadRequest(new { error = "Ya existe un proveedor con ese nombre" });
                }

                var id = await _proveedorRepo.CreateAsync(dto.Nombre);
                return Json(new { id, nombre = dto.Nombre, success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando proveedor rápido");
                return StatusCode(500, new { error = "Error al crear proveedor" });
            }
        }

         
        [HttpGet]
        public async Task<IActionResult> ObtenerProveedor(long id)
        {
            var proveedor = await _proveedorRepo.GetByIdAsync(id);
            if (proveedor == null) return NotFound();
            return Json(proveedor);
        }


       
        [HttpGet]
        public async Task<IActionResult> BuscarCategorias(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
            {
                
                var todas = await _categoriaRepo.GetAllAsync(activo: true);
                return Json(todas);
            }

            var categorias = await _categoriaRepo.GetAllAsync(busqueda: texto, activo: true);
            return Json(categorias);
        }

        
        [HttpGet]
        public async Task<IActionResult> ObtenerCategoria(int id)
        {
            var categoria = await _categoriaRepo.GetByIdAsync(id);
            if (categoria == null) return NotFound();
            return Json(categoria);
        }

         
        [HttpPost]
        public async Task<IActionResult> CrearCategoriaRapida([FromBody] CategoriaCreateDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Nombre) || dto.Nombre.Length < 2)
                {
                    return BadRequest(new { error = "El nombre debe tener al menos 2 caracteres" });
                }

                if (await _categoriaRepo.ExisteNombreAsync(dto.Nombre))
                {
                    return BadRequest(new { error = "Ya existe una categoría con ese nombre" });
                }

                var id = await _categoriaRepo.CreateAsync(dto);
                return Json(new { id, nombre = dto.Nombre, success = true });
            }
            catch (Exception ex)
            {
                
                return StatusCode(500, new { error = "Error al crear categoría" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditarCategoria([FromBody] CategoriaDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Nombre) || dto.Nombre.Length < 2)
                {
                    return BadRequest(new { error = "El nombre debe tener al menos 2 caracteres" });
                }

                // Verificar que existe
                var existente = await _categoriaRepo.GetByIdAsync(dto.Id);
                if (existente == null)
                {
                    return NotFound(new { error = "Categoría no encontrada" });
                }

                // Verificar nombre duplicado
                if (await _categoriaRepo.ExisteNombreAsync(dto.Nombre, dto.Id))
                {
                    return BadRequest(new { error = "Ya existe otra categoría con ese nombre" });
                }

                var resultado = await _categoriaRepo.UpdateAsync(dto);
                if (resultado)
                {
                    return Json(new
                    {
                        success = true,
                        id = dto.Id,
                        nombre = dto.Nombre,
                        descripcion = dto.Descripcion,
                        message = "Categoría actualizada correctamente"
                    });
                }

                return BadRequest(new { error = "No se pudo actualizar la categoría" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editando categoría {CategoriaId}", dto.Id);
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

         
        public async Task<IActionResult> EliminarCategoria(  int id)
        {
            try
            {
                // Verificar que existe
                var categoria = await _categoriaRepo.GetByIdAsync(id);
                if (categoria == null)
                {
                    return NotFound(new { error = "Categoría no encontrada" });
                }

                // Verificar si tiene productos asociados
                var cantidadProductos = await _categoriaRepo.TieneProductosAsync(id);
                if (cantidadProductos > 0)
                {
                    return BadRequest(new
                    {
                        error = $"No se puede eliminar. Tiene {cantidadProductos} producto(s) asociado(s).",
                        tieneProductos = true,
                        cantidadProductos
                    });
                }

                var resultado = await _categoriaRepo.DeleteAsync(id);
                if (resultado)
                {
                    return Json(new
                    {
                        success = true,
                        message = "Categoría eliminada correctamente",
                        id
                    });
                }

                return BadRequest(new { error = "No se pudo eliminar la categoría" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando categoría {CategoriaId}", id);
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategoriasParaGestion()
        {
            try
            {
                var categorias = await _categoriaRepo.GetAllAsync(activo: true);
                var categoriasConConteo = new List<object>();

                foreach (var cat in categorias)
                {
                    var cantidad = await _categoriaRepo.TieneProductosAsync(cat.Id);
                    categoriasConConteo.Add(new
                    {
                        cat.Id,
                        cat.Nombre,
                        cat.Descripcion,
                        CantidadProductos = cantidad,
                        PuedeEliminar = cantidad == 0
                    });
                }

                return Json(categoriasConConteo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo categorías para gestión");
                return StatusCode(500, new { error = "Error al cargar categorías" });
            }
        }


        // GET: /Productos/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var producto = await _repo.GetByIdAsync(id);
            if (producto == null)
            {
                TempData["Error"] = "Producto no encontrado";
                return RedirectToAction(nameof(Index));
            }

            
            var unidades = await _repo.GetUnidadesMedidaAsync();
            var categorias = await _categoriaRepo.GetAllAsync(activo: true);

            ViewBag.UnidadesMedida = unidades;
            ViewBag.Categorias = categorias;

            // Mapear a DTO de edición
            var dto = new ProductoUpdateDto
            {
                Id = producto.Id,
                Codigo = producto.Codigo,
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                CategoriaId = producto.CategoriaId,
                UnidadMedidaId = producto.UnidadMedidaId,
                PrecioCompra = producto.PrecioCompra,
                PrecioVenta = producto.PrecioVenta,
                StockMinimo = producto.StockMinimo,
                StockActual = producto.StockActual,
                UbicacionAlmacen = producto.UbicacionAlmacen,
                idProveedor = producto.idProveedor,
                Activo = producto.Activo,
                FechaCreacion = producto.FechaCreacion, 
                UnidadMedidaNombre = producto.UnidadMedidaNombre,
                CategoriaNombre = producto.catNombre, 
             
               ProveedorNombre = producto.Proveedor
            };

            return View(dto);
        }

        // POST: /Productos/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductoUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
               
                ViewBag.UnidadesMedida = await _repo.GetUnidadesMedidaAsync();
                ViewBag.Categorias = await _categoriaRepo.GetAllAsync(activo: true);
                return View(dto);
            }

            
            if (await _repo.ExisteCodigoAsync(dto.Codigo, dto.Id))
            {
                ModelState.AddModelError("Codigo", "Ya existe otro producto con este código");
                ViewBag.UnidadesMedida = await _repo.GetUnidadesMedidaAsync();
                ViewBag.Categorias = await _categoriaRepo.GetAllAsync(activo: true);
                return View(dto);
            }

            try
            {
                var actualizado = await _repo.UpdateAsync(dto);
                if (actualizado)
                {
                     
                    TempData["Success"] = $"Producto '{dto.Nombre}' actualizado exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] = "No se pudo actualizar el producto";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                 
                ModelState.AddModelError("", "Ocurrió un error al actualizar");
                ViewBag.UnidadesMedida = await _repo.GetUnidadesMedidaAsync();
                ViewBag.Categorias = await _categoriaRepo.GetAllAsync(activo: true);
                return View(dto);
            }
        }

       

        // GET: /Productos/Delete/5 (Vista de confirmación)
        public async Task<IActionResult> Delete(int id)
        {
            var producto = await _repo.GetDetailByIdAsync(id);
            if (producto == null)
            {
                TempData["Error"] = "Producto no encontrado";
                return RedirectToAction(nameof(Index));
            }

            
            ViewBag.TieneMovimientos = await _repo.TieneMovimientosAsync(id);
            ViewBag.TieneVentas = await _repo.TieneVentasAsync(id);

            return View(producto);
        }

        
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var producto = await _repo.GetByIdAsync(id);
                if (producto == null)
                {
                    TempData["Error"] = "Producto no encontrado";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar dependencias críticas
                var tieneVentas = await _repo.TieneVentasAsync(id);
                if (tieneVentas)
                {
                    TempData["Error"] = "No se puede eliminar el producto porque tiene ventas asociadas. Se recomienda desactivarlo.";
                    return RedirectToAction(nameof(Delete), new { id });
                }

                var eliminado = await _repo.DeleteAsync(id);
                if (eliminado)
                {
                    
                    TempData["Success"] = $"Producto '{producto.Nombre}' eliminado exitosamente";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar el producto";
                }
            }
            catch (Exception ex)
            {
                
                TempData["Error"] = "Ocurrió un error al eliminar el producto";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Productos/Desactivar/5 (Desactivar en lugar de eliminar)
        [HttpPost]
        public async Task<IActionResult> Desactivar(int id)
        {
            try
            {
                var producto = await _repo.GetByIdAsync(id);
                if (producto == null) return NotFound();

                // Actualizar solo el estado Activo
                var dto = new ProductoUpdateDto
                {
                    Id = id,
                    Codigo = producto.Codigo,
                    Nombre = producto.Nombre,
                    Descripcion = producto.Descripcion,
                    CategoriaId = producto.CategoriaId,
                    UnidadMedidaId = producto.UnidadMedidaId,
                    PrecioCompra = producto.PrecioCompra,
                    PrecioVenta = producto.PrecioVenta,
                    StockMinimo = producto.StockMinimo,
                    UbicacionAlmacen = producto.UbicacionAlmacen,
                    idProveedor = producto.idProveedor,
                    Activo = false  // Desactivar
                };

                var actualizado = await _repo.UpdateAsync(dto);
                if (actualizado)
                {
                    
                    return Json(new { success = true, message = "Producto desactivado" });
                }
                return Json(new { success = false, message = "No se pudo desactivar" });
            }
            catch (Exception ex)
            {
                 
                return Json(new { success = false, message = "Error del servidor" });
            }
        }

       
        [HttpGet]
        public async Task<IActionResult> ValidarCodigo(string codigo, int? excluirId = null)
        {
            if (string.IsNullOrEmpty(codigo))
                return Json(new { valido = false, mensaje = "Código requerido" });

            var existe = await _repo.ExisteCodigoAsync(codigo, excluirId);
            return Json(new { valido = !existe, mensaje = existe ? "Código ya existe" : "Código disponible" });
        }


       
        [HttpPost]
       
        public async Task<IActionResult> AjustarStock([FromBody]  AjusteStockDto dto)
        {
            try
            {
                var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

           
                if (dto.NuevoStock < 0)
                {
                    return BadRequest(new { success = false, message = "El stock no puede ser negativo" });
                }

                if (string.IsNullOrWhiteSpace(dto.Motivo) || dto.Motivo.Length < 5)
                {
                    return BadRequest(new { success = false, message = "Debe indicar un motivo (mínimo 5 caracteres)" });
                }

                var resultado = await _repo.AjustarStockAsync(dto, usuarioId);

                if (resultado.Success)
                {
                    _logger.LogInformation("Stock ajustado: Producto {ProductoId}, de {Anterior} a {Nuevo}",
                        dto.ProductoId, resultado.StockAnterior, resultado.StockNuevo);
                }

                return Json(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ajustando stock del producto {ProductoId}", dto.ProductoId);
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        // GET: /Productos/GetHistorialMovimientos/5?limite=10
        [HttpGet]
        public async Task<IActionResult> GetHistorialMovimientos(int id, int? limite = 10)
        {
            try
            {
                var movimientos = await _repo.GetHistorialMovimientosAsync(id, limite);
                return Json(movimientos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo historial del producto {ProductoId}", id);
                return StatusCode(500, new { error = "Error al cargar historial" });
            }
        }
        
        public async Task<IActionResult> Details(int id)
        {
            var producto = await _repo.GetDetailByIdAsync(id);
            if (producto == null)
            {
                TempData["Error"] = "Producto no encontrado";
                return RedirectToAction(nameof(Index));
            }

            // Cargar historial de movimientos
            var historial = await _repo.GetHistorialMovimientosAsync(id, 10);
            ViewBag.HistorialMovimientos = historial;

            return View(producto);
        }
    }

}
