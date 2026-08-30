using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

[Authorize]
public class PhieuMuonController : Controller
{
    private readonly DbHelper _db;

    public PhieuMuonController(DbHelper db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var roleStr = User.FindFirst(ClaimTypes.Role)?.Value;
        var maTKStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var maTK = int.Parse(maTKStr ?? "1");

        if (roleStr == "1") // Độc giả -> xem lịch sử cá nhân
        {
            var dt = await _db.ExecuteStoredProcedureAsync("usp_XemLichSuCaNhan", new[]
            {
                new SqlParameter("@MaTK", maTK)
            });
            ViewBag.IsDocGia = true;
            return View(dt);
        }
        else // Thủ thư, Ban QL, Admin -> xem toàn bộ phiếu mượn
        {
            var dt = await _db.ExecuteStoredProcedureAsync("usp_XemPhieuMuonToanBo", new[]
            {
                new SqlParameter("@MaTK", maTK)
            });
            ViewBag.IsDocGia = false;
            return View(dt);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? maSach)
    {
        ViewBag.DocGiaList = await _db.ExecuteQueryAsync("SELECT MaDocGia, HoTen, SDT FROM dbo.DOCGIA WHERE TrangThai = N'Hoạt động' ORDER BY HoTen");
        ViewBag.SachList = await _db.ExecuteQueryAsync("SELECT MaSach, TenSach, SoLuong FROM dbo.SACH ORDER BY TenSach");
        ViewBag.SelectedMaSach = maSach;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int maDocGia, int maSach, string? dbmsMode, int? delaySec)
    {
        var delay = delaySec ?? 5;

        try
        {
            var maTKStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var maTK = int.Parse(maTKStr ?? "1");

            // 1. DEMO LOST UPDATE (CHƯA XỬ LÝ)
            if (dbmsMode == "LostUpdate_Loi")
            {
                var dt = await _db.ExecuteStoredProcedureAsync("sp_Demo_LostUpdate_T1_Loi", new[]
                {
                    new SqlParameter("@MaSach", maSach),
                    new SqlParameter("@DelaySec", delay)
                });
                TempData["SuccessMessage"] = dt.Rows.Count > 0 ? dt.Rows[0]["ThongBao"].ToString() : "Mượn sách hoàn tất (Lost Update mode).";
                return RedirectToAction("Index");
            }

            // 2. DEMO LOST UPDATE (ĐÃ KHẮC PHỤC)
            if (dbmsMode == "LostUpdate_KhacPhuc")
            {
                var dt = await _db.ExecuteStoredProcedureAsync("sp_Demo_LostUpdate_T1_KhacPhuc", new[]
                {
                    new SqlParameter("@MaSach", maSach),
                    new SqlParameter("@DelaySec", delay)
                });
                TempData["SuccessMessage"] = dt.Rows.Count > 0 ? dt.Rows[0]["ThongBao"].ToString() : "Mượn sách hoàn tất (UPDLOCK an toàn).";
                return RedirectToAction("Index");
            }

            // 3. DEMO DEADLOCK T1 (Khóa Độc giả -> Delay -> Khóa Sách)
            if (dbmsMode == "Deadlock_T1_Loi" || dbmsMode == "Deadlock_T1_KhacPhuc")
            {
                var spName = dbmsMode == "Deadlock_T1_Loi" ? "sp_Demo_Deadlock_T1_Loi" : "sp_Demo_Deadlock_T1_KhacPhuc";
                var dt = await _db.ExecuteStoredProcedureAsync(spName, new[]
                {
                    new SqlParameter("@MaDocGia", maDocGia),
                    new SqlParameter("@MaSach", maSach),
                    new SqlParameter("@DelaySec", delay)
                });
                TempData["SuccessMessage"] = dt.Rows.Count > 0 ? dt.Rows[0]["ThongBao"].ToString() : "Deadlock T1 hoàn tất.";
                return RedirectToAction("Index");
            }

            // 4. MƯỢN SÁCH CHUẨN NGHIỆP VỤ THẬT
            var parameters = new[]
            {
                new SqlParameter("@MaTK", maTK),
                new SqlParameter("@MaDocGia", maDocGia),
                new SqlParameter("@MaSach", maSach)
            };

            var dtReal = await _db.ExecuteStoredProcedureAsync("sp_MuonSach", parameters);
            TempData["SuccessMessage"] = dtReal.Rows.Count > 0 ? dtReal.Rows[0]["ThongBao"].ToString() : "Lập phiếu mượn thành công!";
            return RedirectToAction("Index");
        }
        catch (SqlException ex)
        {
            if (ex.Number == 1205)
            {
                TempData["ErrorMessage"] = "⚠️ PHÁT HIỆN DEADLOCK (MÃ LỖI 1205): Giao tác này bị SQL Server chọn làm Deadlock Victim và Rollback để giải phóng bế tắc.";
            }
            else
            {
                TempData["ErrorMessage"] = "Lỗi mượn sách: " + ex.Message;
            }
            return RedirectToAction("Create", new { maSach });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
            return RedirectToAction("Create", new { maSach });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TraSach(int maPM, int maSach)
    {
        try
        {
            var maTKStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var maTK = int.Parse(maTKStr ?? "1");

            var parameters = new[]
            {
                new SqlParameter("@MaTK", maTK),
                new SqlParameter("@MaPM", maPM),
                new SqlParameter("@MaSach", maSach)
            };

            var dt = await _db.ExecuteStoredProcedureAsync("sp_TraSach", parameters);
            TempData["SuccessMessage"] = dt.Rows.Count > 0 ? dt.Rows[0]["ThongBao"].ToString() : "Trả sách thành công!";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = "Lỗi trả sách: " + ex.Message;
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GiaHan(int maPM, int maSach)
    {
        try
        {
            var parameters = new[]
            {
                new SqlParameter("@MaPM", maPM),
                new SqlParameter("@MaSach", maSach)
            };

            var dt = await _db.ExecuteStoredProcedureAsync("sp_GiaHanSach", parameters);
            TempData["SuccessMessage"] = dt.Rows.Count > 0 ? dt.Rows[0]["ThongBao"].ToString() : "Gia hạn thành công!";
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = "Lỗi gia hạn: " + ex.Message;
        }

        return RedirectToAction("Index");
    }
}
