/* =====================================================================================
   UC42, UC24 · Claude (AI) · 13/11/2026 · DEV-ALL-112
   1. NVL phế phẩm: Material.MaterialType thêm giá trị SCRAP (Category SCRAP_FABRIC, SCRAP_NEEDLE, SCRAP_OTHER); đơn vị tự chọn
      (vải theo kg, kim theo cái…). Phế phẩm chỉ để tổ cắt / tổ may trả về kho, không dùng ở đơn xin, phiếu cấp, NVL của đơn.
   2. Phiếu trả (MaterialReturn / MaterialReturnLine) không còn bắt buộc gắn phiếu cấp:
        MaterialReturn thêm ReturningTeamId (tổ trả), WarehouseType (MATERIAL / SEMI_FINISHED), OrderId, WorkOrderId;
        MaterialIssueId cho NULL (phiếu cũ vẫn giữ).
        MaterialReturnLine thêm MaterialId, OrderLineId (đúng một trong hai); MaterialIssueLineId cho NULL.
      Phiếu cũ được điền ReturningTeamId / OrderId / WorkOrderId từ phiếu cấp và MaterialId từ dòng phiếu cấp.
   Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-13_material_return_scrap.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---- 1. Material: loại SCRAP (cột MaterialType là cột tính nên phải bỏ rồi thêm lại) ---- */
IF NOT EXISTS (SELECT 1 FROM sys.computed_columns
               WHERE object_id = OBJECT_ID(N'dbo.Material') AND name = N'MaterialType' AND definition LIKE N'%SCRAP%')
BEGIN
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Material_NameType' AND parent_object_id = OBJECT_ID(N'dbo.Material'))
        ALTER TABLE dbo.Material DROP CONSTRAINT UQ_Material_NameType;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Material_Category')
        ALTER TABLE dbo.Material DROP CONSTRAINT CK_Material_Category;
    ALTER TABLE dbo.Material DROP COLUMN MaterialType;
    ALTER TABLE dbo.Material ADD MaterialType AS (CAST(CASE
        WHEN Category IN ('MAIN_FABRIC','LINING','INTERLINING','RIB') THEN 'FABRIC'
        WHEN Category IN ('SCRAP_FABRIC','SCRAP_NEEDLE','SCRAP_OTHER') THEN 'SCRAP'
        ELSE 'TRIM' END AS VARCHAR(10))) PERSISTED;
    ALTER TABLE dbo.Material ADD CONSTRAINT CK_Material_Category CHECK (Category IN ('MAIN_FABRIC','LINING','INTERLINING','RIB',
        'THREAD','ZIPPER','BUTTON','SNAP','ELASTIC','TAPE','LABEL','HANG_TAG','PADDING','PACKAGING','OTHER',
        'SCRAP_FABRIC','SCRAP_NEEDLE','SCRAP_OTHER'));
    ALTER TABLE dbo.Material ADD CONSTRAINT UQ_Material_NameType UNIQUE (MaterialName, MaterialType, Color);
    COMMIT TRANSACTION;
END
GO

IF OBJECT_ID(N'dbo.vw_MaterialStock', N'V') IS NOT NULL EXEC sys.sp_refreshview N'dbo.vw_MaterialStock';
GO

/* NVL phế phẩm mẫu */
INSERT dbo.Material (MaterialCode, MaterialName, Color, Category, Unit)
SELECT v.Code, v.Name, N'', v.Category, v.Unit
FROM (VALUES ('PP-0001', N'Vải vụn', 'SCRAP_FABRIC', N'kg'),
             ('PP-0002', N'Kim gãy', 'SCRAP_NEEDLE', N'cái'),
             ('PP-0003', N'Phế phẩm khác', 'SCRAP_OTHER', N'kg')) v(Code, Name, Category, Unit)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Material m WHERE m.MaterialCode = v.Code);
GO

/* ---- 2. MaterialReturn: tổ trả, kho, đơn hàng, lệnh cắt ---- */
IF COL_LENGTH('dbo.MaterialReturn', 'ReturningTeamId') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD ReturningTeamId INT NULL;
IF COL_LENGTH('dbo.MaterialReturn', 'WarehouseType') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD WarehouseType VARCHAR(15) NOT NULL CONSTRAINT DF_MRet_Warehouse DEFAULT 'MATERIAL';
IF COL_LENGTH('dbo.MaterialReturn', 'OrderId') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD OrderId INT NULL;
IF COL_LENGTH('dbo.MaterialReturn', 'WorkOrderId') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD WorkOrderId INT NULL;
GO

UPDATE r
SET r.ReturningTeamId = i.ReceivingTeamId, r.OrderId = i.OrderId, r.WorkOrderId = i.WorkOrderId
FROM dbo.MaterialReturn r
JOIN dbo.MaterialIssue i ON i.MaterialIssueId = r.MaterialIssueId
WHERE r.ReturningTeamId IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MaterialReturn') AND name = N'ReturningTeamId' AND is_nullable = 1)
    ALTER TABLE dbo.MaterialReturn ALTER COLUMN ReturningTeamId INT NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MaterialReturn') AND name = N'MaterialIssueId' AND is_nullable = 0)
    ALTER TABLE dbo.MaterialReturn ALTER COLUMN MaterialIssueId INT NULL;
