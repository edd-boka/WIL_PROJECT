using Microsoft.AspNetCore.Mvc;
using Progress_DomainLayer.Models;
using Progress_ServiceLayer.CustomServices;
using Progress_ServiceLayer.ICustomServices;

namespace Progress_Report.Controllers
{
    public class MarksController : Controller
    {
        private readonly ICustomService<Mark> _markService;

        public MarksController(ICustomService<Mark> markService)
        {
            _markService = markService ?? throw new
              ArgumentNullException(nameof(markService));
        }
        public IActionResult Index()
        {
            var marks = _markService.GetAll();
            return View(marks);
        }
        public IActionResult Details(int Id)
        {
            var marks = _markService.Get(Id);
            return View(marks);
        }

        [HttpGet]
        public IActionResult Edit(int Id)
        {
            var marks = _markService.Get(Id);
            return View(marks);
        }

        [HttpPost]
        public IActionResult Edit(Mark mark)
        {
            _markService.Edit(mark);
            return RedirectToAction("Index");
        }
        [HttpGet]
        public IActionResult Create()
        {
            Mark mark = new Mark();
            return View(mark);
        }

        [HttpPost]
        public IActionResult Create(Mark mark)
        {
            _markService.Create(mark);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Delete(int Id)
        {
            var mark = _markService.Get(Id);
            return View(mark);
        }

        [HttpPost]
        public IActionResult Delete(Mark mark)
        {
            _markService.Delete(mark);
            return RedirectToAction("Index");
        }
    }
}
