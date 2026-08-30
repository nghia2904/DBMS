using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

public class SachController : Controller
{
    private readonly DbHelper _db;

    public SachController(DbHelper db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? keyword, int? maTheLoai, int? maTacGia, bool? demoPhantom, string? phantomMode, int? delaySec)
    {
        // XỬ LÝ DEMO PHANTOM READ TẠI TRANG DANH MỤC SÁCH
        if (demoPhantom == true && maTheLoai.HasValue)
        {
            var delay = delaySec ?? 5;
            var spName = phantomMode == "KhacPhuc" ? "sp_Demo_PhantomRead_T1_KhacPhuc" : "sp_Demo_PhantomRead_T1_Loi";
            try
            {
                var dtDemo = await _db.ExecuteStoredProcedureAsync(spName, new[]
                {
                    new SqlParameter("@MaTheLoai", maTheLoai.Value),
                    new SqlParameter("@DelaySec", delay)
                });
                if (dtDemo.Rows.Count > 0)
                {
                    TempData["SuccessMessage"] = dtDemo.Rows[0]["ThongBao"].ToString();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi duyệt Cursor tương tranh: " + ex.Message;
            }
        }

        var dt = await _db.ExecuteStoredProcedureAsync("sp_XemSach", new[]
        {
            DbHelper.P("@MaTheLoai", maTheLoai),
            DbHelper.P("@MaTacGia", maTacGia),
            DbHelper.P("@TrangThai", (string?)null),
            DbHelper.P("@Keyword", keyword)
        });
        var sachList = MapSachList(dt);

        ViewBag.TheLoaiList = await _db.ExecuteStoredProcedureAsync("usp_XemTheLoai", new[] { DbHelper.P("@Keyword", (string?)null) });
        ViewBag.TacGiaList = await _db.ExecuteStoredProcedureAsync("usp_XemTacGia", new[] { DbHelper.P("@Keyword", (string?)null) });
        ViewBag.Keyword = keyword;
        ViewBag.SelectedTheLoai = maTheLoai;
        ViewBag.SelectedTacGia = maTacGia;

        return View(sachList);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!User.IsRole("2")) return RedirectToAction("AccessDenied", "Account");
        await LoadDanhMucAsync();
        return View();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Sach model, bool? isPhantomInsert)
    {
        if (!User.IsRole("2") || !User.MaTK().HasValue)
            return RedirectToAction("AccessDenied", "Account");

        try
        {
            if (isPhantomInsert == true)
            {
                // Gọi SP Demo Phantom T2
                var dtDemo = await _db.ExecuteStoredProcedureAsync("sp_Demo_PhantomRead_T2", new[]
                {
                    new SqlParameter("@MaTheLoai", model.MaTheLoai),
                    new SqlParameter("@TenSachMoi", model.TenSach)
                });
                TempData["SuccessMessage"] = dtDemo.Rows.Count > 0 ? dtDemo.Rows[0]["ThongBao"].ToString() : "Đã chèn sách mới (Demo Phantom T2)!";
                return RedirectToAction("Index", new { maTheLoai = model.MaTheLoai });
            }

            var parameters = new[]
            {
                DbHelper.P("@MaTK", User.MaTK()!.Value),
                DbHelper.P("@TenSach", model.TenSach),
                DbHelper.P("@ISBN", model.ISBN),
                DbHelper.P("@NamXuatBan", model.NamXuatBan),
                DbHelper.P("@SoLuong", model.SoLuong),
                DbHelper.P("@GiaTien", model.GiaTien),
                DbHelper.P("@ViTriKe", model.ViTriKe),
                DbHelper.P("@MaTheLoai", model.MaTheLoai),
                DbHelper.P("@MaTacGia", model.MaTacGia),
                DbHelper.P("@MaNXB", model.MaNXB)
            };

            await _db.ExecuteStoredProcedureAsync("usp_ThuThu_ThemSach", parameters);
            TempData["SuccessMessage"] = "Thêm sách mới thành công!";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Lỗi thêm sách: " + ex.Message);
            await LoadDanhMucAsync();
            return View(model);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (!User.IsRole("2")) return RedirectToAction("AccessDenied", "Account");

        var sach = await FindSachAsync(id);
        if (sach == null) return NotFound();
        await LoadDanhMucAsync();
        return View(sach);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Sach model, string? dbmsMode, int? delaySec)
    {
        if (!User.IsRole("2") || !User.MaTK().HasValue)
            return RedirectToAction("AccessDenied", "Account");

        try
        {
            var delay = delaySec ?? 5;

            // 1. DEMO DIRTY READ T1: Sửa giá -> Delay -> ROLLBACK
            if (dbmsMode == "DirtyRead_T1")
            {
                var dtDemo = await _db.ExecuteStoredProcedureAsync("sp_Demo_DirtyRead_T1_Loi", new[]
                {
                    new SqlParameter("@MaSach", model.MaSach),
                    new SqlParameter("@GiaMoi", model.GiaTien ?? 250000m),
                    new SqlParameter("@DelaySec", delay)
                });
                TempData["SuccessMessage"] = dtDemo.Rows.Count > 0 ? dtDemo.Rows[0]["ThongBao"].ToString() : "T1: Đã chạy sửa giá và Rollback.";
                return RedirectToAction("Edit", new { id = model.MaSach });
            }

            // 2. DEMO UNREPEATABLE READ T2: Sửa giá nhanh và COMMIT giữa chừng
            if (dbmsMode == "UnrepeatableRead_T2")
            {
                var dtDemo = await _db.ExecuteStoredProcedureAsync("sp_Demo_UnrepeatableRead_T2", new[]
                {
                    new SqlParameter("@MaSach", model.MaSach),
                    new SqlParameter("@GiaThayDoi", model.GiaTien ?? 190000m)
                });
                TempData["SuccessMessage"] = dtDemo.Rows.Count > 0 ? dtDemo.Rows[0]["ThongBao"].ToString() : "T2: Đã sửa giá chen ngang thành công.";
                return RedirectToAction("Details", new { id = model.MaSach });
            }

            // 3. DEMO DEADLOCK T2: Khóa Sách -> Delay -> Khóa Độc giả 1
            if (dbmsMode == "Deadlock_T2_Loi" || dbmsMode == "Deadlock_T2_KhacPhuc")
            {
                var spName = dbmsMode == "Deadlock_T2_Loi" ? "sp_Demo_Deadlock_T2_Loi" : "sp_Demo_Deadlock_T2_KhacPhuc";
                var dtDemo = await _db.ExecuteStoredProcedureAsync(spName, new[]
                {
                    new SqlParameter("@MaDocGia", 1),
                    new SqlParameter("@MaSach", model.MaSach),
                    new SqlParameter("@DelaySec", delay)
                });
                TempData["SuccessMessage"] = dtDemo.Rows.Count > 0 ? dtDemo.Rows[0]["ThongBao"].ToString() : "Deadlock T2 hoàn tất.";
                return RedirectToAction("Edit", new { id = model.MaSach });
            }

            var parameters = new[]
            {
                DbHelper.P("@MaTK", User.MaTK()!.Value),
                DbHelper.P("@MaSach", model.MaSach),
                DbHelper.P("@TenSach", model.TenSach),
                DbHelper.P("@ISBN", model.ISBN),
                DbHelper.P("@NamXuatBan", model.NamXuatBan),
                DbHelper.P("@SoLuong", model.SoLuong),
                DbHelper.P("@GiaTien", model.GiaTien),
                DbHelper.P("@ViTriKe", model.ViTriKe),
                DbHelper.P("@TrangThai", model.TrangThai),
                DbHelper.P("@MaTheLoai", model.MaTheLoai),
                DbHelper.P("@MaTacGia", model.MaTacGia),
                DbHelper.P("@MaNXB", model.MaNXB)
            };

            await _db.ExecuteStoredProcedureAsync("usp_ThuThu_SuaSach", parameters);
            TempData["SuccessMessage"] = "Cập nhật thông tin sách thành công!";
            return RedirectToAction("Details", new { id = model.MaSach });
        }
        catch (SqlException ex)
        {
            if (ex.Number == 1205)
            {
                TempData["ErrorMessage"] = "⚠️ PHÁT HIỆN DEADLOCK (MÃ LỖI 1205): Giao tác này bị SQL Server chọn làm Deadlock Victim và Rollback để giải phóng bế tắc.";
            }
            else
            {
                TempData["ErrorMessage"] = "Lỗi SQL: " + ex.Message;
            }
            return RedirectToAction("Edit", new { id = model.MaSach });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Lỗi cập nhật sách: " + ex.Message;
            return RedirectToAction("Edit", new { id = model.MaSach });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, string? readDemoMode, int? delaySec)
    {
        var delay = delaySec ?? 5;

        // DEMO 1: UNREPEATABLE READ T1 (Đọc lần 1 -> Delay -> Đọc lại lần 2)
        if (readDemoMode == "UnrepeatableRead_T1_Loi" || readDemoMode == "UnrepeatableRead_T1_KhacPhuc")
        {
            var spName = readDemoMode == "UnrepeatableRead_T1_Loi" ? "sp_Demo_UnrepeatableRead_T1_Loi" : "sp_Demo_UnrepeatableRead_T1_KhacPhuc";
            try
            {
                var dtDemo = await _db.ExecuteStoredProcedureAsync(spName, new[]
                {
                    new SqlParameter("@MaSach", id),
                    new SqlParameter("@DelaySec", delay)
                });
                if (dtDemo.Rows.Count > 0)
                {
                    TempData["SuccessMessage"] = dtDemo.Rows[0]["ThongBao"].ToString();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi đọc: " + ex.Message;
            }
        }

        // DEMO 2: DIRTY READ T2 (Đọc bẩn READ UNCOMMITTED vs Đọc an toàn READ COMMITTED)
        if (readDemoMode == "DirtyRead_T2_Loi" || readDemoMode == "DirtyRead_T2_KhacPhuc")
        {
            var spName = readDemoMode == "DirtyRead_T2_Loi" ? "sp_Demo_DirtyRead_T2_Loi" : "sp_Demo_DirtyRead_T2_KhacPhuc";
            try
            {
                var dtDemo = await _db.ExecuteStoredProcedureAsync(spName, new[]
                {
                    new SqlParameter("@MaSach", id)
                });
                if (dtDemo.Rows.Count > 0)
                {
                    TempData["SuccessMessage"] = dtDemo.Rows[0]["ThongBao"].ToString();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi đọc: " + ex.Message;
            }
        }

        var sach = await FindSachAsync(id);
        if (sach == null)
        {
            return NotFound("Không tìm thấy thông tin sách với mã này.");
        }

        return View(sach);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        if (!User.IsRole("2"))
            return RedirectToAction("AccessDenied", "Account");

        var sach = await FindSachAsync(id);
        if (sach == null)
        {
            TempData["ErrorMessage"] = "Sách không tồn tại.";
            return RedirectToAction("Index");
        }

        return View(sach);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirm(int id)
    {
        if (!User.IsRole("2") || !User.MaTK().HasValue)
            return RedirectToAction("AccessDenied", "Account");

        try
        {
            await _db.ExecuteStoredProcedureAsync("usp_ThuThu_XoaSach", new[]
            {
                DbHelper.P("@MaTK", User.MaTK()!.Value),
                DbHelper.P("@MaSach", id)
            });
            TempData["SuccessMessage"] = "Đã xóa sách thành công.";
            return RedirectToAction("Index");
        }
        catch (SqlException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction("Delete", new { id });
        }
    }

    private async Task LoadDanhMucAsync()
    {
        ViewBag.TheLoaiList = await _db.ExecuteStoredProcedureAsync("usp_XemTheLoai", new[] { DbHelper.P("@Keyword", (string?)null) });
        ViewBag.TacGiaList = await _db.ExecuteStoredProcedureAsync("usp_XemTacGia", new[] { DbHelper.P("@Keyword", (string?)null) });
        ViewBag.NxbList = await _db.ExecuteStoredProcedureAsync("usp_XemNhaXuatBan", new[] { DbHelper.P("@Keyword", (string?)null) });
    }

    private async Task<Sach?> FindSachAsync(int maSach)
    {
        var dt = await _db.ExecuteStoredProcedureAsync("sp_XemSach", new[]
        {
            DbHelper.P("@MaTheLoai", (int?)null),
            DbHelper.P("@MaTacGia", (int?)null),
            DbHelper.P("@TrangThai", (string?)null),
            DbHelper.P("@Keyword", (string?)null)
        });
        foreach (DataRow row in dt.Rows)
        {
            if (Convert.ToInt32(row["MaSach"]) == maSach)
                return MapSach(row);
        }
        return null;
    }

    private static List<Sach> MapSachList(DataTable dt)
    {
        var list = new List<Sach>();
        foreach (DataRow row in dt.Rows)
            list.Add(MapSach(row));
        return list;
    }

    private static Sach MapSach(DataRow row)
    {
        return new Sach
        {
            MaSach = Convert.ToInt32(row["MaSach"]),
            TenSach = row["TenSach"].ToString() ?? "",
            ISBN = row["ISBN"].ToString() ?? "",
            NamXuatBan = row["NamXuatBan"] != DBNull.Value ? Convert.ToInt32(row["NamXuatBan"]) : null,
            SoLuong = Convert.ToInt32(row["SoLuong"]),
            GiaTien = row["GiaTien"] != DBNull.Value ? Convert.ToDecimal(row["GiaTien"]) : null,
            ViTriKe = row["ViTriKe"].ToString(),
            TrangThai = row["TrangThai"].ToString() ?? "Còn sách",
            MaTheLoai = Convert.ToInt32(row["MaTheLoai"]),
            TenTheLoai = row["TenTheLoai"].ToString(),
            SoNgayMuonToiDa = row.Table.Columns.Contains("SoNgayMuonToiDa") && row["SoNgayMuonToiDa"] != DBNull.Value
                ? Convert.ToInt32(row["SoNgayMuonToiDa"]) : null,
            MaTacGia = Convert.ToInt32(row["MaTacGia"]),
            TenTacGia = row["TenTacGia"].ToString(),
            MaNXB = Convert.ToInt32(row["MaNXB"]),
            TenNXB = row["TenNXB"].ToString()
        };
    }
}
