using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;

namespace QuanLyThuVien.Controllers;

public class DbmsLabController : Controller
{
    private readonly DbHelper _db;

    public DbmsLabController(DbHelper db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Load sample books and categories for dropdowns
        var dtSach = await _db.ExecuteQueryAsync("SELECT TOP 20 MaSach, TenSach, SoLuong, GiaTien FROM dbo.SACH ORDER BY MaSach");
        var dtTheLoai = await _db.ExecuteQueryAsync("SELECT MaTheLoai, TenTheLoai FROM dbo.THELOAI ORDER BY MaTheLoai");

        ViewBag.SachList = dtSach;
        ViewBag.TheLoaiList = dtTheLoai;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RunSimulation(string problemType, string mode, int delaySec, int maSach, int maTheLoai)
    {
        var result = new ConcurrencyResultViewModel
        {
            ProblemType = problemType,
            Mode = mode,
            DelaySeconds = delaySec
        };

        try
        {
            switch (problemType)
            {
                case "LostUpdate":
                    await ExecuteLostUpdateAsync(result, mode, delaySec, maSach);
                    break;
                case "DirtyRead":
                    await ExecuteDirtyReadAsync(result, mode, delaySec, maSach);
                    break;
                case "UnrepeatableRead":
                    await ExecuteUnrepeatableReadAsync(result, mode, delaySec, maSach);
                    break;
                case "PhantomRead":
                    await ExecutePhantomReadAsync(result, mode, delaySec, maTheLoai);
                    break;
                case "Deadlock":
                    await ExecuteDeadlockAsync(result, mode, delaySec, maSach);
                    break;
                default:
                    result.ErrorMessage = "Loại vấn đề tương tranh không hợp lệ.";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return Json(result);
    }

    private async Task ExecuteLostUpdateAsync(ConcurrencyResultViewModel result, string mode, int delaySec, int maSach)
    {
        result.IsolationLevelUsed = mode == "Loi" ? "READ COMMITTED" : "READ COMMITTED + UPDLOCK, HOLDLOCK";
        result.LockingHintUsed = mode == "Loi" ? "Không có khóa giữ (Shared Lock nhả ngay)" : "WITH (UPDLOCK, HOLDLOCK)";

        result.Logs.Add(new ConcurrencyStepLog
        {
            TransactionName = "Hệ thống",
            StepName = "Bắt đầu kịch bản",
            Detail = $"Kích hoạt 2 Giao tác T1 và T2 cùng mượn Mã sách {maSach} (Độ trễ T1: {delaySec}s)",
            SqlExecuted = mode == "Loi" ? "EXEC sp_Demo_LostUpdate_T1_Loi vs T2_Loi" : "EXEC sp_Demo_LostUpdate_T1_KhacPhuc vs T2_KhacPhuc"
        });

        var t1Sp = mode == "Loi" ? "sp_Demo_LostUpdate_T1_Loi" : "sp_Demo_LostUpdate_T1_KhacPhuc";
        var t2Sp = mode == "Loi" ? "sp_Demo_LostUpdate_T2_Loi" : "sp_Demo_LostUpdate_T2_KhacPhuc";

        // Chạy T1 (có delay)
        var task1 = Task.Run(async () =>
        {
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T1",
                StepName = "T1: Đọc & Bắt đầu xử lý",
                Detail = $"T1 bắt đầu đọc số lượng sách {maSach} và giữ giao tác trong {delaySec}s",
                SqlExecuted = $"EXEC {t1Sp} @MaSach={maSach}, @DelaySec={delaySec}"
            });
            var dt1 = await _db.ExecuteStoredProcedureAsync(t1Sp, new[]
            {
                new SqlParameter("@MaSach", maSach),
                new SqlParameter("@DelaySec", delaySec)
            });
            return dt1.Rows.Count > 0 ? dt1.Rows[0]["ThongBao"].ToString() : "T1 Hoàn tất";
        });

        // Chờ 1 giây rồi kích hoạt T2 chen ngang
        await Task.Delay(1000);

        var task2 = Task.Run(async () =>
        {
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T2",
                StepName = "T2: Chen ngang mượn sách",
                Detail = mode == "Loi" ? "T2 đọc số lượng cũ khi T1 chưa commit -> Ghi đè số lượng" : "T2 bị chặn lại (Block) chờ T1 nhả khóa UPDLOCK",
                SqlExecuted = $"EXEC {t2Sp} @MaSach={maSach}"
            });
            var dt2 = await _db.ExecuteStoredProcedureAsync(t2Sp, new[]
            {
                new SqlParameter("@MaSach", maSach)
            });
            return dt2.Rows.Count > 0 ? dt2.Rows[0]["ThongBao"].ToString() : "T2 Hoàn tất";
        });