GO

IF OBJECT_ID(N'dbo.FK_MRet_Team', N'F') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD CONSTRAINT FK_MRet_Team FOREIGN KEY (ReturningTeamId) REFERENCES dbo.Team(TeamId);
IF OBJECT_ID(N'dbo.FK_MRet_Order', N'F') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD CONSTRAINT FK_MRet_Order FOREIGN KEY (OrderId) REFERENCES dbo.CustomerOrder(OrderId);
IF OBJECT_ID(N'dbo.FK_MRet_WorkOrder', N'F') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD CONSTRAINT FK_MRet_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES dbo.WorkOrder(WorkOrderId);
IF OBJECT_ID(N'dbo.CK_MRet_Warehouse', N'C') IS NULL
    ALTER TABLE dbo.MaterialReturn ADD CONSTRAINT CK_MRet_Warehouse CHECK (WarehouseType IN ('MATERIAL','SEMI_FINISHED'));
GO

/* ---- 3. MaterialReturnLine: NVL hoặc dòng đơn (BTP) ---- */
IF COL_LENGTH('dbo.MaterialReturnLine', 'MaterialId') IS NULL
    ALTER TABLE dbo.MaterialReturnLine ADD MaterialId INT NULL;
IF COL_LENGTH('dbo.MaterialReturnLine', 'OrderLineId') IS NULL
    ALTER TABLE dbo.MaterialReturnLine ADD OrderLineId INT NULL;
GO

UPDATE rl
SET rl.MaterialId = il.MaterialId
FROM dbo.MaterialReturnLine rl
JOIN dbo.MaterialIssueLine il ON il.MaterialIssueLineId = rl.MaterialIssueLineId
WHERE rl.MaterialId IS NULL AND rl.OrderLineId IS NULL AND il.MaterialId IS NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MaterialReturnLine') AND name = N'MaterialIssueLineId' AND is_nullable = 0)
    ALTER TABLE dbo.MaterialReturnLine ALTER COLUMN MaterialIssueLineId INT NULL;
GO

IF OBJECT_ID(N'dbo.FK_MRetL_Material', N'F') IS NULL
    ALTER TABLE dbo.MaterialReturnLine ADD CONSTRAINT FK_MRetL_Material FOREIGN KEY (MaterialId) REFERENCES dbo.Material(MaterialId);
IF OBJECT_ID(N'dbo.FK_MRetL_OrderLine', N'F') IS NULL
    ALTER TABLE dbo.MaterialReturnLine ADD CONSTRAINT FK_MRetL_OrderLine FOREIGN KEY (OrderLineId) REFERENCES dbo.OrderLine(OrderLineId);
IF OBJECT_ID(N'dbo.CK_MRetL_Item', N'C') IS NULL
    ALTER TABLE dbo.MaterialReturnLine ADD CONSTRAINT CK_MRetL_Item
        CHECK ((MaterialId IS NOT NULL AND OrderLineId IS NULL) OR (MaterialId IS NULL AND OrderLineId IS NOT NULL));
GO

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_MRetL' AND parent_object_id = OBJECT_ID(N'dbo.MaterialReturnLine'))
    ALTER TABLE dbo.MaterialReturnLine DROP CONSTRAINT UQ_MRetL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_MRetL_Material' AND object_id = OBJECT_ID(N'dbo.MaterialReturnLine'))
    CREATE UNIQUE INDEX UQ_MRetL_Material ON dbo.MaterialReturnLine (MaterialReturnId, MaterialId) WHERE MaterialId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_MRetL_OrderLine' AND object_id = OBJECT_ID(N'dbo.MaterialReturnLine'))
    CREATE UNIQUE INDEX UQ_MRetL_OrderLine ON dbo.MaterialReturnLine (MaterialReturnId, OrderLineId) WHERE OrderLineId IS NOT NULL;
GO

/* Trigger: chỉ kiểm khi phiếu trả và dòng đều gắn phiếu cấp (phiếu cũ). */
CREATE OR ALTER TRIGGER dbo.trg_MaterialReturnLine_Check ON dbo.MaterialReturnLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.MaterialReturn r      ON r.MaterialReturnId = i.MaterialReturnId
               JOIN dbo.MaterialIssueLine l   ON l.MaterialIssueLineId = i.MaterialIssueLineId
               WHERE r.MaterialIssueId IS NOT NULL AND l.MaterialIssueId <> r.MaterialIssueId)
        THROW 50042, N'Dòng phiếu trả phải là dòng của phiếu cấp mà phiếu trả gắn vào.', 1;
END;
GO
