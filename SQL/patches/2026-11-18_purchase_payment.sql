/* =====================================================================================
   DEV-ALL-136: thanh toán đơn mua nhiều lần. Thêm bảng PurchasePayment (mỗi lần trả một dòng, không xóa, sửa trong 24 giờ kèm lý do),
   PurchaseOrder.PaidAmount từ nay là tổng các lần thanh toán; PaymentDate / PaymentMethod / PaymentNote là của lần gần nhất.
   Đơn đã có thanh toán được chuyển thành một dòng (CreatedAt = ngày thanh toán nên không còn sửa được). Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-18_purchase_payment.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- Bản đầu của patch này từng thêm ActualAmountOverride / ActualAmountReason; đã bỏ, dùng PurchaseOrder.ActualAmount (DEV-ALL-129).
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PO_ActualOverride' AND parent_object_id = OBJECT_ID(N'dbo.PurchaseOrder'))
    ALTER TABLE dbo.PurchaseOrder DROP CONSTRAINT CK_PO_ActualOverride;
GO
IF COL_LENGTH(N'dbo.PurchaseOrder', N'ActualAmountOverride') IS NOT NULL ALTER TABLE dbo.PurchaseOrder DROP COLUMN ActualAmountOverride;
IF COL_LENGTH(N'dbo.PurchaseOrder', N'ActualAmountReason') IS NOT NULL ALTER TABLE dbo.PurchaseOrder DROP COLUMN ActualAmountReason;
GO

IF OBJECT_ID(N'dbo.PurchasePayment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchasePayment (
        PurchasePaymentId INT IDENTITY(1,1) CONSTRAINT PK_PurchasePayment PRIMARY KEY,
        PurchaseOrderId   INT            NOT NULL CONSTRAINT FK_PP_PurchaseOrder REFERENCES dbo.PurchaseOrder(PurchaseOrderId),
        PaymentDate       DATE           NOT NULL,
        Amount            DECIMAL(18,2)  NOT NULL CONSTRAINT CK_PP_Amount CHECK (Amount > 0),
        PaymentMethod     VARCHAR(20)    NOT NULL CONSTRAINT CK_PP_Method CHECK (PaymentMethod IN ('BANK_TRANSFER','CASH')),
        PaymentNote       NVARCHAR(500)  NULL,
        CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_PP_CreatedAt DEFAULT (dbo.fn_VnNow()),
        UpdatedAt         DATETIME2(0)   NULL,
        EditReason        NVARCHAR(500)  NULL,
        CONSTRAINT CK_PP_Edit CHECK ((UpdatedAt IS NULL AND EditReason IS NULL) OR (UpdatedAt IS NOT NULL AND EditReason IS NOT NULL))
    );
    CREATE INDEX IX_PP_PurchaseOrder ON dbo.PurchasePayment(PurchaseOrderId);
END
GO

INSERT dbo.PurchasePayment (PurchaseOrderId, PaymentDate, Amount, PaymentMethod, PaymentNote, CreatedAt)
SELECT p.PurchaseOrderId, p.PaymentDate, p.PaidAmount, p.PaymentMethod, p.PaymentNote, CAST(p.PaymentDate AS DATETIME2(0))
FROM dbo.PurchaseOrder p
WHERE p.PaidAmount > 0 AND p.PaymentDate IS NOT NULL AND p.PaymentMethod IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.PurchasePayment x WHERE x.PurchaseOrderId = p.PurchaseOrderId);
GO
