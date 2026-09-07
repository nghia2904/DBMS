-- =========================================================================================
-- ĐỒ ÁN HỆ QUẢN TRỊ CSDL: PHÒNG THÍ NGHIỆM ĐIỀU KHIỂN TƯƠNG TRANH & LỊCH GIAO TÁC
-- Database: QuanLyThuVien
-- Mô tả: Chứa đầy đủ các cặp Stored Procedures mô phỏng 4 vấn đề tương tranh và Deadlock
--        kèm cơ chế tham số @Delay (giây) phục vụ demo trực quan từ ứng dụng.
-- =========================================================================================

USE [QuanLyThuVien];
GO

-- =========================================================================================
-- 0. DỮ LIỆU MẪU DEMO VÀ RESET VỀ BAN ĐẦU
-- Mục tiêu: mỗi lần mở lab đều có thể khôi phục dữ liệu mẫu về trạng thái gốc.
-- =========================================================================================
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
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ResetDemoLab
AS
BEGIN
    SET NOCOUNT ON;

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
    END;

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
        ON s.MaSach = b.MaSach;

    DELETE s
    FROM dbo.SACH s
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.DemoBaseline_SACH b WHERE b.MaSach = s.MaSach
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
        SELECT 1 FROM dbo.SACH s WHERE s.MaSach = b.MaSach
    );
END;
GO

-- =========================================================================================
-- 1. VẤN ĐỀ 1: LOST UPDATE (MẤT CẬP NHẬT)
-- Tình huống: 2 Thủ thư / Độc giả cùng mượn 1 cuốn sách khi số lượng tồn kho chỉ còn 1.
-- =========================================================================================

-- 1.1. CHƯA XỬ LÝ (Bị lỗi Lost Update - Cả 2 cùng đọc số lượng = 1 và cùng trừ về 0, gây âm/sai tồn kho)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_LostUpdate_T1_Loi
    @MaSach INT,
    @DelaySec INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @SoLuongHienTai INT;
        SELECT @SoLuongHienTai = SoLuong FROM dbo.SACH WHERE MaSach = @MaSach;
        
        -- Mô phỏng độ trễ xử lý nghiệp vụ
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        IF @SoLuongHienTai > 0
        BEGIN
            UPDATE dbo.SACH 
            SET SoLuong = @SoLuongHienTai - 1 
            WHERE MaSach = @MaSach;
        END

        COMMIT TRANSACTION;
        SELECT @MaSach AS MaSach, (@SoLuongHienTai - 1) AS SoLuongMoi, N'T1: Mượn thành công (Đọc ' + CAST(@SoLuongHienTai AS NVARCHAR(10)) + N', ghi ' + CAST((@SoLuongHienTai - 1) AS NVARCHAR(10)) + N')' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Demo_LostUpdate_T2_Loi
    @MaSach INT
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @SoLuongHienTai INT;
        SELECT @SoLuongHienTai = SoLuong FROM dbo.SACH WHERE MaSach = @MaSach;
        
        IF @SoLuongHienTai > 0
        BEGIN
            UPDATE dbo.SACH 
            SET SoLuong = @SoLuongHienTai - 1 
            WHERE MaSach = @MaSach;
        END

        COMMIT TRANSACTION;
        SELECT @MaSach AS MaSach, (@SoLuongHienTai - 1) AS SoLuongMoi, N'T2: Mượn thành công (Đọc ' + CAST(@SoLuongHienTai AS NVARCHAR(10)) + N', ghi ' + CAST((@SoLuongHienTai - 1) AS NVARCHAR(10)) + N')' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 1.2. ĐÃ KHẮC PHỤC (Sử dụng UPDLOCK, HOLDLOCK để chặn T2 đọc/sửa khi T1 đang giữ khóa)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_LostUpdate_T1_KhacPhuc
    @MaSach INT,
    @DelaySec INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @SoLuongHienTai INT;
        -- Sử dụng UPDLOCK, HOLDLOCK để giữ khóa cập nhật đến hết giao tác
        SELECT @SoLuongHienTai = SoLuong 
        FROM dbo.SACH WITH (UPDLOCK, HOLDLOCK) 
        WHERE MaSach = @MaSach;
        
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        IF @SoLuongHienTai > 0
        BEGIN
            UPDATE dbo.SACH 
            SET SoLuong = SoLuong - 1 
            WHERE MaSach = @MaSach;
            
            COMMIT TRANSACTION;
            SELECT @MaSach AS MaSach, (@SoLuongHienTai - 1) AS SoLuongMoi, N'T1: Mượn thành công có khóa UPDLOCK (Còn lại: ' + CAST((@SoLuongHienTai - 1) AS NVARCHAR(10)) + N')' AS ThongBao;
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT @MaSach AS MaSach, @SoLuongHienTai AS SoLuongMoi, N'T1: Hết sách trong kho!' AS ThongBao;
        END
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Demo_LostUpdate_T2_KhacPhuc
    @MaSach INT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @SoLuongHienTai INT;
        -- T2 phải chờ T1 nhả khóa mới được đọc
        SELECT @SoLuongHienTai = SoLuong 
        FROM dbo.SACH WITH (UPDLOCK, HOLDLOCK) 
        WHERE MaSach = @MaSach;
        
        IF @SoLuongHienTai > 0
        BEGIN
            UPDATE dbo.SACH 
            SET SoLuong = SoLuong - 1 
            WHERE MaSach = @MaSach;
            
            COMMIT TRANSACTION;
            SELECT @MaSach AS MaSach, (@SoLuongHienTai - 1) AS SoLuongMoi, N'T2: Mượn thành công (Còn lại: ' + CAST((@SoLuongHienTai - 1) AS NVARCHAR(10)) + N')' AS ThongBao;
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT @MaSach AS MaSach, @SoLuongHienTai AS SoLuongMoi, N'T2: Không thể mượn! Sách vừa hết tồn kho.' AS ThongBao;
        END
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO


