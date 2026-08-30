using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly DbHelper _db;

    public AdminController(DbHelper db)
    {
        _db = db;
    }

    private IActionResult? RequireAdmin()
    {
        if (!User.IsRole("4"))
            return RedirectToAction("AccessDenied", "Account");
        return null;
    }

    public async Task<IActionResult> Index(byte? role, string? trangThai, string? keyword)
    {
        var deny = RequireAdmin();
        if (deny != null) return deny;
        var maTK = User.MaTK();
        if (!maTK.HasValue) return RedirectToAction("Login", "Account");

        ViewBag.Role = role;
        ViewBag.TrangThai = trangThai;
        ViewBag.Keyword = keyword;

        try
        {
            var dt = await _db.ExecuteStoredProcedureAsync("usp_Admin_XemTaiKhoan", new[]
            {
                DbHelper.P("@MaTK", maTK.Value),
                DbHelper.P("@Role", role),
                DbHelper.P("@TrangThai", trangThai),
                DbHelper.P("@Keyword", keyword)
            });
            return View(dt);
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return View(new System.Data.DataTable());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoTaiKhoan(string username, string password, byte role, int? maDocGia, int? maNV)
    {
        var deny = RequireAdmin();
        if (deny != null) return deny;
        var maTK = User.MaTK();
        if (!maTK.HasValue) return RedirectToAction("Login", "Account");

        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_Admin_QuanLyTaiKhoan", new[]
            {
                DbHelper.P("@MaTK", maTK.Value),
                DbHelper.P("@Username", username),
                DbHelper.P("@Password", password),
                DbHelper.P("@Role", role),
                DbHelper.P("@MaDocGia", maDocGia),
                DbHelper.P("@MaNV", maNV)
            });
            TempData["SuccessMessage"] = "Đã tạo tài khoản (usp_Admin_QuanLyTaiKhoan).";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, string trangThaiHienTai)
    {
        var deny = RequireAdmin();
        if (deny != null) return deny;
        var maTK = User.MaTK();
        if (!maTK.HasValue) return RedirectToAction("Login", "Account");

        var trangThaiMoi = trangThaiHienTai == "Hoạt động" ? "Bị khóa" : "Hoạt động";
        try
        {
            await _db.ExecuteStoredProcedureAsync("usp_Admin_CapNhatTrangThaiTaiKhoan", new[]
            {
                DbHelper.P("@MaTK", maTK.Value),
                DbHelper.P("@MaTKCanCapNhat", id),
                DbHelper.P("@TrangThai", trangThaiMoi)
            });
            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái tài khoản #{id} thành '{trangThaiMoi}'.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Lỗi cập nhật tài khoản: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Xoa(int id)
    {
        var deny = RequireAdmin();
        if (deny != null) return deny;
        var maTK = User.MaTK();
        if (!maTK.HasValue) return RedirectToAction("Login", "Account");

        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_Admin_XoaTaiKhoan", new[]
            {
                DbHelper.P("@MaTK", maTK.Value),
                DbHelper.P("@MaTKCanXoa", id)
            });
            TempData["SuccessMessage"] = $"Đã xóa tài khoản #{id} (usp_Admin_XoaTaiKhoan).";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
