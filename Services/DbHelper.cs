using System.Data;
using Microsoft.Data.SqlClient;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Services;

public class DbHelper
{
    private readonly string _connectionString;

    public DbHelper(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=.\\SQLEXPRESS;Database=QuanLyThuVien;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    }

    public SqlConnection GetConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<DataTable> ExecuteQueryAsync(string sql, SqlParameter[]? parameters = null)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        if (parameters != null)
        {
            cmd.Parameters.AddRange(parameters);
        }
        using var adapter = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        adapter.Fill(dt);
        return dt;
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, SqlParameter[]? parameters = null)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn);
        if (parameters != null)
        {
            cmd.Parameters.AddRange(parameters);
        }
        return await cmd.ExecuteNonQueryAsync();
    }

    public static SqlParameter P(string name, object? value)
    {
        if (value is string s && string.IsNullOrWhiteSpace(s))
            return new SqlParameter(name, DBNull.Value);
        return new SqlParameter(name, value ?? DBNull.Value);
    }

    public async Task<DataTable> ExecuteStoredProcedureAsync(string spName, SqlParameter[]? parameters = null)
    {
        var ds = await ExecuteStoredProcedureDataSetAsync(spName, parameters);
        return ds.Tables.Count > 0 ? ds.Tables[0] : new DataTable();
    }

    public async Task<DataSet> ExecuteStoredProcedureDataSetAsync(string spName, SqlParameter[]? parameters = null)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(spName, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        if (parameters != null)
        {
            cmd.Parameters.AddRange(parameters);
        }
        using var adapter = new SqlDataAdapter(cmd);
        var ds = new DataSet();
        adapter.Fill(ds);
        return ds;
    }

    public async Task<int> ExecuteNonQueryStoredProcedureAsync(string spName, SqlParameter[]? parameters = null)
    {
        using var conn = GetConnection();
        await conn.OpenAsync();
        using var cmd = new SqlCommand(spName, conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        if (parameters != null)
        {
            cmd.Parameters.AddRange(parameters);
        }
        return await cmd.ExecuteNonQueryAsync();
    }

    public async Task<TaiKhoan?> CheckLoginAsync(string username, string password)
    {
        try
        {
            var parameters = new[]
            {
                new SqlParameter("@Username", username),
                new SqlParameter("@Password", password)
            };
            var dt = await ExecuteStoredProcedureAsync("usp_DangNhap", parameters);
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                return new TaiKhoan
                {
                    MaTK = Convert.ToInt32(row["MaTK"]),
                    Username = row["Username"].ToString() ?? "",
                    Role = Convert.ToByte(row["Role"]),
                    TenRole = row["TenRole"].ToString(),
                    MaDocGia = row["MaDocGia"] != DBNull.Value ? Convert.ToInt32(row["MaDocGia"]) : null,
                    MaNV = row["MaNV"] != DBNull.Value ? Convert.ToInt32(row["MaNV"]) : null,
                    HoTen = row["HoTen"].ToString()
                };
            }
        }
        catch (Exception)
        {
            return null;
        }
        return null;
    }
}
