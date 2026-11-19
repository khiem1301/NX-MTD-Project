/* =====================================================================================
   DEV-ALL-137: thanh toán đơn hàng khách nhiều lần. CustomerOrder thêm FinalAmount (giá trị chốt cả đơn do Kế toán nhập, có thể âm khi
   mình phải đền hợp đồng) và FinalAmountReason; thêm bảng OrderPayment (Amount dương = khách trả, âm = mình trả khách; không xóa,
   sửa trong 24 giờ kèm lý do). Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-18_order_payment.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.CustomerOrder', N'FinalAmount') IS NULL
    ALTER TABLE dbo.CustomerOrder ADD FinalAmount DECIMAL(18,2) NULL, FinalAmountReason NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CustomerOrder_Final' AND parent_object_id = OBJECT_ID(N'dbo.CustomerOrder'))
    ALTER TABLE dbo.CustomerOrder ADD CONSTRAINT CK_CustomerOrder_Final CHECK (FinalAmount IS NULL OR FinalAmountReason IS NOT NULL);
GO

IF OBJECT_ID(N'dbo.OrderPayment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderPayment (
        OrderPaymentId  INT IDENTITY(1,1) CONSTRAINT PK_OrderPayment PRIMARY KEY,
        OrderId         INT            NOT NULL CONSTRAINT FK_OP_Order REFERENCES dbo.CustomerOrder(OrderId),
        PaymentDate     DATE           NOT NULL,
        Amount          DECIMAL(18,2)  NOT NULL CONSTRAINT CK_OP_Amount CHECK (Amount <> 0),
        PaymentMethod   VARCHAR(20)    NOT NULL CONSTRAINT CK_OP_Method CHECK (PaymentMethod IN ('BANK_TRANSFER','CASH')),
        PaymentNote     NVARCHAR(500)  NULL,
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_OP_CreatedAt DEFAULT (dbo.fn_VnNow()),
        UpdatedAt       DATETIME2(0)   NULL,
        EditReason      NVARCHAR(500)  NULL,
        CONSTRAINT CK_OP_Edit CHECK ((UpdatedAt IS NULL AND EditReason IS NULL) OR (UpdatedAt IS NOT NULL AND EditReason IS NOT NULL))
    );
    CREATE INDEX IX_OP_Order ON dbo.OrderPayment(OrderId);
END
GO