-- =========================================================================================
-- 2. VẤN ĐỀ 2: DIRTY READ (ĐỌC RÁC / UNCOMMITTED DATA)
-- Tình huống: T1 cập nhật Giá tiền sách từ 100k -> 200k, chưa commit, sau đó Rollback.
--             T2 chen ngang đọc dữ liệu rác 200k.
-- =========================================================================================

-- 2.1. CHƯA XỬ LÝ (T2 dùng READ UNCOMMITTED đọc phải dữ liệu rác)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_DirtyRead_T1_Loi
    @MaSach INT,
    @GiaMoi DECIMAL(18,0) = 250000,
    @DelaySec INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @GiaCu DECIMAL(18,0);
        SELECT @GiaCu = GiaTien FROM dbo.SACH WHERE MaSach = @MaSach;
        
        -- Bước 1: Cập nhật giá mới tạm thời
        UPDATE dbo.SACH SET GiaTien = @GiaMoi WHERE MaSach = @MaSach;
        
        -- Bước 2: Chờ để T2 đọc dữ liệu rác
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        -- Bước 3: Hủy giao tác (Rollback)
        ROLLBACK TRANSACTION;
        
        SELECT @MaSach AS MaSach, @GiaCu AS GiaHienTai, N'T1: Đã Rollback giao tác. Giá sách trở về ' + CAST(@GiaCu AS NVARCHAR(20)) + N' đ' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Demo_DirtyRead_T2_Loi
    @MaSach INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Mức cô lập cho phép đọc dữ liệu bẩn (Dirty Read)
    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
    BEGIN TRY
        DECLARE @GiaDocDuoc DECIMAL(18,0), @TenSach NVARCHAR(200);
        SELECT @TenSach = TenSach, @GiaDocDuoc = GiaTien FROM dbo.SACH WHERE MaSach = @MaSach;
        
        SELECT @MaSach AS MaSach, @TenSach AS TenSach, @GiaDocDuoc AS GiaDocDuoc, 
               N'T2: Đọc bẩn (Dirty Read) thấy giá: ' + CAST(@GiaDocDuoc AS NVARCHAR(20)) + N' đ (Dữ liệu chưa Commit!)' AS ThongBao;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;
