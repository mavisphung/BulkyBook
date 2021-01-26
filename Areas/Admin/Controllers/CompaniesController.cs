using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CompaniesController : Controller
    {
        private readonly IUnitOfWork _unit;
        public CompaniesController(IUnitOfWork unit)
        {
            _unit = unit;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Upsert(int? id)
        {
            Company comp = new Company();
            if (id == null)
            {
                //insert new company
                return View(comp);
            }

            comp = _unit.Company.Get(id.GetValueOrDefault());
            if (comp == null)
            {
                return NotFound();
            }
            //update
            return View(comp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(Company company)
        {
            if (ModelState.IsValid)
            {
                if (company.Id == 0)
                {
                    //insert
                    _unit.Company.Add(company);
                } else
                {
                    _unit.Company.Update(company);
                }
                _unit.Save();
                return RedirectToAction(nameof(Index));
            }
            return View(company);
        }

        #region
        [HttpGet]
        public IActionResult GetAll()
        {
            var allObjects = _unit.Company.GetAll();
            return Json(new { data = allObjects });
        }

        [HttpDelete]
        public IActionResult Delete(int id)
        {
            var objectFromDb = _unit.Company.Get(id);
            if (objectFromDb == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }
            _unit.Company.Remove(id);
            _unit.Save();
            return Json(new { success = true, message = "Deleted successfully" });
        }
        #endregion
    }
}
