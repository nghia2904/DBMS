namespace QuanLyThuVien.Models;

public class DocGia
{
    public int MaDocGia { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public DateTime? NgaySinh { get; set; }
    public string? GioiTinh { get; set; }
    public string? DiaChi { get; set; }
    public string SDT { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime NgayDangKy { get; set; }
    public string TrangThai { get; set; } = "Hoạt động";
}

public class NhanVien
{
    public int MaNV { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string SDT { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string ChucVu { get; set; } = "Thủ thư";
    public string TrangThai { get; set; } = "Hoạt động";
}

public class TheLoai
{
    public int MaTheLoai { get; set; }
    public string TenTheLoai { get; set; } = string.Empty;
    public string? MoTa { get; set; }
    public int SoNgayMuonToiDa { get; set; } = 14;
}

public class TacGia
{
    public int MaTacGia { get; set; }
    public string TenTacGia { get; set; } = string.Empty;
    public DateTime? NgaySinh { get; set; }
    public string? QuocTich { get; set; }
}

public class NhaXuatBan
{
    public int MaNXB { get; set; }
    public string TenNXB { get; set; } = string.Empty;
    public string? DiaChi { get; set; }
    public string? SoDienThoai { get; set; }
}

