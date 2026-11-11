/* =====================================================================================
   UC51 · Claude (AI) · 11/11/2026 · DEV-ALL-90
   Bàn cắt không nhập số lớp, tỉ lệ size, chiều dài bàn cắt nữa: tổ cắt nhập thẳng số lượng sau cắt từng màu - size
   (CuttingLayLine.ActualQty) và một số mét vải đã dùng của cả bàn (CuttingLay.FabricUsedM, mới).
     - CuttingLay: thêm FabricUsedM DECIMAL(9,2) NULL (> 0), điền = PlyCount × LayLengthM cho bàn đã có chiều dài;
       bàn cũ chưa có chiều dài để NULL (màn hình báo "chưa có số mét"). Bỏ PlyCount, LayLengthM.
     - CuttingLayLine: bỏ RatioValue, CalculatedQty, AdjustReason (cùng CK_CLL_Ratio, CK_CLL_Calc, CK_CLL_Reason).
   Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_cutting_lay_fabric_used.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.CuttingLay', 'FabricUsedM') IS NULL
    ALTER TABLE dbo.CuttingLay ADD FabricUsedM DECIMAL(9,2) NULL;

IF COL_LENGTH('dbo.CuttingLay', 'LayLengthM') IS NOT NULL
    EXEC (N'UPDATE dbo.CuttingLay SET FabricUsedM = PlyCount * LayLengthM
            WHERE FabricUsedM IS NULL AND LayLengthM IS NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CL_FabricUsed')
    EXEC (N'ALTER TABLE dbo.CuttingLay ADD CONSTRAINT CK_CL_FabricUsed CHECK (FabricUsedM > 0);');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CL_Ply')
    ALTER TABLE dbo.CuttingLay DROP CONSTRAINT CK_CL_Ply;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CL_Length')
    ALTER TABLE dbo.CuttingLay DROP CONSTRAINT CK_CL_Length;
IF COL_LENGTH('dbo.CuttingLay', 'PlyCount') IS NOT NULL
    ALTER TABLE dbo.CuttingLay DROP COLUMN PlyCount;
IF COL_LENGTH('dbo.CuttingLay', 'LayLengthM') IS NOT NULL
    ALTER TABLE dbo.CuttingLay DROP COLUMN LayLengthM;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CLL_Reason')
    ALTER TABLE dbo.CuttingLayLine DROP CONSTRAINT CK_CLL_Reason;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CLL_Ratio')
    ALTER TABLE dbo.CuttingLayLine DROP CONSTRAINT CK_CLL_Ratio;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CLL_Calc')
    ALTER TABLE dbo.CuttingLayLine DROP CONSTRAINT CK_CLL_Calc;
IF COL_LENGTH('dbo.CuttingLayLine', 'RatioValue') IS NOT NULL
    ALTER TABLE dbo.CuttingLayLine DROP COLUMN RatioValue;
IF COL_LENGTH('dbo.CuttingLayLine', 'CalculatedQty') IS NOT NULL
    ALTER TABLE dbo.CuttingLayLine DROP COLUMN CalculatedQty;
IF COL_LENGTH('dbo.CuttingLayLine', 'AdjustReason') IS NOT NULL
    ALTER TABLE dbo.CuttingLayLine DROP COLUMN AdjustReason;

COMMIT;
GO

DECLARE @unmeasured INT = (SELECT COUNT(*) FROM dbo.CuttingLay WHERE FabricUsedM IS NULL);
PRINT N'Bàn cắt: đã chuyển sang số lượng sau cắt + số mét vải đã dùng. Bàn chưa có số mét (bàn cũ): '
    + CAST(@unmeasured AS NVARCHAR(10)) + N'.';
GO
