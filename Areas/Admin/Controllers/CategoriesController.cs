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
    public class CategoriesController : Controller
    {
        private readonly IUnitOfWork _unit;

        public CategoriesController(IUnitOfWork unit)
        {
            _unit = unit;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Upsert(int? id) //upsert method = get
        {
            Category cate = new Category();
            if (id == null)
            {
                //thêm mới 1 category
                return View(cate); //trả về cate để insert
            }

            cate = _unit.Category.Get(id.GetValueOrDefault()); //phải dùng hàm GetValueOrDefault vì id có thể null
            if (cate == null)
            {
                //tìm không thấy cate trong db thì trả về notfound, khác với thêm mới
                return NotFound();
            }
            return View(cate); //trả về cate để update
        }

        //Mặc định ASP.Net core đã giúp mình validate form.
        //nhưng để cho chắc chắn, thêm vào data annotation ValidateAntiForgeryToken để
        //lỡ nếu ASP.NET core có bỏ qua thì dưới database sẽ check giùm
        //nghĩa là double check

        [HttpPost]
        [ValidateAntiForgeryToken] //chặn request giả mảo (forgery) từ form submit lên server từ những trang khác
        public IActionResult Upsert(Category category)
        {
            if (ModelState.IsValid)
            {
                if (category.Id == 0)
                {
                    /* Vì thiết kế theo repository pattern nên cần phải lưu ý
                     *      - SaveChanges() chỉ được chạy trong hàm update, những hàm liên quan đến Add, Remove
                     *        sẽ cần phải gọi hàm SaveChanges() từ UnitOfWork
                     */
                    _unit.Category.Add(category);
                    _unit.Save();
                } else
                {
                    _unit.Category.Update(category);
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        /* Gọi API là đúng ?? :D ??
         * 1. API luôn nằm trong controller
         * 2. Đối với trang razor (không theo mô hình mvc) nên phải tạo thêm 1 controller chứa API
         * 3. Đối với mô hình mvc việc tạo 1 api có thể nằm ngay trong file controller, không cần phải tách ra như razor page
         */

        #region API Calls
        [HttpGet]
        public IActionResult GetAll()
        {
            var allObj = _unit.Category.GetAll();
            return Json(new { data = allObj });
        }

        [HttpDelete]
        public IActionResult Delete(int id)
        {
            var objFromDb = _unit.Category.Get(id);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }
            _unit.Category.Remove(id);
            _unit.Save();
            return Json(new { success = true, message = "Deleted successfully" });
        }
        #endregion


    }
}
