/* =====================================================================================
   MealRegistration: suất ăn theo từng nhân viên (tick từ chấm công, bỏ tick trước giờ chốt).
   TeamMealConfirmation.MealCount vẫn là số tổng theo tổ (cộng từ WillEat = 1).
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-10_meal_registration.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF OBJECT_ID(N'dbo.MealRegistration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealRegistration (
        MealRegistrationId  INT IDENTITY(1,1) CONSTRAINT PK_MealRegistration PRIMARY KEY,
        DailyMealId         INT           NOT NULL
            CONSTRAINT FK_MR_DailyMeal REFERENCES dbo.DailyMeal(DailyMealId),
        EmployeeId          INT           NOT NULL
            CONSTRAINT FK_MR_Employee REFERENCES dbo.Employee(EmployeeId),
        TeamId              INT           NULL
            CONSTRAINT FK_MR_Team REFERENCES dbo.Team(TeamId),
        WillEat             BIT           NOT NULL CONSTRAINT DF_MR_WillEat DEFAULT 1,
        UpdatedByEmployeeId INT           NULL
            CONSTRAINT FK_MR_UpdatedBy REFERENCES dbo.Employee(EmployeeId),
        UpdatedAt           DATETIME2(0)  NULL,
        CONSTRAINT UQ_MR UNIQUE (DailyMealId, EmployeeId)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_MR_DailyMeal_Team' AND object_id = OBJECT_ID(N'dbo.MealRegistration'))
    CREATE INDEX IX_MR_DailyMeal_Team ON dbo.MealRegistration(DailyMealId, TeamId)
        INCLUDE (WillEat, EmployeeId);
GO

IF OBJECT_ID(N'dbo.trg_MealRegistration_Cutoff', N'TR') IS NULL
    EXEC(N'
CREATE TRIGGER dbo.trg_MealRegistration_Cutoff ON dbo.MealRegistration AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.DailyMeal m ON m.DailyMealId = i.DailyMealId WHERE m.Status <> ''OPEN'')
       OR EXISTS (SELECT 1 FROM deleted d JOIN dbo.DailyMeal m ON m.DailyMealId = d.DailyMealId WHERE m.Status <> ''OPEN'')
        THROW 50020, N''Đã chốt suất ăn lúc 08:30, không thể thay đổi.'', 1;
END');
GO

PRINT N'Đã thêm bảng MealRegistration + trigger khóa sau giờ chốt.';
GO