GO

-- 2.2. ĐÃ KHẮC PHỤC (Dùng READ COMMITTED - Mặc định, T2 bị block chờ T1 Commit/Rollback xong mới đọc)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_DirtyRead_T2_KhacPhuc
    @MaSach INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Mức cô lập chống đọc bẩn: READ COMMITTED
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    BEGIN TRY
        DECLARE @GiaDocDuoc DECIMAL(18,0), @TenSach NVARCHAR(200);
        SELECT @TenSach = TenSach, @GiaDocDuoc = GiaTien FROM dbo.SACH WHERE MaSach = @MaSach;
        
        SELECT @MaSach AS MaSach, @TenSach AS TenSach, @GiaDocDuoc AS GiaDocDuoc, 
               N'T2: Đọc an toàn (READ COMMITTED) sau khi T1 kết thúc. Giá thực tế: ' + CAST(@GiaDocDuoc AS NVARCHAR(20)) + N' đ' AS ThongBao;
    END TRY
    BEGIN CATCH
        THROW;
    END CATCH
END;
GO


-- =========================================================================================
-- 3. VẤN ĐỀ 3: UNREPEATABLE READ (ĐỌC KHÔNG LẶP LẠI)
-- Tình huống: T1 đọc thông tin sách lần 1. Trong khi T1 đang làm việc, T2 chen ngang sửa giá
--             và Commit. T1 đọc lại lần 2 thấy giá trị bị thay đổi trên CÙNG 1 DÒNG.
-- =========================================================================================

-- 3.1. CHƯA XỬ LÝ (READ COMMITTED không giữ Shared Lock sau khi đọc -> T2 sửa được giữa chừng)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_UnrepeatableRead_T1_Loi
    @MaSach INT,
    @DelaySec INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @GiaLan1 DECIMAL(18,0), @GiaLan2 DECIMAL(18,0);
        
        -- Lần đọc 1
        SELECT @GiaLan1 = GiaTien FROM dbo.SACH WHERE MaSach = @MaSach;
        
        -- T1 tạm dừng xử lý
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        -- Lần đọc 2 (Trong cùng 1 giao tác T1)
        SELECT @GiaLan2 = GiaTien FROM dbo.SACH WHERE MaSach = @MaSach;
        
        COMMIT TRANSACTION;
        
        SELECT @MaSach AS MaSach, @GiaLan1 AS GiaLan1, @GiaLan2 AS GiaLan2,
               CASE WHEN @GiaLan1 <> @GiaLan2 
                    THEN N'T1: BỊ LỖI UNREPEATABLE READ! Lần 1 = ' + CAST(@GiaLan1 AS NVARCHAR(20)) + N', Lần 2 = ' + CAST(@GiaLan2 AS NVARCHAR(20))
                    ELSE N'T1: Đọc lặp lại thành công (Không bị đổi dữ liệu)' END AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Demo_UnrepeatableRead_T2
    @MaSach INT,
    @GiaThayDoi DECIMAL(18,0) = 185000
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        UPDATE dbo.SACH SET GiaTien = @GiaThayDoi WHERE MaSach = @MaSach;
        COMMIT TRANSACTION;
        
        SELECT @MaSach AS MaSach, @GiaThayDoi AS GiaMoi, N'T2: Đã cập nhật giá sách thành công lên ' + CAST(@GiaThayDoi AS NVARCHAR(20)) + N' đ' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 3.2. ĐÃ KHẮC PHỤC (Sử dụng REPEATABLE READ - Giữ Shared Lock đến hết giao tác T1)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_UnrepeatableRead_T1_KhacPhuc
    @MaSach INT,
    @DelaySec INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @GiaLan1 DECIMAL(18,0), @GiaLan2 DECIMAL(18,0);
        
        -- Lần đọc 1 (Khóa Shared được giữ nguyên đến khi COMMIT)
        SELECT @GiaLan1 = GiaTien FROM dbo.SACH WHERE MaSach = @MaSach;
        
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        -- Lần đọc 2
        SELECT @GiaLan2 = GiaTien FROM dbo.SACH WHERE MaSach = @MaSach;
        
        COMMIT TRANSACTION;
        
        SELECT @MaSach AS MaSach, @GiaLan1 AS GiaLan1, @GiaLan2 AS GiaLan2,
               N'T1: ĐÃ KHẮC PHỤC (REPEATABLE READ)! Lần 1 = ' + CAST(@GiaLan1 AS NVARCHAR(20)) + N' đ, Lần 2 = ' + CAST(@GiaLan2 AS NVARCHAR(20)) + N' đ (Nhất quán 100%)' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO


