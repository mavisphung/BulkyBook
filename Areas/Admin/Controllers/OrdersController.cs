using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BulkyBook.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        [BindProperty]
        public OrderDetailsVM OrderVM { get; set; }

        public OrdersController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Details(int id)
        {
            OrderVM = new OrderDetailsVM
            {
                OrderHeader =
                    _unitOfWork.OrderHeader.GetFirstOrDefault(header => header.Id == id,
                                                              includeProperties: "ApplicationUser"),
                OrderDetails =
                    _unitOfWork.OrderDetails.GetAll(detail => detail.OrderId == id,
                                                    includeProperties: "Product")
            };
            return View(OrderVM);
        }

        //xử lí Details POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Details")]
        public IActionResult Details(string stripeToken)
        {
            OrderHeader orderHeader =
                _unitOfWork.OrderHeader.GetFirstOrDefault(header => header.Id == OrderVM.OrderHeader.Id,
                                                            includeProperties: "ApplicationUser");
            if (stripeToken != null)
            {
                var options = new ChargeCreateOptions
                {
                    Amount = Convert.ToInt32(orderHeader.OrderTotal * 100),
                    Currency = "usd",
                    Description = "Order ID: " + orderHeader.Id,
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
                    orderHeader.PaymentStatus = SD.PaymentStatusRejected;
                }
                else
                {
                    orderHeader.TransactionId = charge.Id;
                }

                //Kiểm tra xem trạng thái của charge đã thành công hay chưa, tức là đã trả tiền hay chưa
                // => Status sẽ có giá trị là Succeeded
                if (charge.Status.ToLower() == "succeeded")
                {
                    orderHeader.PaymentStatus = SD.PaymentStatusApproved;
                    orderHeader.OrderStatus = SD.StatusApproved;
                    orderHeader.PaymentDate = DateTime.Now;
                }
                _unitOfWork.Save();
            }
            return RedirectToAction("Details", "Orders", new { id = orderHeader.Id });
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult StartProcessing(int id)
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(header => header.Id == id);
            //Đổi Status của header thành in process để xử lí
            orderHeader.OrderStatus = SD.StatusInProcess;
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult ShipOrder()
        {
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(header => header.Id == OrderVM.OrderHeader.Id);
            orderHeader.TrackingNumber = OrderVM.OrderHeader.TrackingNumber;
            orderHeader.Carrier = OrderVM.OrderHeader.Carrier;
            orderHeader.OrderStatus = SD.StatusShipped;
            orderHeader.ShippingDate = DateTime.Now;
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
        public IActionResult CancelOrder(int id)
        {
            //khi mà hủy thì sẽ refund lại cho khách hàng ngủyên
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(header => header.Id == id);
            if (orderHeader.PaymentStatus == SD.StatusApproved)
            {
                
                var options = new RefundCreateOptions()
                {
                    Amount = Convert.ToInt32(orderHeader.OrderTotal * 100),
                    Reason = RefundReasons.RequestedByCustomer,
                    Charge = orderHeader.TransactionId
                };
                var service = new RefundService();
                Refund refund = service.Create(options);

                orderHeader.OrderStatus = SD.StatusRefunded;
                orderHeader.PaymentStatus = SD.StatusRefunded;
            }
            else
            {
                orderHeader.OrderStatus = SD.StatusCancelled;
                orderHeader.PaymentStatus = SD.StatusCancelled;
            }
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        #region API Calls
        [HttpGet]
        public IActionResult GetOrderList(string status)
        {
            //lấy danh sách các order, trong này được định danh là OrderHeader
            //IEnumerable<OrderHeader> orderHeaderList =
            //    _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");
            //return Json(new { data = orderHeaderList });

            //lấy order theo user và tùy theo role của user mà lấy hết hay không
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

            IEnumerable<OrderHeader> orderHeaderList; //dùng để hứng dữ liệu lấy được

            if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
            {
                //Nếu user có role admin hoặc employee thì được lấy toàn bộ order
                orderHeaderList = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");
            }
            else
            {
                //không thì chỉ lấy được những order của user đã được log in
                orderHeaderList =
                    _unitOfWork.OrderHeader.GetAll(order => order.AppUserId == claim.Value,
                                                    includeProperties: "ApplicationUser");
            }
            //Sau khi lấy được danh sách dựa vào user thì
            //Cập nhật theo thuộc tính status được truyền vào trong hàm GetOrderList này
            switch (status)
            {
                case "pending":
                    //giải thích cho câu lệnh dưới:
                    //Cập nhật lại orderHeaderList
                    //Sau khi lấy hết order dựa vào user
                    //bước kế tiếp là cập nhật lại orderHeaderList với biến status được truyền vào
                    orderHeaderList = orderHeaderList.Where(order => order.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
                case "inprocess":
                    orderHeaderList = orderHeaderList.Where(order => order.OrderStatus == SD.StatusApproved ||
                                                                     order.OrderStatus == SD.StatusInProcess ||
                                                                     order.OrderStatus == SD.StatusPending); 
                    break;
                case "completed":
                    orderHeaderList = orderHeaderList.Where(order => order.PaymentStatus == SD.StatusShipped);
                    break;
                case "rejected":
                    orderHeaderList = orderHeaderList.Where(order => order.OrderStatus == SD.StatusCancelled ||
                                                                     order.OrderStatus == SD.StatusRefunded ||
                                                                     order.OrderStatus == SD.PaymentStatusRejected);
                    break;
                default:

                    break;
            }
            return Json(new { data = orderHeaderList });

        }

        #endregion
    }
}
