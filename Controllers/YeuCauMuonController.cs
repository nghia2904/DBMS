using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

[Authorize]
public class YeuCauMuonController : Controller
{
    private readonly DbHelper _db;

    public YeuCauMuonController(DbHelper db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? trangThai)
    {
        var maTK = User.MaTK();
        if (!maTK.HasValue)
            return RedirectToAction("Login", "Account");

        try
        {
            if (User.IsRole("1"))
            {
                var dt = await _db.ExecuteStoredProcedureAsync("usp_XemYeuCauMuonCaNhan", new[]
                {
                    DbHelper.P("@MaTK", maTK.Value)
                });
                return View("CuaToi", dt);
            }

            if (User.IsRole("2"))
            {
                var dt = await _db.ExecuteStoredProcedureAsync("usp_XemYeuCauMuonThuThu", new[]
                {
                    DbHelper.P("@MaTK", maTK.Value),
                    DbHelper.P("@TrangThai", trangThai)
                });
                ViewBag.TrangThai = trangThai;
                return View("ThuThu", dt);
            }
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction("AccessDenied", "Account");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Tao(int maSach)
    {
        if (!User.IsRole("1") || !User.MaTK().HasValue)
            return RedirectToAction("AccessDenied", "Account");

        try
        {
            var dt = await _db.ExecuteStoredProcedureAsync("usp_TaoYeuCauMuon", new[]
            {
                DbHelper.P("@MaTK", User.MaTK()!.Value),
                DbHelper.P("@MaSach", maSach)
            });
            TempData["SuccessMessage"] = dt.Rows.Count > 0 && dt.Columns.Contains("ThongBao")
                ? dt.Rows[0]["ThongBao"].ToString()
                : "Đã gửi yêu cầu mượn (usp_TaoYeuCauMuon).";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Huy(int maYC)
    {
        if (!User.IsRole("1") || !User.MaTK().HasValue)
            return RedirectToAction("AccessDenied", "Account");

        try
        {
            var dt = await _db.ExecuteStoredProcedureAsync("usp_HuyYeuCauMuon", new[]
            {
                DbHelper.P("@MaTK", User.MaTK()!.Value),
                DbHelper.P("@MaYC", maYC)
            });
            TempData["SuccessMessage"] = dt.Rows.Count > 0 && dt.Columns.Contains("ThongBao")
                ? dt.Rows[0]["ThongBao"].ToString()
                : "Đã hủy yêu cầu.";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XuLy(int maYC, string ketQua, string? lyDoTuChoi)
    {
        if (!User.IsRole("2") || !User.MaTK().HasValue)
            return RedirectToAction("AccessDenied", "Account");

        try
        {
            var dt = await _db.ExecuteStoredProcedureAsync("usp_XuLyYeuCauMuon", new[]
            {
                DbHelper.P("@MaTK", User.MaTK()!.Value),
                DbHelper.P("@MaYC", maYC),
                DbHelper.P("@KetQua", ketQua),
                DbHelper.P("@LyDoTuChoi", lyDoTuChoi)
            });

            var message = dt.Rows.Count > 0 && dt.Columns.Contains("ThongBao")
                ? dt.Rows[0]["ThongBao"].ToString()
                : "Đã xử lý yêu cầu.";

            TempData["SuccessMessage"] = message;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message });
            }
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        return RedirectToAction("Index");
    }
}
