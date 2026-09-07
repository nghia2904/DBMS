namespace QuanLyThuVien.Models;

public class ConcurrencyStepLog
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TransactionName { get; set; } = string.Empty; // T1 or T2
    public string StepName { get; set; } = string.Empty;
    public string SqlExecuted { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Status { get; set; } = "Success"; // Success, Warning, Error, Deadlock
}

public class ConcurrencyResultViewModel
{
    public string ProblemType { get; set; } = string.Empty; // LostUpdate, DirtyRead, UnrepeatableRead, PhantomRead, Deadlock
    public string Mode { get; set; } = "Loi"; // Loi or KhacPhuc
    public int DelaySeconds { get; set; } = 5;
    public string IsolationLevelUsed { get; set; } = string.Empty;
    public string LockingHintUsed { get; set; } = string.Empty;
    
    public string? T1Result { get; set; }
    public string? T2Result { get; set; }
    public bool IsDeadlockVictim { get; set; }
    public int? SqlErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    
    public List<ConcurrencyStepLog> Logs { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
    public string DbmsBehavior { get; set; } = string.Empty;
}

public class DbmsLabSession
{
    public Guid SessionId { get; set; }
    public string TransactionName { get; set; } = string.Empty;
    public string ProblemType { get; set; } = string.Empty;
    public string Mode { get; set; } = "Loi";
    public int MaSach { get; set; }
    public int MaTheLoai { get; set; }
    public int DelaySeconds { get; set; }
    public decimal? InitialPrice { get; set; }
}

public class DbmsLabStepRequest
{
    public Guid SessionId { get; set; }
    public string Operation { get; set; } = string.Empty;
}

public class DbmsLabStepResponse
{
    public string Status { get; set; } = "Success";
    public string Message { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsActive { get; set; }
}

public class ThongKeDayDuViewModel
{
    public int TongSach { get; set; }
    public int TongDocGia { get; set; }
    public int TongNhanVien { get; set; }
    public int TongPhieuMuon { get; set; }
    public int DangMuon { get; set; }
    public int QuaHan { get; set; }
    public decimal TongTienPhat { get; set; }
}

public class ThongKeTheLoaiViewModel
{
    public int MaTheLoai { get; set; }
    public string TenTheLoai { get; set; } = string.Empty;
    public int TongSoDauSach { get; set; }
    public int TongSoLuongSach { get; set; }
    public int SoDauSachCon { get; set; }
    public int SoDauSachHet { get; set; }
    public int SoDauSachNgung { get; set; }
    public decimal GiaTriKho { get; set; }
    public int TongLuotMuon { get; set; }
}

public class TopDocGiaViewModel
{
    public int MaDocGia { get; set; }
    public string TenDocGia { get; set; } = string.Empty;
    public string SDT { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int SoPhieuMuon { get; set; }
    public int TongLuotMuon { get; set; }
    public DateTime? LanMuonGanNhat { get; set; }
}
