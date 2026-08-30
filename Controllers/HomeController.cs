using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

public class HomeController : Controller
{
    private readonly DbHelper _db;

    public HomeController(DbHelper db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var role = User.Role();
            var maTK = User.MaTK();

            // Mini chỉ role 2/3/4 và bắt buộc @MaTK — khách/độc giả không gọi.
            if (User.Identity?.IsAuthenticated == true && maTK.HasValue && User.IsRole("2", "3", "4"))
            {
                var ds = await _db.ExecuteStoredProcedureDataSetAsync("usp_ThongKeMini", new[]
                {
                    DbHelper.P("@MaTK", maTK.Value)
                });
                ViewBag.MiniSach = ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0 ? ds.Tables[0].Rows[0] : null;
                ViewBag.SoPhieuDangMuon = ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0
                    ? ds.Tables[1].Rows[0]["SoPhieuDangMuon"] : null;
                ViewBag.SoPhieuQuaHan = ds.Tables.Count > 2 && ds.Tables[2].Rows.Count > 0
                    ? ds.Tables[2].Rows[0]["SoPhieuQuaHan"] : null;
            }

            var dtSachMoi = await _db.ExecuteStoredProcedureAsync("sp_XemSach", new[]
            {
                DbHelper.P("@MaTheLoai", (int?)null),
                DbHelper.P("@MaTacGia", (int?)null),
                DbHelper.P("@TrangThai", "Còn sách"),
                DbHelper.P("@Keyword", (string?)null)
            });
            ViewBag.SachMoi = dtSachMoi;
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
