using BulkyBook.DataAccess.Data;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _db;

        public UsersController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index()
        {
            return View();
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
            //do mình có data annotation bên Models => khi đấy
            //câu lệnh include này nôm na như sau: CHỈ LẤY mọi ApplicationUser có trong db
            //chứ KHÔNG PHẢI LẤY ApplicationUser kèm theo Company của ApplicationUser
            //nếu ApplicationUser.CompanyId mà có giá trị > 0 thì nó sẽ load Company.Id tương ứng và gắn vào ApplicationUser.Company
            var userList = _db.ApplicationUsers.Include(u => u.Company).ToList(); //bảng user
            var userRole = _db.UserRoles.ToList(); //bảng phụ
            var roles = _db.Roles.ToList(); //bảng role
            //Do bên class ApplicationUser để NotMapped ngay thuộc tính Role
            // => mình phải map thủ công bên dưới bằng foreach
            //ý tưởng như sau:
            // 1. Lấy roleId từ bảng (model) UserRole với điều kiện là UserRole.UserId == ApplicationUser.Id
            // 2. gán vào ApplicationUser.Role = Role.Name với điều kiện là ApplicationUser.Id == roleId lấy từ step 1

            foreach (var user in userList)
            {
                /* SELECT ur.UserId, ur.RoleId
                 * FROM UserRole ur, ApplicationUser user
                 * WHERE ur.UserId = user.Id
                 */
                var roleId = userRole.FirstOrDefault(u => u.UserId == user.Id).RoleId;

                /* SELECT r.Name
                 * FROM Role r, UserRole ur
                 * WHERE r.RoleId = ur.RoleId
                 */
                user.Role = roles.FirstOrDefault(u => u.Id == roleId).Name;
                //company trong user có thể null => gây ra lỗi
                // => xử lí bằng cách check nếu user.Company == null thì khởi tạo 1 đối tượng Company mới với Name = "" 
                if (user.Company == null)
                {
                    user.Company = new Company()
                    {
                        Name = ""
                    };
                }
            }
            return Json(new { data = userList });
        }

        [HttpPost]
        public IActionResult LockUnlock([FromBody] string id)
        {
            var objectFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == id);
            if (objectFromDb == null)
            {
                return Json(new { success = false, message = "Error while locking/unlocking" });
            }

            if (objectFromDb.LockoutEnd != null && objectFromDb.LockoutEnd > DateTime.Now)
            {
                //user đang bị lock
                objectFromDb.LockoutEnd = DateTime.Now;
            } 
            else
            {
                objectFromDb.LockoutEnd = DateTime.Now.AddYears(1000);
            }
            _db.SaveChanges();
            return Json(new { success = true, message = "Operation has been executed" });
        }

        #endregion


    }
}
