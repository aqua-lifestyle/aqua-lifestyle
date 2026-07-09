using Microsoft.AspNetCore.Mvc;
using AqualLifeStyle.Controllers;

namespace AqualLifeStyle.Web.Host.Controllers
{
    public class HomeController : AqualLifeStyleControllerBase
    {
        public IActionResult Index()
        {
            return Redirect("/swagger");
        }
    }
}
