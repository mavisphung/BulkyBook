using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)] //phân quyền
    //phần phân quyền nằm trong controller
    public class CategoriesController : Controller
    {
        private readonly IUnitOfWork _unit;

        public CategoriesController(IUnitOfWork unit)
        {
            _unit = unit;
        }

        public async Task<IActionResult> Index(int catePage = 1)
        {
            CategoriesVM cateVM = new CategoriesVM
            {
                Categories = await _unit.Category.GetAllAsync(),
            };
            var count = cateVM.Categories.Count();
            cateVM.Categories = cateVM.Categories.OrderBy(p => p.Name)
                                                 .Skip((catePage - 1) * 2)
                                                 .Take(2).ToList();
            cateVM.PagingInfo = new PagingInfo
            {
                CurrentPage = catePage,
                ItemsPerPage = 2,
                TotalItems = count,
                UrlParam = "/Admin/Categories/Index?catePage=:"
            };
            return View(cateVM);
        }

        public async Task<IActionResult> Upsert(int? id) //upsert method = get
        {
            Category cate = new Category();
            if (id == null)
            {
                //thêm mới 1 category
                return View(cate); //trả về cate để insert
            }

            cate = await _unit.Category.GetAsync(id.GetValueOrDefault()); //phải dùng hàm GetValueOrDefault vì id có thể null
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
        public async Task<IActionResult> Upsert(Category category)
        {
            if (ModelState.IsValid)
            {
                if (category.Id == 0)
                {
                    /* Vì thiết kế theo repository pattern nên cần phải lưu ý
                     *      - SaveChanges() chỉ được chạy trong hàm update, những hàm liên quan đến Add, Remove
                     *        sẽ cần phải gọi hàm SaveChanges() từ UnitOfWork
                     */
                    await _unit.Category.AddAsync(category);
                    
                } else
                {
                    _unit.Category.Update(category);
                }
                _unit.Save();
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
        public async Task<IActionResult> GetAll()
        {
            var allObj = await _unit.Category.GetAllAsync();
            return Json(new { data = allObj });
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(int id)
        {
            var objFromDb = await _unit.Category.GetAsync(id);
            if (objFromDb == null)
            {
                TempData["Error"] = "Error deleting category!";
                return Json(new { success = false, message = "Error while deleting" });
            }
            await _unit.Category.RemoveAsync(id);
            _unit.Save();
            TempData["Success"] = "Successfully deleted category!";
            return Json(new { success = true, message = "Deleted successfully" });
        }
        #endregion


    }
}
