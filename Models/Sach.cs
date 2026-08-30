namespace QuanLyThuVien.Models;

public class Sach
{
    public int MaSach { get; set; }
    public string TenSach { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public int? NamXuatBan { get; set; }
    public int SoLuong { get; set; }
    public decimal? GiaTien { get; set; }
    public string? ViTriKe { get; set; }
    public string TrangThai { get; set; } = "Còn sách";
    
    public int MaTheLoai { get; set; }
    public string? TenTheLoai { get; set; }
    public int? SoNgayMuonToiDa { get; set; }

    public int MaTacGia { get; set; }
    public string? TenTacGia { get; set; }

    public int MaNXB { get; set; }
    public string? TenNXB { get; set; }
}