-- =========================================================================================
-- 4. VẤN ĐỀ 4: PHANTOM READ (BÓNG MA - DEMO BẰNG CON TRỎ CURSOR)
-- Tình huống: T1 mở con trỏ Cursor duyệt danh sách sách của Thể loại X và đếm số lượng.
--             T2 chen ngang INSERT thêm 1 cuốn sách mới vào Thể loại X và Commit.
--             T1 duyệt lại thấy xuất hiện thêm bản ghi bóng ma (số lượng dòng thay đổi).
-- =========================================================================================

-- 4.1. CHƯA XỬ LÝ (REPEATABLE READ chỉ khóa dòng hiện có, không ngăn được INSERT dòng mới)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_PhantomRead_T1_Loi
    @MaTheLoai INT = 1,
    @DelaySec INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL REPEATABLE READ;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @DemLan1 INT = 0, @DemLan2 INT = 0;
        DECLARE @MaSachCursor INT;

        -- Duyệt bằng CURSOR lần 1
        DECLARE cur1 CURSOR LOCAL FAST_FORWARD FOR 
        SELECT MaSach FROM dbo.SACH WHERE MaTheLoai = @MaTheLoai;
        
        OPEN cur1;
        FETCH NEXT FROM cur1 INTO @MaSachCursor;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @DemLan1 = @DemLan1 + 1;
            FETCH NEXT FROM cur1 INTO @MaSachCursor;
        END
        CLOSE cur1;
        DEALLOCATE cur1;

        -- T1 chờ (trong thời gian này T2 sẽ INSERT sách mới)
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;

        -- Duyệt bằng CURSOR lần 2 (trong cùng 1 giao tác)
        DECLARE cur2 CURSOR LOCAL FAST_FORWARD FOR 
        SELECT MaSach FROM dbo.SACH WHERE MaTheLoai = @MaTheLoai;
        
        OPEN cur2;
        FETCH NEXT FROM cur2 INTO @MaSachCursor;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @DemLan2 = @DemLan2 + 1;
            FETCH NEXT FROM cur2 INTO @MaSachCursor;
        END
        CLOSE cur2;
        DEALLOCATE cur2;

        COMMIT TRANSACTION;

        SELECT @MaTheLoai AS MaTheLoai, @DemLan1 AS SoLuongDauSachLan1, @DemLan2 AS SoLuongDauSachLan2,
               CASE WHEN @DemLan2 > @DemLan1 
                    THEN N'T1: XUẤT HIỆN BÓNG MA (PHANTOM READ)! Cursor lần 1: ' + CAST(@DemLan1 AS NVARCHAR(10)) + N' sách, Cursor lần 2: ' + CAST(@DemLan2 AS NVARCHAR(10)) + N' sách.'
                    ELSE N'T1: Không có bóng ma.' END AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Demo_PhantomRead_T2
    @MaTheLoai INT = 1,
    @TenSachMoi NVARCHAR(200) = N'Sách Demo Phantom Read (Mới)'
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @ISBN VARCHAR(20) = 'PHANTOM-' + CAST(ABS(CHECKSUM(NEWID())) % 100000 AS VARCHAR(10));
        DECLARE @MaTacGia INT, @MaNXB INT;
        SELECT TOP 1 @MaTacGia = MaTacGia FROM dbo.TACGIA;
        SELECT TOP 1 @MaNXB = MaNXB FROM dbo.NHAXUATBAN;

        INSERT INTO dbo.SACH (TenSach, ISBN, NamXuatBan, SoLuong, GiaTien, ViTriKe, TrangThai, MaTheLoai, MaTacGia, MaNXB)
        VALUES (@TenSachMoi, @ISBN, YEAR(GETDATE()), 5, 120000, N'Kệ A1', N'Còn sách', @MaTheLoai, @MaTacGia, @MaNXB);

        COMMIT TRANSACTION;
        SELECT @TenSachMoi AS TenSach, @ISBN AS ISBN, N'T2: Đã INSERT thành công 1 sách mới vào thể loại ' + CAST(@MaTheLoai AS NVARCHAR(10)) AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 4.2. ĐÃ KHẮC PHỤC (Sử dụng SERIALIZABLE - Khóa Range Lock ngăn chặn INSERT bóng ma)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_PhantomRead_T1_KhacPhuc
    @MaTheLoai INT = 1,
    @DelaySec INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        DECLARE @DemLan1 INT = 0, @DemLan2 INT = 0;
        DECLARE @MaSachCursor INT;

        -- Lần 1: Khóa Range Lock toàn bộ phạm vi MaTheLoai = @MaTheLoai
        DECLARE cur1 CURSOR LOCAL FAST_FORWARD FOR 
        SELECT MaSach FROM dbo.SACH WHERE MaTheLoai = @MaTheLoai;
        
        OPEN cur1;
        FETCH NEXT FROM cur1 INTO @MaSachCursor;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @DemLan1 = @DemLan1 + 1;
            FETCH NEXT FROM cur1 INTO @MaSachCursor;
        END
        CLOSE cur1;
        DEALLOCATE cur1;

        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;

        -- Lần 2
        DECLARE cur2 CURSOR LOCAL FAST_FORWARD FOR 
        SELECT MaSach FROM dbo.SACH WHERE MaTheLoai = @MaTheLoai;
        
        OPEN cur2;
        FETCH NEXT FROM cur2 INTO @MaSachCursor;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @DemLan2 = @DemLan2 + 1;
            FETCH NEXT FROM cur2 INTO @MaSachCursor;
        END
        CLOSE cur2;
        DEALLOCATE cur2;

        COMMIT TRANSACTION;

        SELECT @MaTheLoai AS MaTheLoai, @DemLan1 AS SoLuongDauSachLan1, @DemLan2 AS SoLuongDauSachLan2,
               N'T1: ĐÃ KHẮC PHỤC BÓNG MA (SERIALIZABLE)! Cả 2 lần đều đếm được ' + CAST(@DemLan1 AS NVARCHAR(10)) + N' sách (T2 bị chặn đến khi T1 commit).' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO


