using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

[Authorize]
public class ThongKeController : Controller
{
    private readonly DbHelper _db;

    public ThongKeController(DbHelper db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var maTK = User.MaTK();
        if (!maTK.HasValue)
            return RedirectToAction("Login", "Account");

        if (User.IsRole("1"))
            return RedirectToAction("AccessDenied", "Account");

        try
        {
            if (User.IsRole("2"))
            {
                var dsMini = await _db.ExecuteStoredProcedureDataSetAsync("usp_ThongKeMini", new[]
                {
                    DbHelper.P("@MaTK", maTK.Value)
                });
                BindMini(dsMini);
                ViewBag.IsDayDu = false;
                return View();
            }

            if (!User.IsRole("3", "4"))
                return RedirectToAction("AccessDenied", "Account");

            var ds = await _db.ExecuteStoredProcedureDataSetAsync("usp_ThongKeDayDu", new[]
            {
                DbHelper.P("@MaTK", maTK.Value)
            });

            BindMini(ds);
            ViewBag.IsDayDu = true;
            ViewBag.TheLoai = ds.Tables.Count > 3 ? ds.Tables[3] : null;
            ViewBag.TopDocGia = ds.Tables.Count > 4 ? ds.Tables[4] : null;
            ViewBag.TheoThang = ds.Tables.Count > 5 ? ds.Tables[5] : null;

            ViewBag.QuaHan = await _db.ExecuteQueryAsync("SELECT TOP 20 * FROM dbo.v_PhieuMuonQuaHan");
            ViewBag.SachConMuon = await _db.ExecuteQueryAsync("SELECT * FROM dbo.v_SachConMuon");
            ViewBag.LichSu = await _db.ExecuteQueryAsync("SELECT TOP 50 * FROM dbo.v_LichSuMuonTra ORDER BY NgayMuon DESC");
        }
        catch (Exception ex)
        {
            ViewBag.Error = ex.Message;
        }

        return View();
    }

    private void BindMini(DataSet ds)
    {
        ViewBag.MiniSach = ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0 ? ds.Tables[0].Rows[0] : null;
        ViewBag.SoPhieuDangMuon = ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0
            ? ds.Tables[1].Rows[0]["SoPhieuDangMuon"] : null;
        ViewBag.SoPhieuQuaHan = ds.Tables.Count > 2 && ds.Tables[2].Rows.Count > 0
            ? ds.Tables[2].Rows[0]["SoPhieuQuaHan"] : null;
    }
}
