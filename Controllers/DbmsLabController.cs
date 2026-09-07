using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

public class DbmsLabController : Controller
{
    private readonly DbHelper _db;
    private readonly DbmsLabSessionStore _sessions;

    public DbmsLabController(DbHelper db, DbmsLabSessionStore sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await EnsureDemoBaselineAsync();
        await RestoreDemoBaselineAsync();

        ViewBag.SachList = await _db.ExecuteQueryAsync(
            "SELECT TOP 20 MaSach, TenSach, SoLuong, GiaTien FROM dbo.SACH ORDER BY MaSach");
        ViewBag.TheLoaiList = await _db.ExecuteQueryAsync(
            "SELECT MaTheLoai, TenTheLoai FROM dbo.THELOAI ORDER BY MaTheLoai");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Begin([FromForm] DbmsLabSession request)
    {
        if (request.TransactionName is not ("T1" or "T2"))
        {
            return BadRequest("Tên giao tác không hợp lệ.");
        }

        request.SessionId = Guid.NewGuid();
        var session = await _sessions.CreateAsync(request);
        return Json(new
        {
            sessionId = session.Info.SessionId,
            transactionName = session.Info.TransactionName,
            initialPrice = session.Info.InitialPrice
        });
    }

    [HttpPost]
    public async Task<IActionResult> ResetDemoData(int maSach)
    {
        if (maSach < 0)
        {
            return BadRequest("Mã sách không hợp lệ.");
        }

        await EnsureDemoBaselineAsync();
        var affected = await RestoreDemoBaselineAsync();

        if (affected == 0)
        {
            return NotFound("Không tìm thấy dữ liệu demo gốc để reset.");
        }

        var selectedBook = maSach > 0 ? $" cho sách #{maSach}" : string.Empty;
        return Json(new { message = $"Đã reset dữ liệu mẫu về trạng thái ban đầu{selectedBook}." });
    }

    private async Task EnsureDemoBaselineAsync()
    {
        const string sql = @"
            IF OBJECT_ID(N'dbo.DemoBaseline_SACH', N'U') IS NULL
            BEGIN
                SELECT
                    MaSach,
                    TenSach,
                    ISBN,
                    NamXuatBan,
                    SoLuong,
                    GiaTien,
                    ViTriKe,
                    TrangThai,
                    MaTheLoai,
                    MaTacGia,
                    MaNXB
                INTO dbo.DemoBaseline_SACH
                FROM dbo.SACH;
            END
            ELSE IF (SELECT COUNT(*) FROM dbo.DemoBaseline_SACH) = 0
            BEGIN
                INSERT INTO dbo.DemoBaseline_SACH (
                    MaSach,
                    TenSach,
                    ISBN,
                    NamXuatBan,
                    SoLuong,
                    GiaTien,
                    ViTriKe,
                    TrangThai,
                    MaTheLoai,
                    MaTacGia,
                    MaNXB)
                SELECT
                    MaSach,
                    TenSach,
                    ISBN,
                    NamXuatBan,
                    SoLuong,
                    GiaTien,
                    ViTriKe,
                    TrangThai,
                    MaTheLoai,
                    MaTacGia,
                    MaNXB
                FROM dbo.SACH;
            END;";

        await _db.ExecuteNonQueryAsync(sql);
    }

    private async Task<int> RestoreDemoBaselineAsync(int? maSach = null)
    {
        var whereClause = maSach.HasValue
            ? "WHERE s.MaSach = @MaSach"
            : string.Empty;

        var paramList = maSach.HasValue
            ? new[] { new SqlParameter("@MaSach", maSach.Value) }
            : Array.Empty<SqlParameter>();

        const string updateSql = @"
            UPDATE s
            SET
                s.TenSach = b.TenSach,
                s.ISBN = b.ISBN,
                s.NamXuatBan = b.NamXuatBan,
                s.SoLuong = b.SoLuong,
                s.GiaTien = b.GiaTien,
                s.ViTriKe = b.ViTriKe,
                s.TrangThai = b.TrangThai,
                s.MaTheLoai = b.MaTheLoai,
                s.MaTacGia = b.MaTacGia,
                s.MaNXB = b.MaNXB
            FROM dbo.SACH s
            INNER JOIN dbo.DemoBaseline_SACH b
                ON s.MaSach = b.MaSach
            {0};

            DELETE s
            FROM dbo.SACH s
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.DemoBaseline_SACH b
                WHERE b.MaSach = s.MaSach
            );

            INSERT INTO dbo.SACH (
                TenSach,
                ISBN,
                NamXuatBan,
                SoLuong,
                GiaTien,
                ViTriKe,
                TrangThai,
                MaTheLoai,
                MaTacGia,
                MaNXB)
            SELECT
                b.TenSach,
                b.ISBN,
                b.NamXuatBan,
                b.SoLuong,
                b.GiaTien,
                b.ViTriKe,
                b.TrangThai,
                b.MaTheLoai,
                b.MaTacGia,
                b.MaNXB
            FROM dbo.DemoBaseline_SACH b
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.SACH s
                WHERE s.MaSach = b.MaSach
            );";