-- =========================================================================================
-- 5. DEADLOCK (KHÓA CHẾT) & GIẢI PHÁP RESOURCE ORDERING
-- Tình huống: T1 khóa Độc giả 1 rồi chờ Sách 2.
--             T2 khóa Sách 2 rồi chờ Độc giả 1.
--             -> SQL Server bắt chu trình Deadlock và hủy 1 giao tác (Error 1205).
-- =========================================================================================

-- 5.1. CHƯA XỬ LÝ (Thứ tự khóa chéo nhau gây Deadlock)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_Deadlock_T1_Loi
    @MaDocGia INT = 1,
    @MaSach INT = 1,
    @DelaySec INT = 3
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Bước 1: T1 khóa độc giả
        UPDATE dbo.DOCGIA WITH (XLOCK)
        SET DiaChi = DiaChi
        WHERE MaDocGia = @MaDocGia;
        
        -- T1 chờ T2 khóa tài nguyên Sách
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        -- Bước 2: T1 yêu cầu khóa Sách (đang bị T2 giữ)
        UPDATE dbo.SACH WITH (XLOCK)
        SET ViTriKe = ViTriKe
        WHERE MaSach = @MaSach;
        
        COMMIT TRANSACTION;
        SELECT N'T1: Thực thi thành công (Không bị Deadlock)' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Demo_Deadlock_T2_Loi
    @MaDocGia INT = 1,
    @MaSach INT = 1,
    @DelaySec INT = 3
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Bước 1: T2 khóa Sách trước
        UPDATE dbo.SACH WITH (XLOCK)
        SET ViTriKe = ViTriKe
        WHERE MaSach = @MaSach;
        
        -- T2 chờ T1 khóa tài nguyên Độc giả
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        -- Bước 2: T2 yêu cầu khóa Độc giả (đang bị T1 giữ) -> GÂY DEADLOCK!
        UPDATE dbo.DOCGIA WITH (XLOCK)
        SET DiaChi = DiaChi
        WHERE MaDocGia = @MaDocGia;
        
        COMMIT TRANSACTION;
        SELECT N'T2: Thực thi thành công (Không bị Deadlock)' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 5.2. ĐÃ KHẮC PHỤC (Quy định thứ tự tài nguyên đồng nhất: Luôn khóa ĐỘC GIẢ trước, SÁCH sau)
