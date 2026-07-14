using Dapper; 
using Ferreteria.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens; 
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ferreteria.Controllers
{
    public class AuthController : Controller
    {

        private IConfiguration _configuration;
        private readonly string _connectionString;
        public AuthController(  IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login()
        {
            
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Dashboard");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginRequestDto model)
        {
            try
            {
                if (!ModelState.IsValid) return View(model);

                using (SqlConnection cn =new(_connectionString))
                {
                    const string query = @" SELECT u.Id, u.Nombre, u.Apellido, u.Email, u.PasswordHash, r.Nombre as Rol, r.Permisos
                        FROM [TiendaDB].[dbo].Usuarios u INNER JOIN [TiendaDB].[dbo].Roles r ON u.RolId = r.Id
                        WHERE u.Email = @Email AND u.Activo = 1";


                    var usuario = await cn.QueryFirstOrDefaultAsync<dynamic>(query, new { Email = model.Email });

                    if (usuario == null || !string.Equals(AE.Encrypt(model.Password), (string)usuario.PasswordHash))
                    {
                        ModelState.AddModelError("", "Credenciales inválidas");
                        return View(model);
                    }

                    // Actualizar último acceso
                    const string updateAcceso = "UPDATE [TiendaDB].[dbo].Usuarios SET UltimoAcceso = GETDATE() WHERE Id = @Id";
                    await cn.ExecuteAsync(updateAcceso, new { Id = (int)usuario.Id });

                    // Generar JWT
                    var token = GenerateJwtToken(usuario);

                    // Guardar en cookie
                    Response.Cookies.Append("AuthToken", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTime.Now.AddHours(8)
                    });
                }
                

               
            }
            catch (Exception ex)
            {
 
            }
            return RedirectToAction("Index", "Dashboard");
        }

        private string GenerateJwtToken(dynamic usuario)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, ((int)usuario.Id).ToString()),
                new Claim(ClaimTypes.Name, $"{usuario.Nombre} {usuario.Apellido}"),
                new Claim(ClaimTypes.Email, (string)usuario.Email),
                new Claim(ClaimTypes.Role, (string)usuario.Rol),
                new Claim("Permisos", (string)usuario.Permisos)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(8),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public IActionResult Logout()
        {
            Response.Cookies.Delete("AuthToken");
            return RedirectToAction("Login");
        }
    }
}
