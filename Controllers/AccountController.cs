using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

public class AccountController : Controller
{
    private readonly DbHelper _db;

    public AccountController(DbHelper db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = await _db.CheckLoginAsync(model.Username, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác, hoặc tài khoản đã bị khóa.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.MaTK.ToString()),
                new(ClaimTypes.Name, user.Username),
                new("HoTen", user.HoTen ?? user.Username),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("TenRole", user.TenRole ?? "Độc giả")
            };

            if (user.MaDocGia.HasValue) claims.Add(new("MaDocGia", user.MaDocGia.Value.ToString()));
            if (user.MaNV.HasValue) claims.Add(new("MaNV", user.MaNV.Value.ToString()));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = model.RememberMe });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Lỗi đăng nhập: " + ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Mật khẩu xác nhận không khớp.");
            return View(model);
        }

        try
        {
            var parameters = new[]
            {
                new SqlParameter("@Username", model.Username),
                new SqlParameter("@Password", model.Password),
                new SqlParameter("@HoTen", model.HoTen),
                new SqlParameter("@NgaySinh", (object?)model.NgaySinh ?? DBNull.Value),
                new SqlParameter("@GioiTinh", (object?)model.GioiTinh ?? DBNull.Value),
                new SqlParameter("@DiaChi", (object?)model.DiaChi ?? DBNull.Value),
                new SqlParameter("@SDT", model.SDT),
                new SqlParameter("@Email", (object?)model.Email ?? DBNull.Value)
            };

            var dt = await _db.ExecuteStoredProcedureAsync("usp_DangKyDocGia", parameters);
            TempData["SuccessMessage"] = "Đăng ký tài khoản độc giả thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }
        catch (SqlException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }
}