CREATE OR ALTER PROCEDURE dbo.sp_Demo_Deadlock_T1_KhacPhuc
    @MaDocGia INT = 1,
    @MaSach INT = 1,
    @DelaySec INT = 3
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Quy tắc chuẩn: Khóa Độc giả trước
        UPDATE dbo.DOCGIA WITH (XLOCK) SET DiaChi = DiaChi WHERE MaDocGia = @MaDocGia;
        
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        -- Sau đó khóa Sách
        UPDATE dbo.SACH WITH (XLOCK) SET ViTriKe = ViTriKe WHERE MaSach = @MaSach;
        
        COMMIT TRANSACTION;
        SELECT N'T1 (Đã khắc phục): Hoàn tất theo thứ tự tài nguyên chuẩn (Độc giả -> Sách)' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Demo_Deadlock_T2_KhacPhuc
    @MaDocGia INT = 1,
    @MaSach INT = 1,
    @DelaySec INT = 3
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Quy tắc chuẩn: T2 cũng phải khóa Độc giả trước (nếu T1 đang giữ thì T2 sẽ chờ tuần tự, không bao giờ bị Deadlock)
        UPDATE dbo.DOCGIA WITH (XLOCK) SET DiaChi = DiaChi WHERE MaDocGia = @MaDocGia;
        
        DECLARE @DelayStr VARCHAR(10) = '00:00:' + RIGHT('0' + CAST(@DelaySec AS VARCHAR(2)), 2);
        WAITFOR DELAY @DelayStr;
        
        UPDATE dbo.SACH WITH (XLOCK) SET ViTriKe = ViTriKe WHERE MaSach = @MaSach;
        
        COMMIT TRANSACTION;
        SELECT N'T2 (Đã khắc phục): Hoàn tất tuần tự an toàn (Chờ T1 nhả khóa và thực hiện thành công)' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

PRINT N'=== ĐÃ TẠO THÀNH CÔNG TOÀN BỘ CÁC STORED PROCEDURES DEMO TƯƠNG TRANH & DEADLOCK ===';

