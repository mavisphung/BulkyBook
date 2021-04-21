using BulkyBook.DataAccess.Data;
using BulkyBook.DataAccess.Initializer;
using BulkyBook.DataAccess.Repository;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BulkyBook
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));
            services.AddDatabaseDeveloperPageExceptionFilter();

            //options => options.SignIn.RequireConfirmedAccount = true //đăng nhập và dùng email xác thực
            services.AddIdentity<IdentityUser, IdentityRole>().AddDefaultTokenProviders()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            //------------------------Email-----------------
            services.AddSingleton<IEmailSender, EmailSender>();
            services.Configure<EmailOptions>(Configuration);
            // Sau khi thêm dòng Configure, hệ thống sẽ mapping 2 thuộc tính trong file appsettings.json vào class EmailOptions
            // theo kiểu Dependencies Injection (DI)
            //-----------------------------------------------
            //------------------------Stripe----------------
            services.Configure<StripeSettings>(Configuration.GetSection("Stripe"));
            services.Configure<BrainTreeSettings>(Configuration.GetSection("BrainTree"));
            //------------------Twilio----------------------
            services.Configure<TwilioSettings>(Configuration.GetSection("Twilio"));
            //----------------------------------------------
            //------------Tiêm UnitOfWork vào DI----------------
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IBrainTreeGate, BrainTreeGate>();
            //---------------Thêm ITempDataProvider với mục đích chứa các dữ liệu tạm thời sau khi delete----------------------
            services.AddSingleton<ITempDataProvider, CookieTempDataProvider>();
            //------------Tiêm DbInitializer vào DI----------------
            services.AddScoped<IDbInitializer, DbInitializer>();
            //-----------------------------------------------------
            services.AddControllersWithViews().AddRazorRuntimeCompilation();
            services.AddRazorPages();
            //thêm dòng này vào để khi phân quyền bên controller
            //hệ thống asp.net sẽ tự động xét role của cái cookie đấy rồi điều về trang thích hợp
            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = $"/Identity/Account/Login";
                options.LogoutPath = $"/Identity/Account/Logout";
                options.AccessDeniedPath = $"/Identity/Account/AccessDenied";
            });

            //Add facebook login api
            //gồm có 2 thứ cần chú ý: App Id và ApplSecret trên facebook for dev
            //2 mã này copy trên facebook
            services.AddAuthentication().AddFacebook(options =>
            {
                options.AppId = "423674195529328";
                options.AppSecret = "c76553e3c03f64aa1ccc6eaa65a297c0";
            });

            //add google+ api
            //giống y chang facebook
            services.AddAuthentication().AddGoogle(options =>
            {
                options.ClientId = "364228755076-nqfft0evjngqbig8elgt33tu1f6j68g3.apps.googleusercontent.com";
                options.ClientSecret = "mwdpPkp6J3eIF1XpqjW5e06P";
            });

            //Thêm session và chỉnh 3 thứ
            //  1. Thời gian timeout
            //  2. Cookie.HttpOnly = true
            //  3. Cookie.IsEssential = true
            //Chức năng của session trong này là gì?
            //  => Có thể giúp lưu 1 List hay object dưới dạng json để và truy xuất ở bất kỳ controller nào mà mình muốn
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); //30p
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IDbInitializer dbInitializer)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            //Stripe
            StripeConfiguration.ApiKey = Configuration.GetSection("Stripe")["SecretKey"];
            //session
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();
            dbInitializer.Initialize();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "areas",
                    pattern: "{area=Customer}/{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });
        }
    }
}