        result.T2Result = await task2;
        result.T1Result = await task1;

        if (mode == "Loi")
        {
            result.Explanation = "LỖI MẤT CẬP NHẬT: Cả T1 và T2 đều đọc số lượng tồn kho ban đầu và cùng tính toán giảm đi 1, sau đó T2 ghi đè lên kết quả của T1 làm mất 1 lượt ghi nhận hoặc tồn kho bị âm!";
            result.DbmsBehavior = "SQL Server ở mức READ COMMITTED chỉ giữ Shared Lock trong lúc đọc dòng rồi nhả ngay, không ngăn được T2 chen vào đọc và sửa.";
        }
        else
        {
            result.Explanation = "ĐÃ KHẮC PHỤC: T1 sử dụng khóa UPDLOCK và HOLDLOCK giữ khóa đến hết giao tác. T2 bắt buộc phải xếp hàng chờ T1 kết thúc. Khi T1 mượn xong trừ hết sách, T2 đọc lại thấy hết sách và bị từ chối an toàn.";
            result.DbmsBehavior = "UPDLOCK chuyển thành Exclusive Lock khi cập nhật, triệt tiêu hoàn toàn khả năng đọc/ghi đè đồng thời.";
        }
    }

    private async Task ExecuteDirtyReadAsync(ConcurrencyResultViewModel result, string mode, int delaySec, int maSach)
    {
        result.IsolationLevelUsed = mode == "Loi" ? "T2: READ UNCOMMITTED" : "T2: READ COMMITTED (Mặc định)";
        result.LockingHintUsed = mode == "Loi" ? "NOLOCK / READUNCOMMITTED" : "Shared Lock (Chặn đọc bẩn)";

        var t1Sp = "sp_Demo_DirtyRead_T1_Loi";
        var t2Sp = mode == "Loi" ? "sp_Demo_DirtyRead_T2_Loi" : "sp_Demo_DirtyRead_T2_KhacPhuc";

        var task1 = Task.Run(async () =>
        {
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T1",
                StepName = "T1: Đổi giá sách tạm thời",
                Detail = $"T1 sửa giá sách {maSach} lên 250.000đ, giữ {delaySec}s rồi ROLLBACK",
                SqlExecuted = $"EXEC {t1Sp} @MaSach={maSach}, @GiaMoi=250000, @DelaySec={delaySec}"
            });
            var dt1 = await _db.ExecuteStoredProcedureAsync(t1Sp, new[]
            {
                new SqlParameter("@MaSach", maSach),
                new SqlParameter("@GiaMoi", 250000m),
                new SqlParameter("@DelaySec", delaySec)
            });
            return dt1.Rows.Count > 0 ? dt1.Rows[0]["ThongBao"].ToString() : "T1 Hoàn tất";
        });

        // Đợi 1.5s để T1 update xong vào DB (chưa commit) rồi T2 đọc
        await Task.Delay(1500);

        var task2 = Task.Run(async () =>
        {
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T2",
                StepName = "T2: Đọc dữ liệu",
                Detail = mode == "Loi" ? "T2 đọc bằng READ UNCOMMITTED (thấy giá 250k rác)" : "T2 đọc bằng READ COMMITTED (bị chặn chờ T1 Rollback xong mới đọc)",
                SqlExecuted = $"EXEC {t2Sp} @MaSach={maSach}"
            });
            var dt2 = await _db.ExecuteStoredProcedureAsync(t2Sp, new[]
            {
                new SqlParameter("@MaSach", maSach)
            });
            return dt2.Rows.Count > 0 ? dt2.Rows[0]["ThongBao"].ToString() : "T2 Hoàn tất";
        });

        result.T2Result = await task2;
        result.T1Result = await task1;

        if (mode == "Loi")
        {
            result.Explanation = "LỖI ĐỌC RÁC (DIRTY READ): T2 đã đọc giá tiền 250.000đ trong khi T1 chưa hề Commit. Ngay sau đó T1 Rollback lại giá cũ, khiến dữ liệu T2 đọc được là hoàn toàn sai lệch!";
            result.DbmsBehavior = "Mức cô lập READ UNCOMMITTED bỏ qua Exclusive Lock của T1 và đọc thẳng dữ liệu từ Buffer/Dirty Page.";
        }
        else
        {
            result.Explanation = "ĐÃ KHẮC PHỤC: T2 ở mức READ COMMITTED yêu cầu Shared Lock, do T1 đang giữ Exclusive Lock nên T2 bị chặn lại. Khi T1 Rollback xong, T2 mới đọc được giá trị thật đã được xác thực.";
            result.DbmsBehavior = "READ COMMITTED đảm bảo không bao giờ đọc dữ liệu chưa được Commit.";
        }
    }

    private async Task ExecuteUnrepeatableReadAsync(ConcurrencyResultViewModel result, string mode, int delaySec, int maSach)
    {
        result.IsolationLevelUsed = mode == "Loi" ? "T1: READ COMMITTED" : "T1: REPEATABLE READ";
        result.LockingHintUsed = mode == "Loi" ? "Shared Lock nhả ngay sau lệnh SELECT" : "Shared Lock giữ đến hết Transaction";

        var t1Sp = mode == "Loi" ? "sp_Demo_UnrepeatableRead_T1_Loi" : "sp_Demo_UnrepeatableRead_T1_KhacPhuc";
        var t2Sp = "sp_Demo_UnrepeatableRead_T2";

        var task1 = Task.Run(async () =>
        {
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T1",
                StepName = "T1: Đọc lần 1",
                Detail = $"T1 đọc giá sách lần 1, tạm dừng {delaySec}s rồi đọc lại lần 2",
                SqlExecuted = $"EXEC {t1Sp} @MaSach={maSach}, @DelaySec={delaySec}"
            });
            var dt1 = await _db.ExecuteStoredProcedureAsync(t1Sp, new[]
            {
                new SqlParameter("@MaSach", maSach),
                new SqlParameter("@DelaySec", delaySec)
            });
            return dt1.Rows.Count > 0 ? dt1.Rows[0]["ThongBao"].ToString() : "T1 Hoàn tất";
        });

        // Chờ 1.5s rồi T2 chen ngang sửa giá
        await Task.Delay(1500);

        var task2 = Task.Run(async () =>
        {
            var giaMoi = new Random().Next(150, 290) * 1000m;
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T2",
                StepName = "T2: Cập nhật giá giữa chừng",
                Detail = mode == "Loi" ? $"T2 sửa giá sách thành {giaMoi:N0}đ và Commit" : "T2 bị chặn vì T1 đang giữ khóa Shared Lock (REPEATABLE READ)",
                SqlExecuted = $"EXEC {t2Sp} @MaSach={maSach}, @GiaThayDoi={giaMoi}"
            });
            var dt2 = await _db.ExecuteStoredProcedureAsync(t2Sp, new[]
            {
                new SqlParameter("@MaSach", maSach),
                new SqlParameter("@GiaThayDoi", giaMoi)
            });
            return dt2.Rows.Count > 0 ? dt2.Rows[0]["ThongBao"].ToString() : "T2 Hoàn tất";
        });

        result.T2Result = await task2;
        result.T1Result = await task1;

        if (mode == "Loi")
        {
            result.Explanation = "LỖI KHÔNG ĐỌC LẶP LẠI (UNREPEATABLE READ): Trong cùng 1 giao tác T1, 2 lần đọc giá của CÙNG MỘT DÒNG cho ra 2 kết quả khác nhau do T2 chen vào giữa sửa đổi.";
            result.DbmsBehavior = "READ COMMITTED chỉ khóa Shared trong khoảnh khắc đọc rồi nhả ngay, cho phép Transaction khác UPDATE/DELETE bản ghi.";
        }
        else
        {
            result.Explanation = "ĐÃ KHẮC PHỤC: REPEATABLE READ giữ khóa Shared Lock trên dòng dữ liệu cho đến khi T1 Commit. T2 muốn sửa phải chờ T1 kết thúc, đảm bảo T1 luôn đọc kết quả nhất quán.";
            result.DbmsBehavior = "Khóa Shared Lock được duy trì xuyên suốt vòng đời của Transaction.";
        }
    }

    private async Task ExecutePhantomReadAsync(ConcurrencyResultViewModel result, string mode, int delaySec, int maTheLoai)
    {
        result.IsolationLevelUsed = mode == "Loi" ? "T1: REPEATABLE READ (Chưa chặn được INSERT)" : "T1: SERIALIZABLE (Key-Range Locks)";
        result.LockingHintUsed = mode == "Loi" ? "Row-level Shared Lock" : "RangeS-S / Key-Range Lock";

        var t1Sp = mode == "Loi" ? "sp_Demo_PhantomRead_T1_Loi" : "sp_Demo_PhantomRead_T1_KhacPhuc";
        var t2Sp = "sp_Demo_PhantomRead_T2";

        var task1 = Task.Run(async () =>
        {
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T1",
                StepName = "T1: Mở CURSOR duyệt danh sách lần 1",
                Detail = $"T1 mở con trỏ Cursor đếm toàn bộ sách thể loại {maTheLoai}, delay {delaySec}s rồi duyệt lại",
                SqlExecuted = $"EXEC {t1Sp} @MaTheLoai={maTheLoai}, @DelaySec={delaySec}"
            });
            var dt1 = await _db.ExecuteStoredProcedureAsync(t1Sp, new[]
            {
                new SqlParameter("@MaTheLoai", maTheLoai),
                new SqlParameter("@DelaySec", delaySec)
            });
            return dt1.Rows.Count > 0 ? dt1.Rows[0]["ThongBao"].ToString() : "T1 Hoàn tất";
        });

        // Chờ 1.5s rồi T2 chèn sách mới
        await Task.Delay(1500);

        var task2 = Task.Run(async () =>
        {
            var tenSachMoi = "Sách Mới Demo Bóng Ma #" + new Random().Next(100, 999);
            result.Logs.Add(new ConcurrencyStepLog
            {
                TransactionName = "T2",
                StepName = "T2: Chèn thêm sách mới (INSERT)",
                Detail = mode == "Loi" ? $"T2 INSERT '{tenSachMoi}' vào thể loại {maTheLoai} thành công" : "T2 bị chặn khi INSERT vì T1 đang giữ khóa Range Lock (SERIALIZABLE)",
                SqlExecuted = $"EXEC {t2Sp} @MaTheLoai={maTheLoai}, @TenSachMoi='{tenSachMoi}'"
            });
            var dt2 = await _db.ExecuteStoredProcedureAsync(t2Sp, new[]
            {
                new SqlParameter("@MaTheLoai", maTheLoai),
                new SqlParameter("@TenSachMoi", tenSachMoi)
            });
            return dt2.Rows.Count > 0 ? dt2.Rows[0]["ThongBao"].ToString() : "T2 Hoàn tất";
        });

        result.T2Result = await task2;
        result.T1Result = await task1;

        if (mode == "Loi")
        {
            result.Explanation = "LỖI BÓNG MA (PHANTOM READ): REPEATABLE READ chỉ khóa các dòng ĐANG TỒN TẠI. Khi T2 INSERT thêm dòng mới, lần duyệt Cursor thứ 2 của T1 xuất hiện thêm bản ghi bóng ma mà lần 1 không có!";
            result.DbmsBehavior = "Số lượng bản ghi thỏa mãn điều kiện WHERE bị thay đổi giữa 2 lần truy vấn.";
        }
        else
        {
            result.Explanation = "ĐÃ KHẮC PHỤC: Mức cô lập SERIALIZABLE sử dụng Range Lock (Khóa khoảng). Khi T1 truy vấn thể loại X, toàn bộ khoảng dữ liệu bị khóa khiến T2 không thể chèn thêm sách mới cho đến khi T1 kết thúc.";
            result.DbmsBehavior = "SQL Server tạo Key-Range Lock trên chỉ mục, triệt tiêu hoàn toàn hiện tượng Bóng ma.";
        }
    }

    private async Task ExecuteDeadlockAsync(ConcurrencyResultViewModel result, string mode, int delaySec, int maSach)
    {
        result.IsolationLevelUsed = "READ COMMITTED + XLOCK (Khóa độc quyền)";
        result.LockingHintUsed = mode == "Loi" ? "Khóa chéo tài nguyên (T1: Độc giả->Sách, T2: Sách->Độc giả)" : "Chuẩn hóa Resource Ordering (Luôn khóa Độc giả trước -> Sách sau)";

        var t1Sp = mode == "Loi" ? "sp_Demo_Deadlock_T1_Loi" : "sp_Demo_Deadlock_T1_KhacPhuc";
        var t2Sp = mode == "Loi" ? "sp_Demo_Deadlock_T2_Loi" : "sp_Demo_Deadlock_T2_KhacPhuc";

        var maDocGia = 1;

        var task1 = Task.Run(async () =>
        {
            try
            {
                result.Logs.Add(new ConcurrencyStepLog
                {
                    TransactionName = "T1",
                    StepName = "T1: Bắt đầu khóa tài nguyên",
                    Detail = mode == "Loi" ? $"T1 khóa Độc giả {maDocGia} -> Chờ {delaySec}s -> Đòi khóa Sách {maSach}" : "T1 khóa theo thứ tự chuẩn (Độc giả -> Sách)",
                    SqlExecuted = $"EXEC {t1Sp} @MaDocGia={maDocGia}, @MaSach={maSach}, @DelaySec={delaySec}"
                });
                var dt1 = await _db.ExecuteStoredProcedureAsync(t1Sp, new[]
                {
                    new SqlParameter("@MaDocGia", maDocGia),
                    new SqlParameter("@MaSach", maSach),
                    new SqlParameter("@DelaySec", delaySec)
                });
                return dt1.Rows.Count > 0 ? dt1.Rows[0]["ThongBao"].ToString() : "T1 Hoàn tất";
            }
            catch (SqlException ex)
            {
                if (ex.Number == 1205)
                {
                    result.IsDeadlockVictim = true;
                    result.SqlErrorCode = 1205;
                    return "T1 BỊ HỦY DO DEADLOCK (Lỗi 1205: Deadlock victim)! SQL Server đã tự động Rollback T1 để giải phóng bế tắc.";
                }
                return "T1 Lỗi: " + ex.Message;
            }
        });

        // Chờ 500ms rồi T2 chạy
        await Task.Delay(500);

        var task2 = Task.Run(async () =>
        {
            try
            {
                result.Logs.Add(new ConcurrencyStepLog
                {
                    TransactionName = "T2",
                    StepName = "T2: Bắt đầu khóa tài nguyên",
                    Detail = mode == "Loi" ? $"T2 khóa Sách {maSach} -> Chờ {delaySec}s -> Đòi khóa Độc giả {maDocGia} (GÂY DEADLOCK!)" : "T2 khóa theo thứ tự chuẩn (Độc giả -> Sách - T2 sẽ chờ tuần tự)",
                    SqlExecuted = $"EXEC {t2Sp} @MaDocGia={maDocGia}, @MaSach={maSach}, @DelaySec={delaySec}"
                });
                var dt2 = await _db.ExecuteStoredProcedureAsync(t2Sp, new[]
                {
                    new SqlParameter("@MaDocGia", maDocGia),
                    new SqlParameter("@MaSach", maSach),
                    new SqlParameter("@DelaySec", delaySec)
                });
                return dt2.Rows.Count > 0 ? dt2.Rows[0]["ThongBao"].ToString() : "T2 Hoàn tất";
            }
            catch (SqlException ex)
            {
                if (ex.Number == 1205)
                {
                    result.IsDeadlockVictim = true;
                    result.SqlErrorCode = 1205;
                    return "T2 BỊ HỦY DO DEADLOCK (Lỗi 1205: Deadlock victim)! SQL Server đã tự động Rollback T2 để giải phóng bế tắc.";
                }
                return "T2 Lỗi: " + ex.Message;
            }
        });

        result.T1Result = await task1;
        result.T2Result = await task2;

        if (mode == "Loi")
        {
            result.Explanation = "DEADLOCK (KHÓA CHẾT): T1 đang giữ khóa Độc giả và chờ Sách, trong khi T2 đang giữ khóa Sách và chờ Độc giả. Cả hai tạo thành chu trình phụ thuộc vòng (Circular Wait) không bao giờ tự thoát được.";
            result.DbmsBehavior = "Tiến trình giám sát Deadlock Monitor của SQL Server (chạy định kỳ 5s) đã phát hiện chu trình, tự động chọn 1 giao tác có chi phí thấp hơn làm Deadlock Victim (Mã lỗi 1205) và Rollback để cứu giao tác còn lại.";
        }
        else
        {
            result.Explanation = "ĐÃ KHẮC PHỤC BẰNG RESOURCE ORDERING: Tất cả các giao tác bắt buộc phải tuân theo quy tắc thứ tự khóa thống nhất (Luôn khóa Độc giả trước, Sách sau). T2 muốn thao tác phải đợi T1 xong, hoàn toàn không thể tạo thành chu trình Deadlock.";
            result.DbmsBehavior = "Triệt tiêu điều kiện 'Chờ vòng tròn' (Circular Wait) trong 4 điều kiện Coffman gây Deadlock.";
        }
    }
}

