using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Customer.Controllers
{
    [Area("Customer")] //phân quyền
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unit;

        public HomeController(ILogger<HomeController> logger, IUnitOfWork unit)
        {
            _logger = logger;
            _unit = unit;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> productList = _unit.Product.GetAll(includeProperties: "Category,CoverType");

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            if (claim != null)
            {
                //nếu user đã log in
                var count = _unit.ShoppingCart.GetAll(cart => cart.AppUserId == claim.Value)
                    .ToList().Count();
                HttpContext.Session.SetInt32(SD.ssShoppingCart, count);

            }

            return View(productList);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Details(int? id)
        {
            var bookFromDb = _unit.Product
                .GetFirstOrDefault(u => u.Id == id, includeProperties: "Category,CoverType");
            ShoppingCart cartObject = new ShoppingCart()
            {
                Product = bookFromDb,
                ProductId = bookFromDb.Id
            };
            return View(cartObject);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Details(ShoppingCart CartObject)
        {
            CartObject.Id = 0; //dòng này để chắc chắn rằng không có cart nào trùng khi được khởi tạo new mới
            if (ModelState.IsValid)
            {
                //nếu mọi validation đều thỏa mãn thì sẽ add vào cart
                //Quá trình để add vào cart được thực hiện như sau
                //1. Lấy được id của user đang log in vào trang web và gán vào thuộc tính AppUserId của cart
                var claimsIdentity = (ClaimsIdentity) User.Identity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                CartObject.AppUserId = claim.Value;

                //2. Lấy cái cart trong database với điều kiện là
                //      AppUserId và ProductId trong CartObject (tham số) trùng với AppUserId và ProductId trong CartFromDb
                ShoppingCart CartFromDb = _unit.ShoppingCart.GetFirstOrDefault(
                    u => u.AppUserId == CartObject.AppUserId && u.ProductId == CartObject.ProductId,
                    includeProperties: "Product" //lấy cái cart và mapping cả Product từ Db từ thuộc tính ProductId
                    );
                
                if (CartFromDb == null)
                {
                    //trường hợp người dùng không có add sản phẩm nào cart
                    //thì add cái cart mới vào
                    _unit.ShoppingCart.Add(CartObject);
                    
                }
                else
                {
                    //Cập nhật số lượng được truyền vào
                    CartFromDb.Count += CartObject.Count;
                    //dòng dưới có thể không cần vì mình đã lấy được địa chỉ của cái cart lưu trong biến CartFromDb
                    //do đó entity framework tự động track về id đã được lưu trong db và cập nhật lại
                    //cập nhật được là nhờ dòng SaveChanges() của DbContext
                    //_unit.ShoppingCart.Update(CartFromDb);
                }
                _unit.Save();

                //lấy số lượng sản phẩm có trong cart của user
                var count = _unit.ShoppingCart
                    .GetAll(u => u.AppUserId == CartObject.AppUserId)
                    .ToList().Count();

                //set số lượng lên Session mà mình đã đăng ký bên startup
                //Có nhiều cách set như set object, set kiểu int, set 1 reference type
                //Tùy theo mục đích mình sử dụng thì có các phương thức đi kèm
                //Set số => SetInt32, SetFloat, SetDouble
                //set object => SetObject đã được implement ở 
                HttpContext.Session.SetInt32(SD.ssShoppingCart, count);

                //để lấy dữ liệu từ session thì dùng câu lệnh sau
                //HttpContext.Session.GetObject<T>(SD.ssShoppingCart); //T là kiểu bất kỳ khi mình set bằng câu lệnh trên

                return RedirectToAction(nameof(Index));
            }
            else
            {
                //phải chắc rằng khi mà modelstate false
                //sẽ trả lại cái cart kèm theo cái product id chứa trong CartObject
                //var bookFromDb = _unit.Product
                //.GetFirstOrDefault(u => u.Id == CartObject.ProductId, includeProperties: "Category,CoverType");
                //ShoppingCart cartObject = new ShoppingCart()
                //{
                //    Product = bookFromDb,
                //    ProductId = bookFromDb.Id
                //};
                //return View(cartObject);

                //hoặc
                return Details(CartObject.ProductId);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
