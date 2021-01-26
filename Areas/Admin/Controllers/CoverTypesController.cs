using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Utility;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CoverTypesController : Controller
    {
        private readonly IUnitOfWork _unit;

        public CoverTypesController(IUnitOfWork unit)
        {
            _unit = unit;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Upsert(int? id)
        {
            CoverType coverType = new CoverType();
            if (id == null)
            {
                return View(coverType);
            }
            //Bình thường
            //coverType = _unit.CoverType.Get(id.GetValueOrDefault());
            //Dùng stored procedure
            var parameter = new DynamicParameters();
            parameter.Add("@Id", id);
            coverType = _unit.SP_Call.OneRecord<CoverType>(SD.Proc_CoverType_Get, parameter);
            if (coverType == null)
            {
                return NotFound();
            }
            return View(coverType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(CoverType coverType)
        {
            if (ModelState.IsValid)
            {
                var parameter = new DynamicParameters();
                parameter.Add("@Name", coverType.Name);

                if (coverType.Id == 0)
                {
                    //_unit.CoverType.Add(coverType);
                    //Vì chỉnh auto generate id nên khi add vào database không cần kèm ID
                    _unit.SP_Call.Execute(SD.Proc_CoverType_Create, parameter);
                    _unit.Save();
                } else
                {
                    //Do procedure có tham số Id để biết được object cần update
                    parameter.Add("@Id", coverType.Id);
                    _unit.SP_Call.Execute(SD.Proc_CoverType_Update, parameter);
                    _unit.CoverType.Update(coverType);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(coverType);
        }

        #region API Calls
        [HttpGet]
        public IActionResult GetAll()
        {
            //Bình thường
            //var objsFromDb = _unit.CoverType.GetAll();
            //Dùng procedure
            var objsFromDb = _unit.SP_Call.List<CoverType>(SD.Proc_CoverType_GetAll, null); //do không cần gán tham số nên tham số cuối để null
            return Json(new { data = objsFromDb });
        }

        [HttpDelete]
        public IActionResult Delete(int id)
        {
            //bình thường
            //var objFromDb = _unit.CoverType.Get(id);
            //Dùng procedure
            var parameter = new DynamicParameters();
            //Nếu không biết truyền tham số gì thì vào lại migration AddStoredProcedureToDb/Up để tìm tham số
            //nhận biết tham số bằng @
            //Viết giống y chang trong procedure thì nó mới bắt được đúng tham số truyền vào
            parameter.Add("@Id", id);
            var objFromDb = _unit.SP_Call.OneRecord<CoverType>(SD.Proc_CoverType_Get, parameter);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }
            //_unit.CoverType.Remove(objFromDb);
            _unit.SP_Call.Execute(SD.Proc_CoverType_Delete, parameter);
            _unit.Save();
            return Json(new { success = true, message = "Deleted successfully" });
        }
        #endregion
    }
}