        var finalSql = string.Format(updateSql, whereClause);
        return await _db.ExecuteNonQueryAsync(finalSql, paramList);
    }

    [HttpPost]
    public async Task<IActionResult> RunSimulation(
        string problemType, string mode, int delaySec, int maSach, int maTheLoai)
    {
        var result = new ConcurrencyResultViewModel
        {
            ProblemType = problemType,
            Mode = mode,
            DelaySeconds = delaySec
        };

        try
        {
            var t1Procedure = problemType switch
            {
                "LostUpdate" => mode == "Loi" ? "sp_Demo_LostUpdate_T1_Loi" : "sp_Demo_LostUpdate_T1_KhacPhuc",
                "DirtyRead" => "sp_Demo_DirtyRead_T1_Loi",
                "UnrepeatableRead" => mode == "Loi" ? "sp_Demo_UnrepeatableRead_T1_Loi" : "sp_Demo_UnrepeatableRead_T1_KhacPhuc",
                "PhantomRead" => mode == "Loi" ? "sp_Demo_PhantomRead_T1_Loi" : "sp_Demo_PhantomRead_T1_KhacPhuc",
                "Deadlock" => mode == "Loi" ? "sp_Demo_Deadlock_T1_Loi" : "sp_Demo_Deadlock_T1_KhacPhuc",
                _ => throw new ArgumentException("Loại vấn đề tương tranh không hợp lệ.")
            };
            var t2Procedure = problemType switch
            {
                "LostUpdate" => mode == "Loi" ? "sp_Demo_LostUpdate_T2_Loi" : "sp_Demo_LostUpdate_T2_KhacPhuc",
                "DirtyRead" => mode == "Loi" ? "sp_Demo_DirtyRead_T2_Loi" : "sp_Demo_DirtyRead_T2_KhacPhuc",
                "UnrepeatableRead" => "sp_Demo_UnrepeatableRead_T2",
                "PhantomRead" => "sp_Demo_PhantomRead_T2",
                "Deadlock" => mode == "Loi" ? "sp_Demo_Deadlock_T2_Loi" : "sp_Demo_Deadlock_T2_KhacPhuc",
                _ => throw new ArgumentException("Loại vấn đề tương tranh không hợp lệ.")
            };

            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "Hệ thống",
                StepName = "Demo tự động",
                Detail = "Hai stored procedure được chạy tự động như phiên bản lab cũ.",
                SqlExecuted = $"{t1Procedure} / {t2Procedure}"
            });

            var task1 = ExecuteLegacyProcedureAsync(result, "T1", t1Procedure, problemType, maSach, maTheLoai, delaySec);
            await Task.Delay(problemType == "Deadlock" ? 500 : 1500);
            var task2 = ExecuteLegacyProcedureAsync(result, "T2", t2Procedure, problemType, maSach, maTheLoai, 0);
            result.T1Result = await task1;
            result.T2Result = await task2;
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            result.IsDeadlockVictim = true;
            result.SqlErrorCode = 1205;
            result.ErrorMessage = "Một giao tác bị SQL Server chọn làm deadlock victim (1205).";
        }
        catch (ArgumentException ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return Json(result);
    }

    private async Task<string> ExecuteLegacyProcedureAsync(
        ConcurrencyResultViewModel result, string transactionName, string procedure,
        string problemType, int maSach, int maTheLoai, int delaySec)
    {
        result.Logs.Add(new ConcurrencyStepLog
        {
            TransactionName = transactionName,
            StepName = $"{transactionName}: chạy tự động",
            SqlExecuted = $"EXEC {procedure}"
        });

        var parameters = problemType switch
        {
            "PhantomRead" => new[]
            {
                new SqlParameter("@MaTheLoai", maTheLoai),
                new SqlParameter("@DelaySec", delaySec)
            },
            "Deadlock" => new[]
            {
                new SqlParameter("@MaDocGia", 1),
                new SqlParameter("@MaSach", maSach),
                new SqlParameter("@DelaySec", delaySec)
            },
            "DirtyRead" => transactionName == "T1"
                ? new[] { new SqlParameter("@MaSach", maSach), new SqlParameter("@GiaMoi", 250000m), new SqlParameter("@DelaySec", delaySec) }
                : new[] { new SqlParameter("@MaSach", maSach) },
            "UnrepeatableRead" => transactionName == "T1"
                ? new[] { new SqlParameter("@MaSach", maSach), new SqlParameter("@DelaySec", delaySec) }
                : new[] { new SqlParameter("@MaSach", maSach), new SqlParameter("@GiaThayDoi", 190000m) },
            _ => transactionName == "T1"
                ? new[] { new SqlParameter("@MaSach", maSach), new SqlParameter("@DelaySec", delaySec) }
                : new[] { new SqlParameter("@MaSach", maSach) }
        };

        var table = await _db.ExecuteStoredProcedureAsync(procedure, parameters);
        return table.Rows.Count > 0 ? table.Rows[0]["ThongBao"].ToString() ?? "Hoàn tất" : "Hoàn tất";
    }

    [HttpPost]
    public async Task<IActionResult> Step([FromBody] DbmsLabStepRequest request)
    {
        if (!_sessions.TryGet(request.SessionId, out var session) || session is null)
        {
            return NotFound(new DbmsLabStepResponse { Status = "Error", Message = "Phiên giao tác không tồn tại hoặc đã hết hạn." });
        }

        try
        {
            var response = await session.ExecuteAsync(() => ExecuteStepAsync(session, request.Operation));
            if (!session.IsActive)
            {
                _sessions.Remove(request.SessionId);
            }
            return Json(response);
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            _sessions.Remove(request.SessionId);
            return Json(new DbmsLabStepResponse
            {
                Status = "Deadlock",
                Message = $"{session.Info.TransactionName} bị chọn làm deadlock victim (SQL Server 1205).",
                IsActive = false
            });
        }
    }

    [HttpPost]
    public IActionResult Rollback([FromBody] DbmsLabStepRequest request)
    {
        if (!_sessions.TryGet(request.SessionId, out var session) || session is null)
        {
            return NotFound();
        }
        session.Complete(false);
        _sessions.Remove(request.SessionId);
        return Json(new DbmsLabStepResponse { Message = "Đã rollback giao tác.", IsActive = false });
    }

    private static async Task<DbmsLabStepResponse> ExecuteStepAsync(
        DbmsLabSessionStore.SessionState session, string operation)
    {
        var normalized = operation.Trim().ToLowerInvariant();
        if (normalized is "commit" or "rollback")
        {
            session.Complete(normalized == "commit");
            return new DbmsLabStepResponse
            {
                Message = normalized == "commit" ? "Đã commit giao tác." : "Đã rollback giao tác.",
                IsActive = false
            };
        }

        var command = new SqlCommand { Connection = session.Connection, Transaction = session.Transaction };
        try
        {
            switch (session.Info.ProblemType, normalized)
            {
                case ("LostUpdate", "read"):
                    command.CommandText = "SELECT SoLuong FROM dbo.SACH WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    session.LastQuantity = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return Result($"Đọc SoLuong = {session.LastQuantity}.", session);
                case ("LostUpdate", "update"):
                    command.CommandText = "UPDATE dbo.SACH SET SoLuong = @SoLuong WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    command.Parameters.AddWithValue("@SoLuong", (session.LastQuantity ?? 0) - 1);
                    await command.ExecuteNonQueryAsync();
                    return Result($"Ghi SoLuong = {(session.LastQuantity ?? 0) - 1}.", session);
                case ("DirtyRead", "update"):
                    command.CommandText = "SELECT GiaTien FROM dbo.SACH WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    var oldPrice = await command.ExecuteScalarAsync();
                    command.Parameters.Clear();
                    command.CommandText = "UPDATE dbo.SACH SET GiaTien = @GiaTien WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    command.Parameters.AddWithValue("@GiaTien", 250000m);
                    await command.ExecuteNonQueryAsync();
                    return Result($"Giá: {Convert.ToDecimal(oldPrice):N0} → 250.000 (chưa commit).", session);
                case ("DirtyRead", "read"):
                    command.CommandText = "SELECT GiaTien FROM dbo.SACH WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    var dirtyPrice = await command.ExecuteScalarAsync();
                    return Result($"T2 đọc được giá = {Convert.ToDecimal(dirtyPrice):N0}.", session);
                case ("UnrepeatableRead", "read"):
                    command.CommandText = "SELECT GiaTien FROM dbo.SACH WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    session.LastPrice = Convert.ToDecimal(await command.ExecuteScalarAsync());
                    return Result($"Đọc GiaTien = {session.LastPrice:N0}.", session);
                case ("UnrepeatableRead", "update"):
                    command.CommandText = "UPDATE dbo.SACH SET GiaTien = @GiaTien WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    command.Parameters.AddWithValue("@GiaTien", 190000m);
                    await command.ExecuteNonQueryAsync();
                    return Result("Đã cập nhật giá thành 190.000.", session);
                case ("PhantomRead", "read"):
                    command.CommandText = "SELECT COUNT(*) FROM dbo.SACH WHERE MaTheLoai = @MaTheLoai";
                    command.Parameters.AddWithValue("@MaTheLoai", session.Info.MaTheLoai);
                    return Result($"Đếm được {await command.ExecuteScalarAsync()} đầu sách.", session);
                case ("PhantomRead", "update"):
                    command.CommandText = """
                        INSERT INTO dbo.SACH
                            (TenSach, ISBN, NamXuatBan, SoLuong, GiaTien, ViTriKe, TrangThai, MaTheLoai, MaTacGia, MaNXB)
                        VALUES (@TenSach, @ISBN, YEAR(GETDATE()), 5, 120000, N'Kệ Lab', N'Còn sách', @MaTheLoai, 1, 1)
                        """;
                    command.Parameters.AddWithValue("@TenSach", $"Sách Lab {DateTime.UtcNow:HHmmss}");
                    command.Parameters.AddWithValue("@ISBN", $"LAB-{Guid.NewGuid():N}"[..13]);
                    command.Parameters.AddWithValue("@MaTheLoai", session.Info.MaTheLoai);
                    await command.ExecuteNonQueryAsync();
                    return Result("Đã INSERT một bản ghi vào phạm vi thể loại.", session);
                case ("Deadlock", "lock-first"):
                    command.CommandText = session.Info.TransactionName == "T1"
                        ? "SELECT MaDocGia FROM dbo.DOCGIA WITH (UPDLOCK, HOLDLOCK) WHERE MaDocGia = 1"
                        : "SELECT MaSach FROM dbo.SACH WITH (UPDLOCK, HOLDLOCK) WHERE MaSach = @MaSach";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    await command.ExecuteScalarAsync();
                    return Result("Đã khóa tài nguyên thứ nhất.", session);
                case ("Deadlock", "lock-second"):
                    command.CommandText = session.Info.TransactionName == "T1"
                        ? "SELECT MaSach FROM dbo.SACH WITH (UPDLOCK, HOLDLOCK) WHERE MaSach = @MaSach"
                        : "SELECT MaDocGia FROM dbo.DOCGIA WITH (UPDLOCK, HOLDLOCK) WHERE MaDocGia = 1";
                    command.Parameters.AddWithValue("@MaSach", session.Info.MaSach);
                    await command.ExecuteScalarAsync();
                    return Result("Đã khóa tài nguyên thứ hai.", session);
                default:
                    return new DbmsLabStepResponse { Status = "Error", Message = "Thao tác không phù hợp với kịch bản." };
            }
        }
        finally
        {
            await command.DisposeAsync();
        }
    }

    private static DbmsLabStepResponse Result(string message, DbmsLabSessionStore.SessionState session) =>
        new() { Message = message, IsActive = session.IsActive };
}
