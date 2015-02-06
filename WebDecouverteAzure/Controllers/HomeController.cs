using System.Web.Mvc;
using WebDecouverteAzure.Models;
using WebDecouverteAzure.Services;

namespace WebDecouverteAzure.Controllers
{
    public class HomeController : Controller
    {
        private readonly ParametrageService _parametrageService;
        public HomeController()
        {
            _parametrageService = new ParametrageService();
        }
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Parametrage()
        {
            var model = new ParametrageModel
            {
                Duration = _parametrageService.LoadDuration(),
            };
            return View(model);
        }

        [HttpPost]
        public ActionResult Parametrage(ParametrageModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(new ParametrageModel
                {
                    Duration = model.Duration,
                    Result = "La durée n'est pas valide"
                });
            }

            _parametrageService.SaveDuration(model.Duration);
            model.Result = "Paramétrage sauvegardé";

            return View(model);
        }
    }
}