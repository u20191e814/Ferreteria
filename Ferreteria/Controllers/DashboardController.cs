using Ferreteria.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ferreteria.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDashboardRepository _dashboardRepo;

        public DashboardController(IDashboardRepository dashboardRepo)
        {
            _dashboardRepo = dashboardRepo;
        }

        public async Task<IActionResult> Index()
        {
            var dashboard = await _dashboardRepo.GetDashboardAsync();
            return View(dashboard);
        }
    }
}
