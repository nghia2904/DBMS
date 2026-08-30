namespace QuanLyThuVien.Models;

public class PhieuMuon
{
    public int MaPM { get; set; }
    public int MaDocGia { get; set; }
    public string? TenDocGia { get; set; }
    public int MaNV { get; set; }
    public string? TenNhanVien { get; set; }
    public DateTime NgayMuon { get; set; }
    public string TrangThai { get; set; } = "Đang mượn";
    public List<ChiTietPhieuMuon> ChiTiets { get; set; } = new();
}

public class ChiTietPhieuMuon
{
    public int MaPM { get; set; }
    public int MaSach { get; set; }
    public string? TenSach { get; set; }
    public int SoLuong { get; set; } = 1;
    public DateTime HanTra { get; set; }
    public DateTime? NgayTra { get; set; }
    public decimal TienPhat { get; set; } = 0;
    public byte SoLanGiaHan { get; set; } = 0;
}

public class YeuCauMuon
{
    public int MaYC { get; set; }
    public int MaDocGia { get; set; }
    public string? TenDocGia { get; set; }
    public int MaSach { get; set; }
    public string? TenSach { get; set; }
    public DateTime NgayYeuCau { get; set; }
    public string TrangThai { get; set; } = "Chờ xử lý";
    public int? MaNVXuLy { get; set; }
    public string? TenNVXuLy { get; set; }
    public DateTime? NgayXuLy { get; set; }
    public string? LyDoTuChoi { get; set; }
}

public class TaiKhoan
{
    public int MaTK { get; set; }
    public string Username { get; set; } = string.Empty;
    public byte Role { get; set; } // 1: Độc giả, 2: Thủ thư, 3: Ban QL, 4: Admin
    public string? TenRole { get; set; }
    public int? MaDocGia { get; set; }
    public int? MaNV { get; set; }
    public string? HoTen { get; set; }
    public string TrangThai { get; set; } = "Hoạt động";
}

public class LoginViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public DateTime? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? DiaChi { get; set; }
    public string SDT { get; set; } = string.Empty;
    public string? Email { get; set; }
}

