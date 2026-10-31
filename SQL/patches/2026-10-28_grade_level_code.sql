/* =====================================================================================
   Patch 28/10/2026 - Bậc thợ động (Admin thêm / sửa / ngừng dùng bậc)
   - Tạo dbo.GradeLevelCode (danh mục bậc), seed A-D từ dbo.GradeLevel
   - Nới mã bậc CHAR(1) -> VARCHAR(5) ở GradeLevel, Employee, GradeEvaluation
   - Bỏ 3 CHECK cứng 'A'..'D', thay bằng khóa ngoại tới danh mục
   Chạy lại không lỗi (idempotent). Áp lên DB đã dựng từ script cũ; DB dựng mới từ NX-MTD_Database.sql đã có sẵn.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.GradeLevelCode', 'U') IS NOT NULL
BEGIN
    PRINT N'Patch bậc thợ đã áp dụng, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

CREATE TABLE dbo.GradeLevelCode (
    GradeCode  VARCHAR(5)    NOT NULL CONSTRAINT PK_GradeLevelCode PRIMARY KEY CONSTRAINT CK_GLC_Code CHECK (GradeCode NOT LIKE '%[^A-Z0-9]%'),
    GradeName  NVARCHAR(50)  NOT NULL,
    SortOrder  SMALLINT      NOT NULL CONSTRAINT DF_GLC_Sort DEFAULT 0,
    Status     VARCHAR(10)   NOT NULL CONSTRAINT DF_GLC_Status DEFAULT 'ACTIVE' CONSTRAINT CK_GLC_Status CHECK (Status IN ('ACTIVE','INACTIVE')),
    CreatedAt  DATETIME2(0)  NOT NULL CONSTRAINT DF_GLC_CreatedAt DEFAULT (dbo.fn_VnNow())
);

-- Seed từ các mã đang có (tên lấy từ phiên bản tiền bậc mới nhất)
INSERT dbo.GradeLevelCode (GradeCode, GradeName, SortOrder)
SELECT g.GradeCode, MAX(g.GradeName), ROW_NUMBER() OVER (ORDER BY g.GradeCode)
FROM dbo.GradeLevel g GROUP BY g.GradeCode;
-- Mã đã gán cho nhân viên / đánh giá mà chưa có tiền bậc: vẫn phải có trong danh mục
INSERT dbo.GradeLevelCode (GradeCode, GradeName, SortOrder)
SELECT c.GradeCode, N'Bậc ' + c.GradeCode, 100
FROM (SELECT CurrentGradeCode AS GradeCode FROM dbo.Employee WHERE CurrentGradeCode IS NOT NULL
      UNION SELECT ProposedGradeCode FROM dbo.GradeEvaluation
      UNION SELECT CurrentGradeCode FROM dbo.GradeEvaluation WHERE CurrentGradeCode IS NOT NULL) c
WHERE NOT EXISTS (SELECT 1 FROM dbo.GradeLevelCode x WHERE x.GradeCode = c.GradeCode);

-- Bỏ CHECK cứng, nới kiểu cột, thêm khóa ngoại
ALTER TABLE dbo.GradeLevel DROP CONSTRAINT CK_GradeLevel_Code;
ALTER TABLE dbo.Employee DROP CONSTRAINT CK_Employee_Grade;
ALTER TABLE dbo.GradeEvaluation DROP CONSTRAINT CK_GradeEval_Grades;
ALTER TABLE dbo.GradeLevel DROP CONSTRAINT UQ_GradeLevel;
ALTER TABLE dbo.GradeLevel ALTER COLUMN GradeCode VARCHAR(5) NOT NULL;
ALTER TABLE dbo.GradeLevel ADD CONSTRAINT UQ_GradeLevel UNIQUE (GradeCode, EffectiveDate);
ALTER TABLE dbo.Employee ALTER COLUMN CurrentGradeCode VARCHAR(5) NULL;
ALTER TABLE dbo.GradeEvaluation ALTER COLUMN CurrentGradeCode VARCHAR(5) NULL;
ALTER TABLE dbo.GradeEvaluation ALTER COLUMN ProposedGradeCode VARCHAR(5) NOT NULL;

ALTER TABLE dbo.GradeLevel ADD CONSTRAINT FK_GradeLevel_Code FOREIGN KEY (GradeCode) REFERENCES dbo.GradeLevelCode(GradeCode);
ALTER TABLE dbo.Employee ADD CONSTRAINT FK_Employee_Grade FOREIGN KEY (CurrentGradeCode) REFERENCES dbo.GradeLevelCode(GradeCode);
ALTER TABLE dbo.GradeEvaluation ADD CONSTRAINT FK_GradeEval_Current FOREIGN KEY (CurrentGradeCode) REFERENCES dbo.GradeLevelCode(GradeCode);
ALTER TABLE dbo.GradeEvaluation ADD CONSTRAINT FK_GradeEval_Proposed FOREIGN KEY (ProposedGradeCode) REFERENCES dbo.GradeLevelCode(GradeCode);

COMMIT TRANSACTION;
PRINT N'Đã áp dụng patch bậc thợ động.';
