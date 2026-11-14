/* =====================================================================================
   DEV-ALL-113: số lần giao NVL = số ngày giao (thay khoảng "từ ngày - đến ngày").
   Thêm bảng DeliveryScheduleDate lưu ngày dự kiến từng lần giao; mỗi dòng thuộc đúng một chủ lịch có khoá ngoại: PurchaseOrderId (đơn mua),
   OrderId (lịch khách giao của đơn hàng) hoặc RequestId (đơn giao bù). Các cột ngày đầu / ngày cuối cũ vẫn giữ làm tóm tắt
   (ExpectedDeliveryDate / ExpectedDeliveryDateTo, CustomerDeliveryDate / CustomerDeliveryDateTo, TargetDate / TargetDateTo).
   Dữ liệu cũ: giao 1 lần -> 1 ngày; giao N >= 2 lần có khoảng -> N ngày chia đều từ ngày đầu tới ngày cuối (làm tròn); đơn giao bù cũ -> ngày
   từ (+ ngày đến). Sau đó người dùng có thể mở sửa lại ngày từng lần.
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-13_delivery_schedule_date.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID('dbo.DeliveryScheduleDate', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryScheduleDate (
        DeliveryScheduleDateId INT IDENTITY(1,1) CONSTRAINT PK_DeliveryScheduleDate PRIMARY KEY,
        PurchaseOrderId INT NULL CONSTRAINT FK_DSD_PurchaseOrder REFERENCES dbo.PurchaseOrder(PurchaseOrderId) ON DELETE CASCADE,
        OrderId         INT NULL CONSTRAINT FK_DSD_Order         REFERENCES dbo.CustomerOrder(OrderId)         ON DELETE CASCADE,
        RequestId       INT NULL CONSTRAINT FK_DSD_Request       REFERENCES dbo.Request(RequestId)             ON DELETE CASCADE,
        SeqNo           SMALLINT NOT NULL CONSTRAINT CK_DSD_Seq CHECK (SeqNo >= 1),
        DeliveryDate    DATE NOT NULL,
        CONSTRAINT CK_DSD_Owner CHECK ((CASE WHEN PurchaseOrderId IS NULL THEN 0 ELSE 1 END + CASE WHEN OrderId IS NULL THEN 0 ELSE 1 END
                                        + CASE WHEN RequestId IS NULL THEN 0 ELSE 1 END) = 1)
    );
    CREATE UNIQUE INDEX UQ_DSD_PurchaseOrder ON dbo.DeliveryScheduleDate(PurchaseOrderId, SeqNo) WHERE PurchaseOrderId IS NOT NULL;
    CREATE UNIQUE INDEX UQ_DSD_Order         ON dbo.DeliveryScheduleDate(OrderId, SeqNo)         WHERE OrderId IS NOT NULL;
    CREATE UNIQUE INDEX UQ_DSD_Request       ON dbo.DeliveryScheduleDate(RequestId, SeqNo)       WHERE RequestId IS NOT NULL;
END
GO

-- Đơn mua
INSERT INTO dbo.DeliveryScheduleDate (PurchaseOrderId, SeqNo, DeliveryDate)
SELECT po.PurchaseOrderId, n.SeqNo,
       CASE WHEN po.ExpectedDeliveryDateTo IS NULL OR po.ExpectedDeliveryCount < 2 THEN po.ExpectedDeliveryDate
            ELSE DATEADD(DAY, CONVERT(INT, ROUND((n.SeqNo - 1) * DATEDIFF(DAY, po.ExpectedDeliveryDate, po.ExpectedDeliveryDateTo) / (po.ExpectedDeliveryCount - 1.0), 0)),
                         po.ExpectedDeliveryDate) END
FROM dbo.PurchaseOrder po
JOIN (SELECT TOP (99) CONVERT(SMALLINT, ROW_NUMBER() OVER (ORDER BY (SELECT 1))) AS SeqNo FROM sys.all_objects) n
  ON n.SeqNo <= IIF(po.ExpectedDeliveryDateTo IS NULL, 1, po.ExpectedDeliveryCount)
WHERE po.ExpectedDeliveryDate IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.DeliveryScheduleDate d WHERE d.PurchaseOrderId = po.PurchaseOrderId);
GO

-- Lịch khách giao của đơn hàng
INSERT INTO dbo.DeliveryScheduleDate (OrderId, SeqNo, DeliveryDate)
SELECT co.OrderId, n.SeqNo,
       CASE WHEN co.CustomerDeliveryDateTo IS NULL OR co.CustomerDeliveryCount < 2 THEN co.CustomerDeliveryDate
            ELSE DATEADD(DAY, CONVERT(INT, ROUND((n.SeqNo - 1) * DATEDIFF(DAY, co.CustomerDeliveryDate, co.CustomerDeliveryDateTo) / (co.CustomerDeliveryCount - 1.0), 0)),
                         co.CustomerDeliveryDate) END
FROM dbo.CustomerOrder co
JOIN (SELECT TOP (99) CONVERT(SMALLINT, ROW_NUMBER() OVER (ORDER BY (SELECT 1))) AS SeqNo FROM sys.all_objects) n
  ON n.SeqNo <= IIF(co.CustomerDeliveryDateTo IS NULL, 1, co.CustomerDeliveryCount)
WHERE co.CustomerDeliveryDate IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.DeliveryScheduleDate d WHERE d.OrderId = co.OrderId);
GO

-- Đơn giao bù (đơn thiếu NVL): ngày từ, thêm ngày đến nếu có
INSERT INTO dbo.DeliveryScheduleDate (RequestId, SeqNo, DeliveryDate)
SELECT r.RequestId, v.SeqNo, v.D
FROM dbo.Request r
CROSS APPLY (VALUES (CONVERT(SMALLINT, 1), r.TargetDate), (CONVERT(SMALLINT, 2), r.TargetDateTo)) v(SeqNo, D)
WHERE r.RequestTypeCode = 'SHORT_DELIVERY' AND v.D IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.DeliveryScheduleDate d WHERE d.RequestId = r.RequestId);
GO
PRINT N'Đã thêm bảng ngày giao từng lần DeliveryScheduleDate (có khoá ngoại) và điền từ dữ liệu cũ.';
GO
