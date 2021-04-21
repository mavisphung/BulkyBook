using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace BulkyBook.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProductsController : Controller
    {
        private readonly IUnitOfWork _unit;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductsController(IUnitOfWork unit, IWebHostEnvironment hostEnvironment)
        {
            _unit = unit;
            _hostEnvironment = hostEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Upsert(int? id) //upsert method = get
        {
            IEnumerable<Category> cateList = await _unit.Category.GetAllAsync();
            ProductVM productVM = new ProductVM()
            {
                Product = new Product(),
                CategoryList = cateList.Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                }),
                CoverTypeList = _unit.CoverType.GetAll().Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                })
            };
            if (id == null)
            {
                //trả về 1 ProductVM
                return View(productVM);
            }
            //edit
            productVM.Product = _unit.Product.Get(id.GetValueOrDefault()); //phải dùng hàm GetValueOrDefault vì id có thể null
            if (productVM.Product == null)
            {
                //tìm không thấy product trong productVM trong db thì trả về notfound, khác với thêm mới
                return NotFound();
            }
            return View(productVM); //trả về cate để update
        }

        //Mặc định ASP.Net core đã giúp mình validate form.
        //nhưng để cho chắc chắn, thêm vào data annotation ValidateAntiForgeryToken để
        //lỡ nếu ASP.NET core có bỏ qua thì dưới database sẽ check giùm
        //nghĩa là double check

        [HttpPost]
        [ValidateAntiForgeryToken] //chặn request giả mạo (forgery) từ form submit lên server từ những trang khác
        public async Task<IActionResult> Upsert(ProductVM productVM)
        {
            if (ModelState.IsValid)
            {

                //lấy dường dẫn của thằng wwwroot
                string webRootPath = _hostEnvironment.WebRootPath;

                //lấy files collection từ request gửi đến
                var files = HttpContext.Request.Form.Files;
                if (files.Count > 0) //nếu lấy được file
                {
                    string fileName = Guid.NewGuid().ToString();
                    var uploads = Path.Combine(webRootPath, @"images\products"); //lấy đường dẫn của thư mục được chứa ảnh
                    var extension = Path.GetExtension(files[0].FileName); //lấy đuôi của cái file gửi lên

                    if (productVM.Product.ImageUrl != null)
                    {
                        //edit thì cần phải xóa cái image cũ đi
                        var imagePath = Path.Combine(webRootPath, productVM.Product.ImageUrl.TrimStart('\\')); //lấy đường dẫn ảnh cũ
                        if (System.IO.File.Exists(imagePath))
                        {
                            //nếu tấm ảnh cũ có tồn tại thì xóa đi
                            System.IO.File.Delete(imagePath);
                        }
                    }
                    using (var fileStreams = new FileStream(Path.Combine(uploads, fileName + extension), FileMode.Create))
                    {
                        files[0].CopyTo(fileStreams);
                    }
                    productVM.Product.ImageUrl = @"\images\products\" + fileName + extension;
                } else
                {
                    //update when they do not change the img
                    if (productVM.Product.Id != 0)
                    {
                        Product objectFromDb = _unit.Product.Get(productVM.Product.Id);
                        productVM.Product.ImageUrl = objectFromDb.ImageUrl;
                    }
                }

                if (productVM.Product.Id == 0)
                {
                    /* Vì thiết kế theo repository pattern nên cần phải lưu ý
                     *      - SaveChanges() chỉ được chạy trong hàm update, những hàm liên quan đến Add, Remove
                     *        sẽ cần phải gọi hàm SaveChanges() từ UnitOfWork
                     */
                    _unit.Product.Add(productVM.Product);
                }
                else
                {
                    _unit.Product.Update(productVM.Product); //cẩn thận chỗ này, vì trong ProductRepository không có saveChanges() cuối cùng
                }
                _unit.Save();
                return RedirectToAction(nameof(Index));
            } 
            else
            {
                //Nếu không có phần partial Validate nằm bên trang upsert.cshtml
                //thì asp.net core sẽ validate ở server side
                //nếu có phần đó thì sẽ validate ở client side

                //-----
                //Giả sử trường hợp dev không validate bên client side
                //Khi nhận được đối tượng ProductVM sẽ gây ra lỗi là CategoryId không thể convert sang IEnumrable<SelectListItem>
                //=> Cần phải truy xuất lại danh sách Categories và CoverTypes dưới Db
                //gán vào productVM.CategoryList và productVM.CoverTypeList để tránh gây lỗi
                //Phần code đấy có thể copy từ dòng 37 đến dòng 46 ngay tại controller này
                //và xử lí ngay tại chỗ else của ModelState.IsValid trước khi return về trang upsert
                IEnumerable<Category> categories = await _unit.Category.GetAllAsync();
                productVM.CategoryList = categories.Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                });
                productVM.CoverTypeList = _unit.CoverType.GetAll().Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                });

                //dành cho update
                if (productVM.Product.Id != 0)
                {
                    productVM.Product = _unit.Product.Get(productVM.Product.Id);
                }
            }
            return View(productVM);
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
            var allObj = _unit.Product.GetAll(includeProperties:"Category,CoverType");
            return Json(new { data = allObj });
        }

        [HttpDelete]
        public IActionResult Delete(int id)
        {
            var objFromDb = _unit.Product.Get(id);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            //xóa link ảnh nằm trong database
            string root = _hostEnvironment.WebRootPath;
            if (objFromDb.ImageUrl != null) //tức là object này có chứa ảnh
            {
                string imagePath = Path.Combine(root, objFromDb.ImageUrl.TrimStart('\\'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            //xóa object trong database
            _unit.Product.Remove(id);
            _unit.Save();
            return Json(new { success = true, message = "Deleted successfully" });
        }
        #endregion


    }
}
