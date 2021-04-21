using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace BulkyBook.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class CartController : Controller
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<IdentityUser> _userManager;
        private TwilioSettings _twilioOptions { get; set; }

        [BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }

        public CartController(IUnitOfWork unitOfWork, 
                              IEmailSender emailSender,
                              UserManager<IdentityUser> userManager,
                              IOptions<TwilioSettings> twilioOptions)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _userManager = userManager;
            _twilioOptions = twilioOptions.Value;
        }

        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity) User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            ShoppingCartVM = new ShoppingCartVM()
            {
                OrderHeader = new OrderHeader(),
                //số lượng sản phẩm được lấy từ cart, 1 ShoppingCart == 1 Product
                ListCart = _unitOfWork.ShoppingCart.GetAll(u => u.AppUserId == claim.Value, includeProperties: "Product")
            };
            ShoppingCartVM.OrderHeader.ApplicationUser = 
                _unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claim.Value, includeProperties: "Company");

            //tổng giá tiền của các sản phẩm chứa trong cart
            foreach (var list in ShoppingCartVM.ListCart)
            {
                //lấy giá tương ứng với số lượng
                list.Price = SD.GetPriceBasedOnQuantity(list.Count, list.Product.Price, list.Product.Price50, list.Product.Price100);
                //tổng giá
                ShoppingCartVM.OrderHeader.OrderTotal += (list.Price * list.Count);
                //cập nhật lại description thành raw html
                list.Product.Description = SD.ConvertToRawHtml(list.Product.Description);

                //sửa thành ... nếu description dài hơn 100 ký tự
                if (list.Product.Description.Length > 100)
                {
                    list.Product.Description = list.Product.Description.Substring(0, 99) + "...";
                }
            }

            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPOST()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            var user = _unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claim.Value);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Verification email is empty");
            }
            

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = code },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(user.Email, "Confirm your email",
                $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Plus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart
                                        .GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");
            cartFromDb.Count += 1;
            cartFromDb.Price = SD.GetPriceBasedOnQuantity(cartFromDb.Count,
                                                          cartFromDb.Product.Price,
                                                          cartFromDb.Product.Price50,
                                                          cartFromDb.Product.Price100);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Minus(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart
                                        .GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");

            if (cartFromDb.Count == 1)
            {
                var cnt = _unitOfWork.ShoppingCart.GetAll(u => u.AppUserId == cartFromDb.AppUserId).ToList().Count();
                _unitOfWork.ShoppingCart.Remove(cartFromDb);
                _unitOfWork.Save();

                HttpContext.Session.SetInt32(SD.ssShoppingCart, cnt - 1);
            }
            else
            {
                cartFromDb.Count -= 1;
                cartFromDb.Price = SD.GetPriceBasedOnQuantity(cartFromDb.Count,
                                                              cartFromDb.Product.Price,
                                                              cartFromDb.Product.Price50,
                                                              cartFromDb.Product.Price100);
            }
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Remove(int cartId)
        {
            var cartFromDb = _unitOfWork.ShoppingCart
                                        .GetFirstOrDefault(c => c.Id == cartId, includeProperties: "Product");

            var cnt = _unitOfWork.ShoppingCart.GetAll(u => u.AppUserId == cartFromDb.AppUserId).ToList().Count();
            _unitOfWork.ShoppingCart.Remove(cartFromDb);
            _unitOfWork.Save();

            HttpContext.Session.SetInt32(SD.ssShoppingCart, cnt - 1);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Summary()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            ShoppingCartVM = new ShoppingCartVM()
            {
                OrderHeader = new OrderHeader(),
                ListCart = _unitOfWork.ShoppingCart.GetAll(c => c.AppUserId == claim.Value,
                                                            includeProperties: "Product")
            };

            ShoppingCartVM.OrderHeader.ApplicationUser =
                _unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claim.Value, includeProperties: "Company");

            foreach (var list in ShoppingCartVM.ListCart)
            {
                list.Price = SD.GetPriceBasedOnQuantity(list.Count, 
                                                        list.Product.Price, 
                                                        list.Product.Price50, 
                                                        list.Product.Price100);
                ShoppingCartVM.OrderHeader.OrderTotal += (list.Price * list.Count);
            }

            //địa chỉ được in trên bill
            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
            ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.StreetAddress;
            ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
            ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;
            ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;

            return View(ShoppingCartVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Summary")]
        public IActionResult SummaryPOST(string stripeToken)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            //BindProperties thay vì truyền tham số

            ShoppingCartVM.OrderHeader.ApplicationUser =
                _unitOfWork.ApplicationUser.GetFirstOrDefault(u => u.Id == claim.Value, includeProperties: "Company");

            ShoppingCartVM.ListCart =
                _unitOfWork.ShoppingCart.GetAll(c => c.AppUserId == claim.Value, includeProperties: "Product");

            ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            ShoppingCartVM.OrderHeader.AppUserId = claim.Value;
            ShoppingCartVM.OrderHeader.OrderDate = DateTime.Now;

            _unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitOfWork.Save();

            //sau khi lấy được cái cart của user thì bắt đầu tạo order detail
            //List<OrderDetails> orderDetailsList = new List<OrderDetails>();
            foreach (var item in ShoppingCartVM.ListCart)
            {
                item.Price = SD.GetPriceBasedOnQuantity(item.Count,
                                                        item.Product.Price,
                                                        item.Product.Price50,
                                                        item.Product.Price100);
                OrderDetails orderDetails = new OrderDetails()
                {
                    ProductId = item.ProductId,
                    OrderId = ShoppingCartVM.OrderHeader.Id,
                    Price = item.Price,
                    Count = item.Count
                };
                ShoppingCartVM.OrderHeader.OrderTotal += orderDetails.Price * orderDetails.Count;
                _unitOfWork.OrderDetails.Add(orderDetails);
            }
            _unitOfWork.ShoppingCart.RemoveRange(ShoppingCartVM.ListCart);
            _unitOfWork.Save();

            HttpContext.Session.SetInt32(SD.ssShoppingCart, 0);

            if (stripeToken == null)
            {
                //using for delayed payment.
                ShoppingCartVM.OrderHeader.PaymentDueDate = DateTime.Now.AddDays(30);
                ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusDelayedPayment;
                ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
            }
            else
            {
                //tạo 1 đối tượng ChargeCreateOptions mà nhận cái stripeToken được trả về từ View
                //Sử dụng 4 thuộc tình cơ bản của 1 options
                //Amount => Số tiền
                //Currency => loại tệ
                //Description => miễn là có ý nghĩa
                //Source => gán bằng stripeToken
                var options = new ChargeCreateOptions
                {
                    Amount = Convert.ToInt32(ShoppingCartVM.OrderHeader.OrderTotal * 100),
                    Currency = "usd",
                    Description = "Order ID: " + ShoppingCartVM.OrderHeader.Id,
                    Source = stripeToken
                };

                //khởi tạo đối tượng ChargeService để có thể tạo ra đối tượng Charge dùng để xử lí transaction
                //đối tượng Charge sẽ đảm bảo cái transaction đấy có thành công hay không
                var service = new ChargeService();
                Charge charge = service.Create(options);

                //Kiểm tra thuộc tính BalanceTransactionId xem có bằng null hay không
                //Nếu null => tức là lỗi
                //PaymentStatus sẽ là Rejected
                if (charge.Id == null)
                {
                    ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusRejected;
                }
                else
                {
                    ShoppingCartVM.OrderHeader.TransactionId = charge.BalanceTransactionId;
                }

                //Kiểm tra xem trạng thái của charge đã thành công hay chưa, tức là đã trả tiền hay chưa
                // => Status sẽ có giá trị là Succeeded
                if (charge.Status.ToLower() == "succeeded")
                {
                    ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusApproved;
                    ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusApproved;
                    ShoppingCartVM.OrderHeader.PaymentDate = DateTime.Now;
                }
            }
            //chỉ nên để UnitOfWork save 1 lần để tói ưu hóa
            _unitOfWork.Save();
            return RedirectToAction("OrderConfirmation", "Cart", new { id = ShoppingCartVM.OrderHeader.Id });
        }

        public IActionResult OrderConfirmation(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(header => header.Id == id);
            TwilioClient.Init(_twilioOptions.AccountSid, _twilioOptions.AuthToken);
            //Phải có try catch vì có khả năng bên api gây lỗi
            //Hướng dẫn cài đặt Twilio
            /*
             * Tạo 1 class TwilioSettings chứa 3 thuộc tính
             * PhoneNumber, AccountSid, AuthToken
             * Vào Nuget cài đặt Twilio
             * Lưu PhoneNumber, AccountSid, AuthToken trên web vào file appsettings.json
             * Tiêm vào DI bằng services.configure...
             * Xài Twilio.Rest.Api.V2010.Account.MessageResource để gửi
             * Twilio.Types.PhoneNumber
             */
            try
            {
                var message = MessageResource.Create(
                    body: "Ban vua dat hang tren Shopee (BulkyBook). Ma don hang la: " + id,
                    from: new PhoneNumber(_twilioOptions.PhoneNumber),
                    to: new PhoneNumber(orderHeader.PhoneNumber)
                    );
            }
            catch (Exception ex)
            {

            }
            return View(id);
        }
    }
}
