using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

[Authorize]
public class DanhMucController : Controller
{
    private readonly DbHelper _db;

    public DanhMucController(DbHelper db)
    {
        _db = db;
    }

    private IActionResult? RequireStaff()
    {
        if (!User.IsRole("2", "3", "4"))
            return RedirectToAction("AccessDenied", "Account");
        return null;
    }

    private IActionResult? RequireThuThu()
    {
        if (!User.IsRole("2"))
            return RedirectToAction("AccessDenied", "Account");
        return null;
    }

    public async Task<IActionResult> TheLoai(string? keyword)
    {
        var deny = RequireStaff();
        if (deny != null) return deny;
        ViewBag.Keyword = keyword;
        var dt = await _db.ExecuteStoredProcedureAsync("usp_XemTheLoai", new[] { DbHelper.P("@Keyword", keyword) });
        return View(dt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemTheLoai(string tenTheLoai, string? moTa, int soNgayMuonToiDa)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_ThemTheLoai", new[]
            {
                DbHelper.P("@TenTheLoai", tenTheLoai),
                DbHelper.P("@MoTa", moTa),
                DbHelper.P("@SoNgayMuonToiDa", soNgayMuonToiDa)
            });
            TempData["SuccessMessage"] = "Đã thêm thể loại (usp_ThemTheLoai).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(TheLoai));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaTheLoai(int maTheLoai, string tenTheLoai, string? moTa, int soNgayMuonToiDa)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_SuaTheLoai", new[]
            {
                DbHelper.P("@MaTheLoai", maTheLoai),
                DbHelper.P("@TenTheLoai", tenTheLoai),
                DbHelper.P("@MoTa", moTa),
                DbHelper.P("@SoNgayMuonToiDa", soNgayMuonToiDa)
            });
            TempData["SuccessMessage"] = "Đã sửa thể loại (usp_SuaTheLoai).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(TheLoai));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaTheLoai(int maTheLoai)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_XoaTheLoai", new[] { DbHelper.P("@MaTheLoai", maTheLoai) });
            TempData["SuccessMessage"] = "Đã xóa thể loại (usp_XoaTheLoai).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(TheLoai));
    }

    public async Task<IActionResult> TacGia(string? keyword)
    {
        var deny = RequireStaff();
        if (deny != null) return deny;
        ViewBag.Keyword = keyword;
        var dt = await _db.ExecuteStoredProcedureAsync("usp_XemTacGia", new[] { DbHelper.P("@Keyword", keyword) });
        return View(dt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemTacGia(string tenTacGia, DateTime? ngaySinh, string? quocTich)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_ThemTacGia", new[]
            {
                DbHelper.P("@TenTacGia", tenTacGia),
                DbHelper.P("@NgaySinh", ngaySinh),
                DbHelper.P("@QuocTich", quocTich)
            });
            TempData["SuccessMessage"] = "Đã thêm tác giả (usp_ThemTacGia).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(TacGia));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaTacGia(int maTacGia, string tenTacGia, DateTime? ngaySinh, string? quocTich)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_SuaTacGia", new[]
            {
                DbHelper.P("@MaTacGia", maTacGia),
                DbHelper.P("@TenTacGia", tenTacGia),
                DbHelper.P("@NgaySinh", ngaySinh),
                DbHelper.P("@QuocTich", quocTich)
            });
            TempData["SuccessMessage"] = "Đã sửa tác giả (usp_SuaTacGia).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(TacGia));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaTacGia(int maTacGia)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_XoaTacGia", new[] { DbHelper.P("@MaTacGia", maTacGia) });
            TempData["SuccessMessage"] = "Đã xóa tác giả (usp_XoaTacGia).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(TacGia));
    }

    public async Task<IActionResult> NhaXuatBan(string? keyword)
    {
        var deny = RequireStaff();
        if (deny != null) return deny;
        ViewBag.Keyword = keyword;
        var dt = await _db.ExecuteStoredProcedureAsync("usp_XemNhaXuatBan", new[] { DbHelper.P("@Keyword", keyword) });
        return View(dt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemNhaXuatBan(string tenNXB, string? diaChi, string? soDienThoai)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_ThemNhaXuatBan", new[]
            {
                DbHelper.P("@TenNXB", tenNXB),
                DbHelper.P("@DiaChi", diaChi),
                DbHelper.P("@SoDienThoai", soDienThoai)
            });
            TempData["SuccessMessage"] = "Đã thêm NXB (usp_ThemNhaXuatBan).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(NhaXuatBan));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaNhaXuatBan(int maNXB, string tenNXB, string? diaChi, string? soDienThoai)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_SuaNhaXuatBan", new[]
            {
                DbHelper.P("@MaNXB", maNXB),
                DbHelper.P("@TenNXB", tenNXB),
                DbHelper.P("@DiaChi", diaChi),
                DbHelper.P("@SoDienThoai", soDienThoai)
            });
            TempData["SuccessMessage"] = "Đã sửa NXB (usp_SuaNhaXuatBan).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(NhaXuatBan));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaNhaXuatBan(int maNXB)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_XoaNhaXuatBan", new[] { DbHelper.P("@MaNXB", maNXB) });
            TempData["SuccessMessage"] = "Đã xóa NXB (usp_XoaNhaXuatBan).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(NhaXuatBan));
    }

    public async Task<IActionResult> DocGia(string? keyword, string? trangThai, int? maDocGia)
    {
        var deny = RequireStaff();
        if (deny != null) return deny;
        var maTK = User.MaTK();
        if (!maTK.HasValue) return RedirectToAction("Login", "Account");
        ViewBag.Keyword = keyword;
        ViewBag.TrangThai = trangThai;
        try
        {
            var dt = await _db.ExecuteStoredProcedureAsync("usp_XemDocGiaTheoQuyen", new[]
            {
                DbHelper.P("@MaTK", maTK.Value),
                DbHelper.P("@MaDocGia", maDocGia),
                DbHelper.P("@TrangThai", trangThai),
                DbHelper.P("@Keyword", keyword)
            });
            return View(dt);
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return View(new DataTable());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemDocGia(string hoTen, DateTime? ngaySinh, string? gioiTinh, string? diaChi, string sdt, string? email)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_ThemDocGia", new[]
            {
                DbHelper.P("@HoTen", hoTen),
                DbHelper.P("@NgaySinh", ngaySinh),
                DbHelper.P("@GioiTinh", gioiTinh),
                DbHelper.P("@DiaChi", diaChi),
                DbHelper.P("@SDT", sdt),
                DbHelper.P("@Email", email)
            });
            TempData["SuccessMessage"] = "Đã thêm độc giả (usp_ThemDocGia).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(DocGia));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaDocGia(int maDocGia, string hoTen, DateTime? ngaySinh, string? gioiTinh, string? diaChi, string sdt, string? email, string trangThai)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_SuaDocGia", new[]
            {
                DbHelper.P("@MaDocGia", maDocGia),
                DbHelper.P("@HoTen", hoTen),
                DbHelper.P("@NgaySinh", ngaySinh),
                DbHelper.P("@GioiTinh", gioiTinh),
                DbHelper.P("@DiaChi", diaChi),
                DbHelper.P("@SDT", sdt),
                DbHelper.P("@Email", email),
                DbHelper.P("@TrangThai", trangThai)
            });
            TempData["SuccessMessage"] = "Đã sửa độc giả (usp_SuaDocGia).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(DocGia));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaDocGia(int maDocGia)
    {
        var deny = RequireThuThu();
        if (deny != null) return deny;
        try
        {
            await _db.ExecuteNonQueryStoredProcedureAsync("usp_XoaDocGia", new[] { DbHelper.P("@MaDocGia", maDocGia) });
            TempData["SuccessMessage"] = "Đã xóa độc giả (usp_XoaDocGia).";
        }
        catch (SqlException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(DocGia));
    }
}
