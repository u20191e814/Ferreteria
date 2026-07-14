using Ferreteria.Entities;
using Ferreteria.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Crypto.Generators;

namespace Ferreteria.Controllers
{
    //[Authorize]
    //public class Usuarios : Controller
    //{
    //    public IActionResult Index()
    //    {
    //        return View();
    //    }
    //}

    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private readonly IUsuarioRepository _usuarioRepo;
        
        private readonly IRol _rol;
        public UsuariosController(IUsuarioRepository usuarioRepo, ILogger<UsuariosController> logger, IRol rol)
        {
            _usuarioRepo = usuarioRepo;
            
            _rol = rol; 
        }

        public async Task<IActionResult> Index(  string busqueda = null,  int? rol = null  )
        {
            
            var usuarios = await _usuarioRepo.GetAllAsync(busqueda, rol);

           
            var totalItems = usuarios?.Count() ?? 0;
              
            ViewBag.Busqueda = busqueda;
            ViewBag.Rol = rol;
            
             
            ViewBag.TotalItems = totalItems;


           
            ViewBag.Roles = await _rol.GetRol();

            return View(usuarios);
        }

        

        

        // GET: Usuarios/Create
        public async Task< IActionResult> Create()
        {
            ViewBag.Roles = await _rol.GetRol();
            
            return View();
        }

        // POST: Usuarios/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsuarioCreateDto dto)
        {
            ViewBag.Roles = await _rol.GetRol();
            if (!ModelState.IsValid)
            {
               
                return View(dto);
            }

            try
            {

                var existe = await _usuarioRepo.ExisteEmailAsync(dto.Email);
                if (existe)
                {
                    TempData["Error"] = "El email ya está registrado";
                    //ModelState.AddModelError("Email", "El email ya está registrado");
                 
                    return View(dto);
                }
                dto.PasswordHash = AE.Encrypt(dto.PasswordHash);
                int usuarioId = await _usuarioRepo.CreateAsync(dto);
                if (usuarioId >0 )
                {
                    TempData["Success"] = $"Usuario {dto.Nombre} {dto.Apellido} creado exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] = "No se pudo crear el usuario";
                    
                    return View(dto);
                }


            }
            catch (Exception ex)
            {

                TempData["Error"] = "Error al crear el usuario: " + ex.Message;
                
                return View(dto);
            }
        }

        // GET: Usuarios/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var usuario = await _usuarioRepo.GetByIdAsync(id);
           

            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado";
                return RedirectToAction(nameof(Index));
            }

            var dto = new UsuarioUpdateDto
            {
                Id = usuario.Id,
                Nombre = usuario.NombreCompleto?.Split(' ').FirstOrDefault() ?? "",
                Apellido = usuario.NombreCompleto?.Split(' ').Skip(1).FirstOrDefault() ?? "",
                Email = usuario.Email,
                RolId = usuario.RolId  , 
                Telefono =usuario.Telefono, 
                PasswordHash = AE.Decrypt( usuario.PasswordHash)
            };
            ViewBag.Roles = await _rol.GetRol();
            
            ViewBag.Rol = usuario.RolId;
            return View(dto);
        }

        // POST: Usuarios/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UsuarioUpdateDto dto)
        {
            ViewBag.Roles = await _rol.GetRol();
            if (id != dto.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                
                return View(dto);
            }

            try
            {
                var existe = await _usuarioRepo.ExisteEmailAsync(dto.Email, dto.Id);
               
                if (existe  )
                {
                    TempData["Error"] = "El email ya está registrado por otro usuario";
                    
                    return View(dto);
                }
                dto.PasswordHash = AE.Encrypt(dto.PasswordHash);
                var update = await _usuarioRepo.UpdateAsync(dto);
                if (update)
                {
                    TempData["Success"] = $"Usuario {dto.Nombre} {dto.Apellido} actualizado exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    TempData["Error"] =  "No se pudo actualizar el usuario"; 
                    return View(dto);
                }

            }
            catch (Exception ex)
            {

                TempData["Error"] =  "Error al actualizar el usuario: " + ex.Message;
               
                return View(dto);
            }
        }

        
        [HttpPost]
        public async Task<bool>  Delete(int id)
        {
            try
            {
                 

                var affected = await _usuarioRepo.DeleteAsync(id);

                if (affected)
                {
                    TempData["Success"] = "Usuario eliminado exitosamente";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar el usuario";
                }
            }
            catch (Exception ex)
            {
             
                TempData["Error"] = "Error al eliminar el usuario: " + ex.Message;
            }
            return true;
            
        }
    }
}
