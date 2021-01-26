using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace BulkyBook.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        //Chú ý RoleManager => đoạn này có thể gây lỗi vì mình chưa tiêm thằng RoleManager vào project
        //Error: Unable to resolve service for type 
        //       'Microsoft.AspNetCore.Identity.RoleManager`1[Microsoft.AspNetCore.Identity.IdentityRole]' 
        //       while attempting to activate 'BulkyBook.Areas.Identity.Pages.Account.RegisterModel'.
        //------
        //cách xử lí: vào startup.cs => services.AddIdentity<IdentityUser, IdentityRole>()


        //kế tiếp sẽ bị lỗi Unable to resolve service for type 'Microsoft.AspNetCore.Identity.UI.Services.IEmailSender' 
        //                  while attempting to activate 'BulkyBook.Areas.Identity.Pages.Account.RegisterModel'.
        //Bị lỗi này là do ngay dòng phía dưới mình đã đóng comment dòng var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        //cách xử lí: services.AddIdentity<IdentityUser, IdentityRole>().AddDefaultTokenProviders()
        // Và dựng 1 class implement interface IEmailSender
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUnitOfWork _unit;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            RoleManager<IdentityRole> roleManager,
            IUnitOfWork unit)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _roleManager = roleManager;
            _unit = unit;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Name { get; set; }
            public string StreetAddress { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string PostalCode { get; set; }
            public string PhoneNumber { get; set; }
            public int? CompanyId { get; set; }

            public string Role { get; set; }

            public IEnumerable<SelectListItem> CompanyList { get; set; }
            public IEnumerable<SelectListItem> RoleList { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;

            Input = new InputModel()
            {
                CompanyList = _unit.Company.GetAll().Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                }),
                //yêu cầu là người dùng admin không thể tạo account có role là Individual Customer
                // nên sẽ lựa toàn bộ role trừ SD.Role_User_Individual
                //Và mình lấy về cái list tên nên Text = i luôn
                RoleList = _roleManager.Roles.Where(i => i.Name != SD.Role_User_Individual).Select(r => r.Name).Select(i => new SelectListItem
                {
                    Text = i,
                    Value = i
                })
            };

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                //var user = new IdentityUser { UserName = Input.Email, Email = Input.Email };
                var user = new ApplicationUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    CompanyId = Input.CompanyId,
                    StreetAddress = Input.StreetAddress,
                    City = Input.City,
                    State = Input.State,
                    PostalCode = Input.PostalCode,
                    Name = Input.Name,
                    PhoneNumber = Input.PhoneNumber,
                    Role = Input.Role
                };

                //Tạo tại khoản
                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    //trường hợp tạo tài khoản thành công
                    _logger.LogInformation("User created a new account with password.");

                    //kiểm tra xem role này đã có trong RoleManager chưa, nếu chưa có thì thêm role mới vào
                    if (!await _roleManager.RoleExistsAsync(SD.Role_Admin))
                    {
                        //trường hợp chưa tồn tại thì tạo mới
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_Admin));
                    }

                    if (!await _roleManager.RoleExistsAsync(SD.Role_Employee))
                    {
                        //trường hợp chưa tồn tại thì tạo mới
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_Employee));
                    }
                    if (!await _roleManager.RoleExistsAsync(SD.Role_User_Company))
                    {
                        //trường hợp chưa tồn tại thì tạo mới
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_User_Company));
                    }
                    if (!await _roleManager.RoleExistsAsync(SD.Role_User_Individual))
                    {
                        //trường hợp chưa tồn tại thì tạo mới
                        await _roleManager.CreateAsync(new IdentityRole(SD.Role_User_Individual));
                    }

                    //Add role cho user
                    //mặc định đang để là admin
                    //await _userManager.AddToRoleAsync(user, SD.Role_Admin);

                    if (user.Role == null)
                    {
                        //trường hợp đăng ký nhưng không chọn role => tạo role mặc định là Individual Customer
                        await _userManager.AddToRoleAsync(user, SD.Role_User_Individual);
                    }
                    else
                    {
                        //trường hợp đăng ký và chọn role
                        //sẽ check thêm là user có company hay không
                        if (user.CompanyId > 0)
                        {
                            //trường hợp có company
                            await _userManager.AddToRoleAsync(user, SD.Role_User_Company);
                        }
                        await _userManager.AddToRoleAsync(user, user.Role);
                    }

                    //var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    //code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    //var callbackUrl = Url.Page(
                    //    "/Account/ConfirmEmail",
                    //    pageHandler: null,
                    //    values: new { area = "Identity", userId = user.Id, code = code, returnUrl = returnUrl },
                    //    protocol: Request.Scheme);

                    //await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                    //    $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        if (user.Role == null)
                        {
                            await _signInManager.SignInAsync(user, isPersistent: false);
                            return LocalRedirect(returnUrl);
                        }
                        else
                        {
                            //trường hợp admin đăng ký 1 user mới
                            //vẫn giữ đăng nhập của admin và trả về list users
                            //có link như sau /Admin/Users/Index
                            return RedirectToAction("Index", "User", new { Area = "Admin" });
                        }
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
