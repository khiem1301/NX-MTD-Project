# Unit Test Case — 10 Service độ phức tạp CAO

> Đầu vào: `QA_HIGH_COMPLEXITY_SERVICES_ANALYSIS.md` (cùng folder `tests/docs/high-complexity-services/`, 2026-11-22)
> Phạm vi: tầng Application Service — 68 public method

## Quy ước chung

- **Framework gợi ý:** xUnit + Moq + FluentAssertions. Query EF dùng EF Core SQLite in-memory (hoặc InMemory provider) seed sẵn. Riêng `IUnitOfWork.BeginTransaction/Commit/Rollback` thì verify qua Moq/spy.
- **Đồng hồ:** `IClock.Today = 2026-11-22`, `IClock.Now = 2026-11-22 10:00 (+07)`, trừ khi case ghi khác.
- **Người dùng:** `ICurrentUser` được mock theo role (TeamLeader / Manager / Admin / Thủ kho / Kế toán) và teamId.
- **Tên mã lỗi** (`NotFound`, `InvalidStatus`, `PeriodLocked`…) lấy theo tài liệu phân tích. Khi viết code test thì thay bằng hằng số `ErrorCodes` thật của dự án.
- **Expected** ghi hành vi **đúng theo nghiệp vụ**. Case có ký hiệu **⚠** là case dự kiến **FAIL trên code hiện tại** (đã nêu trong mục "Điểm nghi ngờ" của tài liệu phân tích). Khi chạy FAIL thì log bug, không sửa expected cho khớp code.
- **N** = Normal, **B** = Boundary, **A** = Abnormal.

---

## 1. OperationService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID01 | OperationService.ListAsync | N | Seed Product P1 (Id=1) gồm 3 công đoạn: OP01, OP02 Active; OP03 Discontinued. PieceRate OP01: APPROVED hiệu lực 2026-11-01. | productId=1, includeDiscontinued=false | `Result.Success`, 2 phần tử (OP01, OP02); OP01.CurrentRate = rate 2026-11-01 |
| UTCID02 | OperationService.ListAsync | N | Như trên | productId=1, includeDiscontinued=true | Success, 3 phần tử, có OP03 với Status=Discontinued |
| UTCID03 | OperationService.ListAsync | B | Không cần seed | productId=0 | `Result.Failure` (Validation), repo `Operation` không được query |
| UTCID04 | OperationService.ListAsync | B | Không cần seed | productId=-1 | Failure (Validation) |
| UTCID05 | OperationService.ListAsync | B | Product Id=1 tồn tại, 0 công đoạn | productId=1, includeDiscontinued=false | Success, danh sách rỗng (Count=0) |
| UTCID06 | OperationService.ListAsync | B | OP01 có rate APPROVED EffectiveDate = 2026-11-22 (= Today), không có rate tương lai | productId=1 | OP01.CurrentRate = rate đó; UpcomingRate = null (ngày = Today tính là hiện hành) |
| UTCID07 | OperationService.ListAsync | B | OP01: rate APPROVED tương lai 2026-12-01 + rate PENDING 2026-12-15 | productId=1 | NextRate = PENDING (ưu tiên PENDING); UpcomingRate = APPROVED 2026-12-01. Ghi nhận 2 giá trị khác nhau (điểm nghi ngờ #4) |
| UTCID08 | OperationService.ListAsync | A | Không có product Id=999 | productId=999 | Failure `NotFound` (nhất quán với CreateAsync). ⚠ Code hiện tại trả Success với list rỗng (#8) |
| UTCID09 | OperationService.CreateAsync | N | Seed Product Id=1; mock `IAuditLogService`; UoW transaction spy | dto {ProductId=1, OperationCode="OP10", Name="May cổ", StationSequence=10, StandardTimeSec=45, IsFinalStep=false} | `Result<int>.Success`, Data > 0; BeginTransaction 1 lần, SaveChanges 2 lần, Commit 1 lần; Audit được log 1 lần |
| UTCID10 | OperationService.CreateAsync | N | Product 1 đã có OP20 IsFinalStep=true, Active | dto {OperationCode="OP30", StationSequence=30, IsFinalStep=true} | Success; OP30.IsFinalStep=true; OP20.IsFinalStep=false (MoveFinalStep) |
| UTCID11 | OperationService.CreateAsync | N | Seed OperationGroup "Lắp ráp", OperationComponent "Thân trước", "Tay" | dto có GroupName="Lắp ráp", Components=["Thân trước","Tay"] | Success; công đoạn gắn đúng GroupId và 2 link component |
| UTCID12 | OperationService.CreateAsync | B | Product 1 tồn tại | OperationCode="  OP11  " | Success; mã lưu DB = "OP11" (đã Trim) |
| UTCID13 | OperationService.CreateAsync | B | Product 1 tồn tại; cấu hình MaxLength OperationCode = 50 | OperationCode = chuỗi 50 ký tự | Success |
| UTCID14 | OperationService.CreateAsync | B | Như trên | OperationCode = chuỗi 51 ký tự | Failure (Validation), không gọi SaveChanges |
| UTCID15 | OperationService.CreateAsync | B | Product 1 tồn tại | OperationCode="" (rỗng) | Failure (Validation) |
| UTCID16 | OperationService.CreateAsync | B | Product 1 tồn tại | StandardTimeSec=0 | Failure (Validation). ⚠ Service không check, phụ thuộc validator (#5) |
| UTCID17 | OperationService.CreateAsync | B | Product 1 tồn tại | StandardTimeSec=-1 | Failure (Validation). ⚠ (#5) |
| UTCID18 | OperationService.CreateAsync | B | Product 1 tồn tại | Components=[] (0 phần tử), GroupName=null | Success, không tạo link component nào |
| UTCID19 | OperationService.CreateAsync | A | — | dto = null | `ArgumentNullException` hoặc Failure (Validation), không mở transaction |
| UTCID20 | OperationService.CreateAsync | A | Product 1 tồn tại | OperationCode=null | Failure (Validation). ⚠ Code hiện tại ném `NullReferenceException` tại `.Trim()` (#6) |
| UTCID21 | OperationService.CreateAsync | A | Không có Product 999 | dto.ProductId=999 | Failure `NotFound`; repo Add không được gọi |
| UTCID22 | OperationService.CreateAsync | A | Product 1 đã có OP10 | OperationCode="OP10" | Failure (DuplicateCode); không SaveChanges |
| UTCID23 | OperationService.CreateAsync | A | Product 1 đã có công đoạn STT=10 | StationSequence=10, code mới | Failure (DuplicateSequence) |
| UTCID24 | OperationService.CreateAsync | A | Mock SaveChanges lần 2 ném `DbUpdateException` | dto hợp lệ, IsFinalStep=true | Rollback được gọi; không Commit; exception propagate (hoặc Failure). Final step cũ giữ nguyên |
| UTCID25 | OperationService.UpdateAsync | N | Seed OP10 (Id=5, ProductId=1, Active) | operationId=5, dto {Name="May cổ mới", StandardTimeSec=50} | Success; Name, StandardTimeSec cập nhật; Audit log |
| UTCID26 | OperationService.UpdateAsync | N | OP10 Active; OP20 đang IsFinalStep | operationId=5, IsFinalStep=true | Success; OP10 final, OP20 bỏ final |
| UTCID27 | OperationService.UpdateAsync | B | OP10 Id=5 | dto.OperationCode="OP10" (trùng chính nó) | Success (không tính là trùng mã với chính mình) |
| UTCID28 | OperationService.UpdateAsync | B | OP10 Id=5 ProductId=1 | dto.ProductId=2 | Success; ProductId vẫn = 1 (ép theo entity cũ) |
| UTCID29 | OperationService.UpdateAsync | B | OP10 Discontinued; OP20 final Active | IsFinalStep=true | Không MoveFinalStep (chỉ chạy khi Active); OP20 vẫn final |
| UTCID30 | OperationService.UpdateAsync | A | Không có Id 999 | operationId=999 | Failure `NotFound` |
| UTCID31 | OperationService.UpdateAsync | A | — | operationId=0 | Failure `NotFound`/Validation |
| UTCID32 | OperationService.UpdateAsync | A | OP10 (Id=5), OP20 (Id=6) cùng product | operationId=5, OperationCode="OP20" | Failure (DuplicateCode) |
| UTCID33 | OperationService.UpdateAsync | A | OP10 Id=5 | OperationCode=null | Failure (Validation). ⚠ NRE tại `.Trim()` (#6) |
| UTCID34 | OperationService.UpdateAsync | A | Mock SaveChanges ném `DbUpdateConcurrencyException` | operationId=5, dto hợp lệ | Exception propagate / Failure (Concurrency); transaction rollback |
| UTCID35 | OperationService.DiscontinueAsync | N | OP10 Active, không có WorkAllocation PENDING, mọi RemainingQty = 0 | operationId=5 | Success; Status = Discontinued; Audit log |
| UTCID36 | OperationService.DiscontinueAsync | B | OP10 đã Discontinued | operationId=5 | Success (idempotent); SaveChanges không gọi |
| UTCID37 | OperationService.DiscontinueAsync | B | OP10 có 1 WorkAllocation PENDING | operationId=5 | Failure `HasOpenWork` |
| UTCID38 | OperationService.DiscontinueAsync | B | VwWorkerOutputCap RemainingQty=1, đơn chưa đóng | operationId=5 | Failure `HasOpenWork` |
| UTCID39 | OperationService.DiscontinueAsync | B | RemainingQty=5 nhưng đơn đã đóng (Completed/Cancelled) | operationId=5 | Success (đơn đóng không tính open work) |
| UTCID40 | OperationService.DiscontinueAsync | A | Không có Id 999 | operationId=999 | Failure `NotFound` |
| UTCID41 | OperationService.DiscontinueAsync | A | OP20 là công đoạn cuối Active duy nhất của Product 1 | operationId=OP20 | Failure (FinalStepRequired). ⚠ Code hiện tại Success → mã hàng mất final step (#1) |
| UTCID42 | OperationService.ReactivateAsync | N | OP03 Discontinued, IsFinalStep=false | operationId=OP03 | Success; Status = Active |
| UTCID43 | OperationService.ReactivateAsync | B | OP03 Discontinued, IsFinalStep=true; product không còn final Active | operationId=OP03 | Success; OP03 Active và là final duy nhất |
| UTCID44 | OperationService.ReactivateAsync | B | OP10 đang Active | operationId=OP10 | Success no-op (idempotent), SaveChanges không gọi |
| UTCID45 | OperationService.ReactivateAsync | A | OP03 Discontinued, IsFinalStep=true; OP20 final Active đã tồn tại | operationId=OP03 | Failure (FinalStepConflict) |
| UTCID46 | OperationService.ReactivateAsync | A | Không có Id 999 | operationId=999 | Failure `NotFound` |
| UTCID47 | OperationService.PreviewImportAsync | N | Product 1 tồn tại; mock `IExcelOperationParser` trả 1 sheet "CongDoan", 3 dòng hợp lệ (STT 10/20/30, dòng 30 final) | productId=1, stream hợp lệ, sheetName="CongDoan" | Success Preview 3 dòng, Action Create/Update đúng; **không** SaveChanges |
| UTCID48 | OperationService.PreviewImportAsync | B | Parser trả 2 sheet | sheetName=null | Dùng sheet đầu tiên |
| UTCID49 | OperationService.PreviewImportAsync | B | Parser trả sheet chỉ có header, 0 dòng | productId=1 | Success, 0 dòng (hoặc Failure Empty theo spec); không exception |
| UTCID50 | OperationService.PreviewImportAsync | B | Parser trả 2 dòng cùng STT=10 | productId=1 | 2 dòng Action=Error, lỗi trùng STT |
| UTCID51 | OperationService.PreviewImportAsync | B | Parser trả 2 dòng IsFinal=true | productId=1 | Các dòng final đánh dấu Error "nhiều công đoạn cuối" |
| UTCID52 | OperationService.PreviewImportAsync | B | Dòng có component "Cầu vai" không tồn tại | productId=1 | Dòng hợp lệ + warning component skip |
| UTCID53 | OperationService.PreviewImportAsync | B | Dòng STT=32767 | productId=1 | Dòng hợp lệ, StationSequence=32767 |
| UTCID54 | OperationService.PreviewImportAsync | B | Dòng STT=32768 | productId=1 | Dòng Error (STT vượt giới hạn). ⚠ Cast `(short)` tràn số âm (#9) |
| UTCID55 | OperationService.PreviewImportAsync | A | Parser ném exception (file không phải xlsx) | stream rác | Failure (ExcelInvalid); exception được catch |
| UTCID56 | OperationService.PreviewImportAsync | A | Parser trả sheet "CongDoan" | sheetName="XYZ" | Failure (SheetNotFound) |
| UTCID57 | OperationService.PreviewImportAsync | A | Parser trả list sheet rỗng | sheetName=null | Failure (ExcelInvalid). ⚠ Code hiện tại ném `ArgumentOutOfRangeException` tại `sheets[0]` (#7) |
| UTCID58 | OperationService.PreviewImportAsync | A | Không có Product 999 | productId=999, stream hợp lệ | Failure `NotFound` |
| UTCID59 | OperationService.PreviewImportAsync | A | — | stream = null | `ArgumentNullException` hoặc Failure (ExcelInvalid) |
| UTCID60 | OperationService.ImportAsync | N | Product 1 rỗng; parser trả 3 dòng hợp lệ | replace=false, submitRates=false | Success ImportResultDto {Created=3}; Commit 1 lần; không tạo PieceRate |
| UTCID61 | OperationService.ImportAsync | N | Như trên; mock `INotificationService` | submitRates=true, effectiveDate=2026-12-01 | 3 PieceRate PENDING EffectiveDate=2026-12-01; Notifier gửi Admin đúng 1 lần |
| UTCID62 | OperationService.ImportAsync | N | Product 1 có OP99 chưa dùng (không có Output/Defect/Assignment), file không có OP99 | replace=true | Success; OP99 bị xóa; Deleted=1 |
| UTCID63 | OperationService.ImportAsync | B | OP99 đã có OutputRecord, file không có OP99 | replace=true | OP99 không bị xóa; kết quả báo blocked/used |
| UTCID64 | OperationService.ImportAsync | B | Parser trả 3 dòng, cả 3 Error | replace=true | Failure (NoValidRows) hoặc Success kèm cảnh báo rõ "0 thay đổi". ⚠ Hiện tại Success im lặng, dễ hiểu nhầm đã replace (#2) |
| UTCID65 | OperationService.ImportAsync | B | 3 dòng hợp lệ | submitRates=true, effectiveDate = 2026-11-22 (= Today) | Success, rate EffectiveDate = Today |
| UTCID66 | OperationService.ImportAsync | B | 3 dòng hợp lệ | submitRates=true, effectiveDate=null | Rate dùng ngày mặc định (Today) |
| UTCID67 | OperationService.ImportAsync | B | OP10 có component cũ "Tay"; dòng import chỉ có component không tồn tại | replace=false | Component cũ "Tay" giữ nguyên + warning. Assert warning có trong kết quả (#3) |
| UTCID68 | OperationService.ImportAsync | A | Parser trả 2 dòng IsFinal=true | replace=false | Failure (FinalStep); không SaveChanges |
| UTCID69 | OperationService.ImportAsync | A | Parser ném exception | stream rác | Failure (ExcelInvalid); BeginTransaction không gọi |
| UTCID70 | OperationService.ImportAsync | A | SaveChanges ném `DbUpdateException` | submitRates=true | Rollback; Notifier **không** được gọi |

---

## 2. MaterialReceiptService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID71 | MaterialReceiptService.GetPagedAsync | N | Seed 5 phiếu: 3 Pending, 2 Confirmed | query {Status=Pending, Page=1, PageSize=10} | Success; Items=3 Pending; TotalCount=3 |
| UTCID72 | MaterialReceiptService.GetPagedAsync | N | Phiếu thuộc đơn "DH2611-001" | query {Keyword="DH2611"} | Trả về các phiếu có code/đơn/PO chứa keyword |
| UTCID73 | MaterialReceiptService.GetPagedAsync | B | 5 phiếu | Page=2, PageSize=1 | Items=1 (phiếu thứ 2 theo sort); TotalCount=5 |
| UTCID74 | MaterialReceiptService.GetPagedAsync | B | 5 phiếu | Page=10, PageSize=10 (vượt trang) | Items rỗng; TotalCount=5 |
| UTCID75 | MaterialReceiptService.GetPagedAsync | B | 5 phiếu | Keyword="   " | Không áp filter, trả 5 |
| UTCID76 | MaterialReceiptService.GetPagedAsync | B | DB rỗng | query mặc định | Success; Items rỗng; Total=0 |
| UTCID77 | MaterialReceiptService.GetPagedAsync | A | 5 phiếu | OrderId=999 (không tồn tại) | Success; Items rỗng (không lỗi) |
| UTCID78 | MaterialReceiptService.GetPagedAsync | A | 5 phiếu | Page=0, PageSize=0 | Chuẩn hóa về Page=1/PageSize mặc định, hoặc Failure Validation; không exception chia 0 |
| UTCID79 | MaterialReceiptService.GetByIdAsync | N | Phiếu Id=10 Confirmed, 2 dòng, là lần giao thứ 2 | receiptId=10 | Success; Lines=2; DeliveryNo=2 |
| UTCID80 | MaterialReceiptService.GetByIdAsync | N | Phiếu 10 có Request SHORT_DELIVERY, ReplenishQty=50 | receiptId=10 | ShortDeliveryRequestId ≠ null; ToReplenishQty=50 |
| UTCID81 | MaterialReceiptService.GetByIdAsync | B | Phiếu 10 và 11 Confirmed cùng ConfirmedAt | receiptId=11 | DeliveryNo phiếu 11 = DeliveryNo phiếu 10 + 1 (tie-break theo Id) |
| UTCID82 | MaterialReceiptService.GetByIdAsync | B | Phiếu 12 Pending, 0 dòng | receiptId=12 | Success; Lines rỗng; không exception |
| UTCID83 | MaterialReceiptService.GetByIdAsync | A | Không có 999 | receiptId=999 | Failure `NotFound` |
| UTCID84 | MaterialReceiptService.GetByIdAsync | A | — | receiptId=0 | Failure `NotFound` |
| UTCID85 | MaterialReceiptService.GetByIdAsync | A | Dòng phiếu có OrderMaterial = null (data lệch) | receiptId=13 | Failure hoặc MaterialId=null an toàn. ⚠ NRE tại `l.MaterialId!.Value` (#4) |
| UTCID86 | MaterialReceiptService.CreateAsync | N | Đơn 1 có NVL M1 cần 200, đã nhận 0; mock `IDocumentCodeGenerator` → "PN2611-001"; mock Notifier | dto {Source=CUSTOMER, OrderId=1, ReceiptDate=2026-11-22, Lines=[M1, AcceptedQty=100]} | Success DTO Status=Pending, Code="PN2611-001"; Notifier gọi 1 lần |
| UTCID87 | MaterialReceiptService.CreateAsync | N | PO 5 (Supplier S1) dòng M2 = 300 | dto {Source=SUPPLIER, POId=5, Lines=[M2,150]} | Success; SupplierId=S1 |
| UTCID88 | MaterialReceiptService.CreateAsync | B | M1 outstanding=100 | AcceptedQty=100 | Success |
| UTCID89 | MaterialReceiptService.CreateAsync | B | Đơn 1 hợp lệ | Lines=[] (0 dòng) | Failure (Validation) |
| UTCID90 | MaterialReceiptService.CreateAsync | B | DeliveryCountGuard: max 3 lần, đã có 2 | dto hợp lệ | Success (lần thứ 3) |
| UTCID91 | MaterialReceiptService.CreateAsync | B | Đã đủ 3 lần giao | dto hợp lệ | Failure `DeliveryCountReached` |
| UTCID92 | MaterialReceiptService.CreateAsync | A | — | Source="OTHER" | Failure (Validation) |
| UTCID93 | MaterialReceiptService.CreateAsync | A | Không có đơn 999 | Source=CUSTOMER, OrderId=999 | Failure `NotFound` |
| UTCID94 | MaterialReceiptService.CreateAsync | A | Đã có phiếu Pending cho M1 của đơn 1 | Lines=[M1, 50] | Failure (DuplicatePending) |
| UTCID95 | MaterialReceiptService.CreateAsync | A | Shortage request của M1 đã đóng | dto M1 | Failure (ShortageClosed) |
| UTCID96 | MaterialReceiptService.CreateAsync | A | Mock transaction trả `created.IsSuccess=false` | dto hợp lệ | Trả nguyên Failure. ⚠ Code hiện tại vẫn gọi `GetByIdAsync(created.Data)` (#7) |
| UTCID97 | MaterialReceiptService.UpdateAsync | N | Phiếu 10 Pending, 1 dòng M1=100 | receiptId=10, Lines=[M1=80, M3=20] | Success; DTO có 2 dòng mới |
| UTCID98 | MaterialReceiptService.UpdateAsync | N | Phiếu 10 Returned | receiptId=10, dto hợp lệ | Success (Returned được sửa) |
| UTCID99 | MaterialReceiptService.UpdateAsync | B | M1 outstanding = 100 (không tính chính phiếu 10 đang giữ 100) | receiptId=10, AcceptedQty=100 | Success. ⚠ Nếu không truyền `exceptReceiptId` sẽ đếm trùng → Failure sai (#9) |
| UTCID100 | MaterialReceiptService.UpdateAsync | B | Phiếu 10 Pending | Lines=[] | Failure (Validation) |
| UTCID101 | MaterialReceiptService.UpdateAsync | A | Phiếu 10 Confirmed | receiptId=10 | Failure `InvalidStatus` |
| UTCID102 | MaterialReceiptService.UpdateAsync | A | Phiếu 14 là phiếu bổ sung (supplementary) Pending | receiptId=14 | Failure `InvalidStatus` |
| UTCID103 | MaterialReceiptService.UpdateAsync | A | Không có 999 | receiptId=999 | Failure `NotFound` |
| UTCID104 | MaterialReceiptService.UpdateAsync | A | Dòng cũ có ảnh/attachment hoặc OriginalReceiptLine tham chiếu | receiptId=10, Lines mới | Tham chiếu được giữ hoặc chặn update. ⚠ Xóa hết rồi thêm lại → orphan (#5) |
| UTCID105 | MaterialReceiptService.CreateSupplementaryAsync | N | Shortage request 20, line RL1 ReplenishQty=50, đã bổ sung 0 | dto {ShortageRequestId=20, ReceiptDate=2026-11-22, Lines=[RL1, 30]} | Success; phiếu bổ sung Pending |
| UTCID106 | MaterialReceiptService.CreateSupplementaryAsync | B | remaining = 50 | AcceptedQty=50 | Success |
| UTCID107 | MaterialReceiptService.CreateSupplementaryAsync | B | remaining = 50 | AcceptedQty=51 | Failure `ExceedsReplenish` |
| UTCID108 | MaterialReceiptService.CreateSupplementaryAsync | B | Đã có phiếu bổ sung Pending 20 (includePending) → remaining=30 | AcceptedQty=31 | Failure `ExceedsReplenish` |
| UTCID109 | MaterialReceiptService.CreateSupplementaryAsync | B | Request tạo ngày 2026-11-20 | ReceiptDate=2026-11-20 (cùng ngày) | Success |
| UTCID110 | MaterialReceiptService.CreateSupplementaryAsync | B | Request tạo ngày 2026-11-20 | ReceiptDate=2026-11-19 | Failure (ReceiptDate invalid) |
| UTCID111 | MaterialReceiptService.CreateSupplementaryAsync | A | Không có request 999 | ShortageRequestId=999 | Failure `NotFound` |
| UTCID112 | MaterialReceiptService.CreateSupplementaryAsync | A | Request 20 đã Resolved/Closed | ShortageRequestId=20 | Failure `NotResolvable` |
| UTCID113 | MaterialReceiptService.CreateSupplementaryAsync | A | DeliveryCountGuard trả đã đủ lần | dto hợp lệ | Failure `DeliveryCountReached` |
| UTCID114 | MaterialReceiptService.CreateSupplementaryAsync | A | RL99 không thuộc request 20 | Lines=[RL99, 10] | Failure (Validation) |
| UTCID115 | MaterialReceiptService.ConfirmAsync | N | Phiếu 10 Pending, M1 AcceptedQty=100 ≤ outstanding 200, có ảnh; mock `IStockService.PostAsync` → Success | receiptId=10, confirmedByEmployeeId=7 | Success; Stock nhận +100 (1 lần); MaterialSupplyUpdater.Recalculate gọi; Status=Confirmed, ConfirmedAt=Now, ConfirmedBy=7 |
| UTCID116 | MaterialReceiptService.ConfirmAsync | N | Lần giao cuối (3/3), M1 cần 200, đã nhận 150, phiếu 50 → thiếu 0; một trường hợp khác nhận 30 → thiếu 20 | receiptId=10 (nhận 30) | Success + message `SHORT_DELIVERY`; ShortageTracker tạo request thiếu 20 |
| UTCID117 | MaterialReceiptService.ConfirmAsync | B | outstanding=100 | AcceptedQty=100 | Success |
| UTCID118 | MaterialReceiptService.ConfirmAsync | B | outstanding=100, nguồn SUPPLIER | AcceptedQty=101 | Failure `ExceedsPurchase`; Stock không gọi |
| UTCID119 | MaterialReceiptService.ConfirmAsync | B | outstanding=100, nguồn CUSTOMER | AcceptedQty=101 | Failure `ExceedsCustomer` |
| UTCID120 | MaterialReceiptService.ConfirmAsync | B | Mọi dòng AcceptedQty=0 | receiptId=10 | Theo policy: Failure (EmptyReceipt) hoặc không tính lần giao. ⚠ Hiện tại Success, Stock không gọi nhưng vẫn đếm lần giao / có thể kích SHORT_DELIVERY (#3) |
| UTCID121 | MaterialReceiptService.ConfirmAsync | B | NotReplenishedQty lớn → outstanding = -20 | AcceptedQty=10 | Failure Exceeds với message không hiển thị số âm. ⚠ Message "còn -20" (#6) |
| UTCID122 | MaterialReceiptService.ConfirmAsync | A | Không có 999 | receiptId=999 | Failure `NotFound` |
| UTCID123 | MaterialReceiptService.ConfirmAsync | A | Phiếu 10 đã Confirmed | receiptId=10 | Failure `InvalidStatus`; Stock không gọi |
| UTCID124 | MaterialReceiptService.ConfirmAsync | A | Phiếu 10 Returned | receiptId=10 | Failure `InvalidStatus` |
| UTCID125 | MaterialReceiptService.ConfirmAsync | A | Phiếu 10 Pending, chưa có ảnh | receiptId=10 | Failure `PhotoRequired` |
| UTCID126 | MaterialReceiptService.ConfirmAsync | A | Phiếu 10 Pending hợp lệ | confirmedByEmployeeId=0 | Failure (Validation). ⚠ Service không check, lỗi FK muộn (#1) |
| UTCID127 | MaterialReceiptService.ConfirmAsync | A | Mock `IStockService` trả Failure | receiptId=10 | Failure (stock); Rollback; Status vẫn Pending |
| UTCID128 | MaterialReceiptService.ConfirmAsync | A | 2 phiếu Pending P10 (80) và P11 (80) cùng M1, outstanding=100; P10 đã Confirm trước | Confirm P11 | Failure `ExceedsPurchase/Customer` (others Confirmed=80, 80+80 > 100) (#2) |
| UTCID129 | MaterialReceiptService.ReturnAsync | N | Phiếu 10 Pending; mock Notifier | receiptId=10, Reason="Sai số lượng" | Success; Status=Returned; Notifier gửi Thủ kho |
| UTCID130 | MaterialReceiptService.ReturnAsync | B | Phiếu 10 Pending; MaxLength Reason=500 | Reason = 500 ký tự | Success |
| UTCID131 | MaterialReceiptService.ReturnAsync | B | Phiếu 10 Pending | Reason="" | Failure (Validation) |
| UTCID132 | MaterialReceiptService.ReturnAsync | A | Phiếu 10 Pending | Reason=null | Failure (Validation). ⚠ NRE tại `dto.Reason.Trim()` (#8) |
| UTCID133 | MaterialReceiptService.ReturnAsync | A | Phiếu 10 Confirmed | Reason="x" | Failure `InvalidStatus` |
| UTCID134 | MaterialReceiptService.ReturnAsync | A | Không có 999 | receiptId=999 | Failure `NotFound` |

---

## 3. PayrollService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID135 | PayrollService.ExportAsync | N | Period 2026-10 tồn tại; mock `IPayrollExcelWriter` trả byte[] | periodMonth="2026-10" | Success ExcelFileDto: FileName có "2026-10", Content.Length > 0 |
| UTCID136 | PayrollService.ExportAsync | N | Chưa có period 2026-12 | periodMonth="2026-12" | Success; period 2026-12 được tạo (GetOrCreatePeriod) |
| UTCID137 | PayrollService.ExportAsync | B | — | periodMonth="2026-01" | Success (tháng biên dưới) |
| UTCID138 | PayrollService.ExportAsync | B | — | periodMonth="2026-13" | Failure `PeriodMonthInvalid` |
| UTCID139 | PayrollService.ExportAsync | B | — | periodMonth="2026-00" | Failure `PeriodMonthInvalid` |
| UTCID140 | PayrollService.ExportAsync | B | — | periodMonth="" | Failure `PeriodMonthInvalid` |
| UTCID141 | PayrollService.ExportAsync | A | — | periodMonth=null | Failure `PeriodMonthInvalid` (không exception) |
| UTCID142 | PayrollService.ExportAsync | A | — | periodMonth="2026/10" | Failure `PeriodMonthInvalid` |
| UTCID143 | PayrollService.PreviewImportAsync | N | Mock `IExcelPayrollParser` trả 3 dòng hợp lệ (NV001–003, NetPay = Σ items) | stream hợp lệ | Success; 3 dòng Valid |
| UTCID144 | PayrollService.PreviewImportAsync | B | Parser trả 0 dòng | stream chỉ có header | Failure (Empty) |
| UTCID145 | PayrollService.PreviewImportAsync | B | Dòng WorkDays=0 | stream | Dòng Valid |
| UTCID146 | PayrollService.PreviewImportAsync | B | Dòng WorkDays=31 | stream | Dòng Valid |
| UTCID147 | PayrollService.PreviewImportAsync | B | Dòng WorkDays=32 | stream | Dòng Error |
| UTCID148 | PayrollService.PreviewImportAsync | B | Dòng WorkDays=-1 | stream | Dòng Error |
| UTCID149 | PayrollService.PreviewImportAsync | B | Dòng WorkDays=null | stream | Dòng Error (bắt buộc). ⚠ Hiện tại Valid (#2) |
| UTCID150 | PayrollService.PreviewImportAsync | B | NetPay lệch Σ items đúng 1.000đ | stream | Valid, không warning |
| UTCID151 | PayrollService.PreviewImportAsync | B | NetPay lệch Σ items 1.001đ | stream | Valid + warning lệch NetPay (#1) |
| UTCID152 | PayrollService.PreviewImportAsync | B | NV001 xuất hiện 2 dòng | stream | Dòng sau đánh dấu Duplicate |
| UTCID153 | PayrollService.PreviewImportAsync | A | Parser ném exception | stream rác | Failure (ExcelInvalid), không ném ra ngoài |
| UTCID154 | PayrollService.PreviewImportAsync | A | Dòng NetPay=null | stream | Dòng Error |
| UTCID155 | PayrollService.PreviewImportAsync | A | Dòng items rỗng | stream | Dòng Error |
| UTCID156 | PayrollService.PreviewImportAsync | A | Dòng mã NV "NV999" không tồn tại | stream | Dòng Error (EmployeeNotFound) |
| UTCID157 | PayrollService.ImportAsync | N | Payroll 2026-10 Draft, period mở, có file cũ "old.xlsx"; mock `IPayrollFileStore` | stream 3 dòng hợp lệ, periodMonth="2026-10" | Success ImportResult {Imported=3}; FileStore.Save gọi 1 lần; Delete("old.xlsx") gọi; payslips cũ thay bằng mới |
| UTCID158 | PayrollService.ImportAsync | N | Payroll 2026-10 Rejected | stream hợp lệ | Success (Rejected được import lại) |
| UTCID159 | PayrollService.ImportAsync | B | Stream 2 dòng Valid + 1 Error | periodMonth="2026-10" | Success; Imported=2, ErrorCount=1. Response phải có error count (#3) |
| UTCID160 | PayrollService.ImportAsync | B | Stream toàn dòng Error | periodMonth="2026-10" | Failure (EmptyValid); FileStore.Save **không** gọi |
| UTCID161 | PayrollService.ImportAsync | A | AccountingPeriod 2026-10 Locked | stream hợp lệ | Failure `PeriodLocked`; FileStore.Save không gọi |
| UTCID162 | PayrollService.ImportAsync | A | Payroll 2026-10 PendingApproval | stream hợp lệ | Failure `InvalidStatus` |
| UTCID163 | PayrollService.ImportAsync | A | Payroll 2026-10 Approved | stream hợp lệ | Failure `InvalidStatus` |
| UTCID164 | PayrollService.ImportAsync | A | — | periodMonth="10-2026" | Failure `PeriodMonthInvalid` |
| UTCID165 | PayrollService.ImportAsync | A | Mock persist (SaveChanges) ném exception sau khi FileStore.Save "new.xlsx" | stream hợp lệ | Failure; FileStore.Delete("new.xlsx") được gọi; file cũ **không** bị xóa (#7) |
| UTCID166 | PayrollService.ImportAsync | A | Parser ném exception | stream rác | Failure (ExcelInvalid) |
| UTCID167 | PayrollService.GetByPeriodMonthAsync | N | Payroll 2026-10 có 3 payslip | periodMonth="2026-10" | Success Summary: EmployeeCount=3, TotalNetPay đúng |
| UTCID168 | PayrollService.GetByPeriodMonthAsync | B | — | periodMonth="2026-13" | Failure `PeriodMonthInvalid` |
| UTCID169 | PayrollService.GetByPeriodMonthAsync | A | Chưa có payroll 2026-09 | periodMonth="2026-09" | Failure `NotFound` |
| UTCID170 | PayrollService.SubmitAsync | N | Payroll Id=1 Draft, 5 payslip | payrollId=1 | Success; Status=PendingApproval; Audit log |
| UTCID171 | PayrollService.SubmitAsync | B | Payroll Id=2 Draft, 0 payslip | payrollId=2 | Failure (Empty) |
| UTCID172 | PayrollService.SubmitAsync | A | Payroll Id=3 Approved | payrollId=3 | Failure `InvalidStatus` |
| UTCID173 | PayrollService.SubmitAsync | A | Không có 999 | payrollId=999 | Failure `NotFound` |
| UTCID174 | PayrollService.GetImportedFileAsync | N | Payroll 1 có ImportedFilePath; FileStore trả stream | payrollId=1 | Success File (Content, ContentType xlsx) |
| UTCID175 | PayrollService.GetImportedFileAsync | B | Payroll 4 chưa import (FilePath=null) | payrollId=4 | Failure `FileNotFound` |
| UTCID176 | PayrollService.GetImportedFileAsync | A | FilePath có nhưng FileStore trả null (file bị xóa trên disk) | payrollId=1 | Failure `FileNotFound` |
| UTCID177 | PayrollService.GetImportedFileAsync | A | Không có 999 | payrollId=999 | Failure `NotFound` |
| UTCID178 | PayrollService.GetPayrollsAsync | N | 3 payroll: Draft, PendingApproval, Approved | query {Status="PendingApproval", Page=1} | Success; 1 item (map sang raw PENDING_APPROVAL) |
| UTCID179 | PayrollService.GetPayrollsAsync | B | 3 payroll | Status=null | Trả cả 3 |
| UTCID180 | PayrollService.GetPayrollsAsync | B | 3 payroll | Page=5, PageSize=10 | Items rỗng, Total=3 |
| UTCID181 | PayrollService.GetPayrollsAsync | A | 3 payroll | Status="Foo" | Failure (Validation 400). ⚠ Hiện tại trả list rỗng (#5) |
| UTCID182 | PayrollService.GetDetailAsync | N | Payroll 1 có 5 payslip | payrollId=1 | Success Detail: 5 payslip, đúng Status |
| UTCID183 | PayrollService.GetDetailAsync | B | Payroll 2 Draft, 0 payslip | payrollId=2 | Success; Payslips rỗng, Total=0 |
| UTCID184 | PayrollService.GetDetailAsync | A | — | payrollId=999 | Failure `NotFound` |
| UTCID185 | PayrollService.ApproveAsync | N | Payroll 1 PendingApproval; AccountingPeriod 2026-10 mở | payrollId=1 | Success; Status=Approved; AccountingPeriod.IsLocked=true; Audit log |
| UTCID186 | PayrollService.ApproveAsync | B | Payroll PendingApproval nhưng 0 payslip (data bất thường) | payrollId=5 | Failure (Empty). ⚠ Hiện tại vẫn Approve (#8) |
| UTCID187 | PayrollService.ApproveAsync | A | Payroll 2 Draft | payrollId=2 | Failure `InvalidStatus`; period không bị khóa |
| UTCID188 | PayrollService.ApproveAsync | A | — | payrollId=999 | Failure `NotFound` |
| UTCID189 | PayrollService.RejectAsync | N | Payroll 1 PendingApproval | payrollId=1, Reason="Sai phụ cấp" | Success; Status=Rejected; RejectReason lưu |
| UTCID190 | PayrollService.RejectAsync | B | Payroll 1 PendingApproval | Reason="   " | Failure (Validation). ⚠ Service không check (#9) |
| UTCID191 | PayrollService.RejectAsync | A | Payroll 2 Draft | Reason="x" | Failure `InvalidStatus` |
| UTCID192 | PayrollService.RejectAsync | A | — | payrollId=999, Reason="x" | Failure `NotFound` |
| UTCID193 | PayrollService.ReopenAsync | N | Payroll 1 Approved; period 2026-10 Locked | payrollId=1, Reason="Điều chỉnh thưởng" | Success; Status=Draft; period IsLocked=false (side-effect cả tháng, #4) |
| UTCID194 | PayrollService.ReopenAsync | B | Payroll 1 Approved | Reason=null | Failure (Validation; SRS bắt buộc lý do). ⚠ (#9) |
| UTCID195 | PayrollService.ReopenAsync | A | Payroll 2 Draft | Reason="x" | Failure `InvalidStatus` |
| UTCID196 | PayrollService.ReopenAsync | A | — | payrollId=999 | Failure `NotFound` |
| UTCID197 | PayrollService.GetMyPayslipsAsync | N | NV Id=10 có payslip Approved tháng 08, 09, 10 | employeeId=10 | Success; 3 phần tử, sắp xếp tháng giảm dần |
| UTCID198 | PayrollService.GetMyPayslipsAsync | B | NV 11 chưa có payslip | employeeId=11 | Success; list rỗng |
| UTCID199 | PayrollService.GetMyPayslipsAsync | A | — | employeeId=0 | Failure (Validation) hoặc list rỗng; không exception |
| UTCID200 | PayrollService.GetMyPayslipAsync | N | NV 10 có payslip 2026-10 Approved | employeeId=10, periodMonth="2026-10" | Success Detail: items, NetPay |
| UTCID201 | PayrollService.GetMyPayslipAsync | B | — | employeeId=10, periodMonth="2026-13" | Failure `PeriodMonthInvalid` |
| UTCID202 | PayrollService.GetMyPayslipAsync | A | Payslip 2026-10 chỉ có của NV 11 | employeeId=10, periodMonth="2026-10" | Failure `NotFound` (không lộ lương người khác) (#6) |
| UTCID203 | PayrollService.GetMyPayslipAsync | A | Payroll 2026-11 còn Draft | employeeId=10, periodMonth="2026-11" | Failure `NotFound` (NV không xem được lương chưa duyệt; xác nhận lại với spec) |

---

## 4. OutputService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID204 | OutputService.GetGridAsync | N | ICurrentUser = TeamLeader tổ 3; OrderLine 100 đã chia tổ 3 (Allotted); Assignment: W1×OP10, W2×OP10; OutputRecord W1×OP10 = 20; Cap W1×OP10 = 50; mock PieceRateService trả rate 1.500đ; Now trước EditDeadline | workDate=2026-11-22, shiftCode="CA1", orderLineId=100, sewingTeamId=null | Success grid 2 dòng; W1: Reported=20, Cap=50; CanEdit=true |
| UTCID205 | OutputService.GetGridAsync | N | ICurrentUser = Manager | workDate=2026-11-22, shiftCode="CA1", orderLineId=100, sewingTeamId=3 | Success grid của tổ 3 |
| UTCID206 | OutputService.GetGridAsync | B | W3×OP20 không có assignment/record nhưng Cap=10 | như UTCID trên | Có dòng W3×OP20 (union keys, cap > 0) |
| UTCID207 | OutputService.GetGridAsync | B | W4×OP20 chỉ có Cap=0 | như trên | Không có dòng W4×OP20 |
| UTCID208 | OutputService.GetGridAsync | B | TeamLeader; IClock.Now = EditDeadline (đúng mốc) | workDate=2026-11-22 | CanEdit=true (`<=`) |
| UTCID209 | OutputService.GetGridAsync | B | TeamLeader; IClock.Now = EditDeadline + 1 giây | workDate=2026-11-22 | CanEdit=false |
| UTCID210 | OutputService.GetGridAsync | B | Manager; Now sau EditDeadline | sewingTeamId=3 | CanEdit=true |
| UTCID211 | OutputService.GetGridAsync | A | — | shiftCode="XYZ" | Failure (InvalidShift) |
| UTCID212 | OutputService.GetGridAsync | A | Không có OrderLine 999 | orderLineId=999 | Failure (OrderLine NotFound) |
| UTCID213 | OutputService.GetGridAsync | A | OrderLine 101 chưa chia cho tổ 3 | orderLineId=101 | Failure (NotAllotted) |
| UTCID214 | OutputService.GetGridAsync | A | ICurrentUser = Manager | sewingTeamId=null | Failure (Team required) |
| UTCID215 | OutputService.GetGridAsync | A | OutputRecord có OperationId=777 không còn trong dictionary operations | orderLineId=100 | Bỏ qua dòng hoặc Failure rõ ràng. ⚠ `KeyNotFoundException` tại `operations[k.OperationId]` (#7) |
| UTCID216 | OutputService.GetDayGridAsync | N | W1×OP10: CA1=10, CA2=15 | workDate=2026-11-22, orderLineId=100 | Success; W1 Quantity=25; CanEdit=false; Shift=AllDay |
| UTCID217 | OutputService.GetDayGridAsync | B | Có assignment nhưng 0 record | như trên | Dòng Quantity=0 |
| UTCID218 | OutputService.GetDayGridAsync | B | CA1 rate 1.500, CA2 rate 1.700 (khác rate) | như trên | UnitRate hiển thị cần xác định rõ (rate đại diện / bình quân). ⚠ Hiện tại `FirstOrDefault` (#5) |
| UTCID219 | OutputService.GetDayGridAsync | A | OrderLine 101 không Allotted cho tổ 3 | orderLineId=101 | Failure (NotAllotted) |
| UTCID220 | OutputService.SaveAsync | N | TeamLeader tổ 3; OP10 Active cùng product; W1, W2 thuộc tổ 3; Cap 50; rate APPROVED; mock IAppLock acquire OK | dto {WorkDate=2026-11-22, Shift="CA1", OrderLineId=100, Rows=[(OP10,W1,10),(OP10,W2,12)]} | Success {Inserted=2, Updated=0, Deleted=0}; AppLock acquire/release; Commit; Audit log |
| UTCID221 | OutputService.SaveAsync | N | Record W1×OP10 CA1 = 10, RowVersion=v1 | Rows=[(OP10,W1,15,RowVer=v1)] | Success {Updated=1}; Quantity=15 |
| UTCID222 | OutputService.SaveAsync | N | Record W1×OP10 = 10 | Rows=[(OP10,W1,0,v1)] | Success {Deleted=1}; record bị xóa |
| UTCID223 | OutputService.SaveAsync | N | Cap W1×OP10=50; W5 cùng tổ còn trần dư 10; mock OutputCapTransfer.ApplyAsync Success | Rows=[(OP10,W1,55)], TransferExcess=true | Success + warning chuyển trần; ApplyAsync gọi với excess=5 |
| UTCID224 | OutputService.SaveAsync | B | Config MaxQty=1000 | Quantity=1000 | Success |
| UTCID225 | OutputService.SaveAsync | B | MaxQty=1000 | Quantity=1001 | Failure (Validation) |
| UTCID226 | OutputService.SaveAsync | B | Chưa có record W1×OP10 | Quantity=0 | Success {Inserted=0} (skip) |
| UTCID227 | OutputService.SaveAsync | B | — | Quantity=-1 | Failure (Validation) |
| UTCID228 | OutputService.SaveAsync | B | Cap=50; ca khác đã báo 30 (others) | Quantity=20 (30+20 = cap) | Success |
| UTCID229 | OutputService.SaveAsync | B | Cap=50; others=30 | Quantity=21 | Failure (CapExceeded) (cộng dồn mọi ca, #9) |
| UTCID230 | OutputService.SaveAsync | B | — | Rows=[] (0 phần tử) | Success 0/0/0 hoặc Failure Validation; không mở transaction |
| UTCID231 | OutputService.SaveAsync | B | — | WorkDate=2026-11-23 (Today+1) | Failure (FutureDate) |
| UTCID232 | OutputService.SaveAsync | B | Đơn ReceivedDate=2026-11-10 | WorkDate=2026-11-10 | Success |
| UTCID233 | OutputService.SaveAsync | B | Đơn ReceivedDate=2026-11-10 | WorkDate=2026-11-09 | Failure (BeforeOrderStart) |
| UTCID234 | OutputService.SaveAsync | B | TeamLeader; Now = EditDeadline | Rows sửa record cũ | Success (`<=`) |
| UTCID235 | OutputService.SaveAsync | B | TeamLeader; Now = EditDeadline + 1 giây | Rows sửa record cũ | Failure (EditWindowClosed) |
| UTCID236 | OutputService.SaveAsync | B | Manager; quá hạn | Rows sửa, Reason="Bổ sung sót" | Success |
| UTCID237 | OutputService.SaveAsync | B | Manager; quá hạn | Rows sửa, Reason=null | Failure (ReasonRequired) |
| UTCID238 | OutputService.SaveAsync | B | Đã có record W1×OP10 và W2×OP10 | Rows chỉ gửi W1 (bỏ W2) | Record W2 **vẫn tồn tại** (omit ≠ delete); Deleted=0 (#1) |
| UTCID239 | OutputService.SaveAsync | A | — | Rows=[(OP10,W1,5),(OP10,W1,7)] (trùng key) | Failure (DuplicateRow) |
| UTCID240 | OutputService.SaveAsync | A | ProductionGuard: kỳ 2026-11 Locked | dto hợp lệ | Failure `PeriodLocked`; không SaveChanges |
| UTCID241 | OutputService.SaveAsync | A | Đơn đã đóng (Completed/Stopped) | dto hợp lệ | Failure (OrderClosed) |
| UTCID242 | OutputService.SaveAsync | A | PieceRate OP10 chưa APPROVED | Row mới (insert) | Failure `PieceRateNotApproved` |
| UTCID243 | OutputService.SaveAsync | A | PieceRate OP10 chưa APPROVED; record cũ đã tồn tại | Row update qty 10→12 | Success (update không re-check rate, GBR-11) |
| UTCID244 | OutputService.SaveAsync | A | OP30 Discontinued | Rows=[(OP30,W1,5)] | Failure (OperationInactive) |
| UTCID245 | OutputService.SaveAsync | A | OP40 thuộc product khác | Rows=[(OP40,W1,5)] | Failure (OperationNotInProduct) |
| UTCID246 | OutputService.SaveAsync | A | W9 thuộc tổ 5 | Rows=[(OP10,W9,5)] | Failure (WorkerNotInTeam) |
| UTCID247 | OutputService.SaveAsync | A | Record RowVersion=v2 | Row gửi RowVer=v1 | Failure (Concurrency) |
| UTCID248 | OutputService.SaveAsync | A | Không có trần dư; mock OutputCapTransfer.ApplyAsync Failure | Quantity vượt cap, TransferExcess=true | Failure; Rollback; không record nào được ghi (#3) |
| UTCID249 | OutputService.SaveAsync | A | Mock IAppLock acquire timeout | dto hợp lệ | Failure (Busy/LockTimeout); không SaveChanges |
| UTCID250 | OutputService.SaveAsync | A | Mock SaveChanges ném SqlException số 50052 (trigger cap) | dto hợp lệ | Failure (CapExceeded) đã map; Rollback (#2) |
| UTCID251 | OutputService.SaveAsync | A | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) |

---

## 5. MaterialIssueService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID252 | MaterialIssueService.GetPagedAsync | N | 4 phiếu cấp: tổ 3 (2 phiếu), tổ 5 (2 phiếu) | query {Page=1}, receivingTeamId=null (Thủ kho) | Success; 4 items |
| UTCID253 | MaterialIssueService.GetPagedAsync | N | Như trên | receivingTeamId=3, Status=Pending | Chỉ phiếu tổ 3 có Status Pending |
| UTCID254 | MaterialIssueService.GetPagedAsync | B | DB rỗng | query mặc định | Success; Items rỗng |
| UTCID255 | MaterialIssueService.GetPagedAsync | B | 4 phiếu | Page=3, PageSize=2 | Items rỗng; Total=4 |
| UTCID256 | MaterialIssueService.GetPagedAsync | A | Phiếu cấp không theo đơn (Order=null) hoặc IssuedBy bị xóa | query mặc định | Success; OrderCode/IssuedByName = null. ⚠ Lỗi navigation thiếu null-safe (#8) |
| UTCID257 | MaterialIssueService.GetByIdAsync | N | Phiếu 50 thuộc tổ 3 | issueId=50, receivingTeamId=3 | Success DTO đủ lines |
| UTCID258 | MaterialIssueService.GetByIdAsync | B | Phiếu 50 tổ 3 | issueId=50, receivingTeamId=null | Success (không check team) |
| UTCID259 | MaterialIssueService.GetByIdAsync | A | Phiếu 50 tổ 3 | issueId=50, receivingTeamId=5 | Failure `TeamMismatch` |
| UTCID260 | MaterialIssueService.GetByIdAsync | A | — | issueId=999 | Failure `NotFound` |
| UTCID261 | MaterialIssueService.CreateAsync | N | Đơn xin 30 Approved: NVL thô M1 = 100; tồn kho M1 = 500; mock CodeGenerator → "PX2611-001" | dto {RequestId=30, Lines=[M1, IssuedQty=100]}, issuedBy=7 | Success DTO Status=PendingReceipt; tồn kho chưa trừ |
| UTCID262 | MaterialIssueService.CreateAsync | N | BTP OrderLine 100, công đoạn đã phân công cho tổ 3 | dto cấp BTP cho tổ 3, Lines có OrderLineId=100 | Success |
| UTCID263 | MaterialIssueService.CreateAsync | B | Tồn M1 = 100 | IssuedQty=100 | Success |
| UTCID264 | MaterialIssueService.CreateAsync | B | Tồn M1 = 100 | IssuedQty=101 | Failure (InsufficientStock) |
| UTCID265 | MaterialIssueService.CreateAsync | B | — | Lines=[] | Failure (Validation) |
| UTCID266 | MaterialIssueService.CreateAsync | B | Đơn xin M1 = 100; tồn đủ | IssuedQty=120 | Success + cảnh báo vượt số xin (DEV-ALL-108, policy chỉ cảnh báo) (#9) |
| UTCID267 | MaterialIssueService.CreateAsync | B | — | IssuedQty=0 | Failure (Validation) |
| UTCID268 | MaterialIssueService.CreateAsync | A | — | IssuedQty=-5 | Failure (Validation) |
| UTCID269 | MaterialIssueService.CreateAsync | A | Đơn xin 31 Pending (chưa duyệt) | RequestId=31 | Failure (InvalidStatus) |
| UTCID270 | MaterialIssueService.CreateAsync | A | — | RequestId=999 | Failure `NotFound` |
| UTCID271 | MaterialIssueService.CreateAsync | A | BTP OrderLine 100 chưa phân công công đoạn cho tổ 3 | cấp BTP | Failure (OperationsNotAssigned) |
| UTCID272 | MaterialIssueService.CreateAsync | A | Phiếu BTP có lines không có OrderLineId | cấp BTP | Failure (Validation). ⚠ `lineIds` rỗng → pass check phân công (#5) |
| UTCID273 | MaterialIssueService.ConfirmAsync | N | Phiếu 50 Pending tổ 3, line M1 Issued=100; tồn 500; mock IStockService OK | issueId=50, dto Lines=[M1, Confirmed=100], teamId=3, employeeId=12 | Success; Status=Received; tồn −100; Request.MarkCompleted gọi |
| UTCID274 | MaterialIssueService.ConfirmAsync | N | Phiếu BTP; đơn Status=NEW, MaterialStatus=Ready | Confirmed = Issued | Success; CustomerOrder.StartProduction gọi → IN_PRODUCTION |
| UTCID275 | MaterialIssueService.ConfirmAsync | B | Issued=100 | Confirmed=99, DiscrepancyReason="Thiếu 1 cuộn" | Success; tồn −99 |
| UTCID276 | MaterialIssueService.ConfirmAsync | B | Issued=100 | Confirmed=99, Reason=null | Failure (ReasonRequired) |
| UTCID277 | MaterialIssueService.ConfirmAsync | B | Issued=100 | Confirmed=101 | Failure (ExceedsIssued) |
| UTCID278 | MaterialIssueService.ConfirmAsync | B | Phiếu BTP | Mọi dòng Confirmed=0 (+ reason) | Failure (nhận 0 không hợp lệ) hoặc không StartProduction. ⚠ Hiện tại Received và StartProduction (#1) |
| UTCID279 | MaterialIssueService.ConfirmAsync | B | Phiếu BTP; đơn NEW, MaterialStatus=Awaiting | Confirmed=Issued | Success; StartProduction **không** gọi, đơn vẫn NEW. Cần thông báo/log rõ (#4) |
| UTCID280 | MaterialIssueService.ConfirmAsync | A | — | Confirmed=-1 | Failure (Validation). ⚠ Service không check (#2) |
| UTCID281 | MaterialIssueService.ConfirmAsync | A | Phiếu 50 đã Received | issueId=50 | Failure `InvalidStatus` |
| UTCID282 | MaterialIssueService.ConfirmAsync | A | Phiếu 50 thuộc tổ 3 | teamId=5 | Failure `TeamMismatch` |
| UTCID283 | MaterialIssueService.ConfirmAsync | A | Phiếu 50 có 2 lines | dto chỉ gửi 1 line | Failure (MissingLines) |
| UTCID284 | MaterialIssueService.ConfirmAsync | A | Tồn M1 đã bị phiếu khác lấy còn 50; mock Stock Failure | Confirmed=100 | Failure (InsufficientStock); Rollback; phiếu vẫn Pending (#3) |
| UTCID285 | MaterialIssueService.ConfirmAsync | A | — | issueId=999 | Failure `NotFound` |
| UTCID286 | MaterialIssueService.RejectAsync | N | Phiếu 50 Pending tổ 3; mock Notifier | issueId=50, Reason="Sai mã NVL", teamId=3 | Success; Status=Rejected; Request vẫn Approved; Notifier được **await** |
| UTCID287 | MaterialIssueService.RejectAsync | B | Phiếu 50 Pending | Reason="" | Failure (Validation) |
| UTCID288 | MaterialIssueService.RejectAsync | A | Phiếu 50 Pending | Reason=null | Failure (Validation). ⚠ NRE tại `.Trim()` (#7) |
| UTCID289 | MaterialIssueService.RejectAsync | A | Phiếu 50 tổ 3 | teamId=5 | Failure `TeamMismatch` |
| UTCID290 | MaterialIssueService.RejectAsync | A | Phiếu 50 Received | Reason="x" | Failure `InvalidStatus` |
| UTCID291 | MaterialIssueService.RejectAsync | A | Mock Notifier ném exception | Reason="x" | Lỗi được log/xử lý, không mất im lặng. ⚠ `NotifyEmployees` không await (#7) |

---

## 6. ProductSpecService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID292 | ProductSpecService.GetAsync | N | Product 1: 3 component, 2 variant (Đỏ, Xanh), 4 norm Active | productId=1 | Success Spec DTO đủ component/variant/norm |
| UTCID293 | ProductSpecService.GetAsync | B | Product 2 chưa có spec | productId=2 | Success; các list rỗng |
| UTCID294 | ProductSpecService.GetAsync | B | Product 1 có 1 norm Discontinued, component tương ứng Discontinued | productId=1 | Norm Discontinued không trả; component Discontinued cũng ẩn hoặc gắn cờ nhất quán. ⚠ Có thể lệch UI (#4) |
| UTCID295 | ProductSpecService.GetAsync | A | — | productId=999 | Failure `NotFound` |
| UTCID296 | ProductSpecService.SaveAsync | N | Product 1 có spec; mock Audit | dto spec hợp lệ (component, marker group, norm cho Đỏ/Xanh) | Success Spec mới; transaction Commit; Audit log |
| UTCID297 | ProductSpecService.SaveAsync | N | Component Id=11 "Thân trước" | dto component {Id=11, Name="Thân trước trái"} | Success; component 11 đổi tên (không tạo mới) |
| UTCID298 | ProductSpecService.SaveAsync | B | Component "Thân Trước" (không gửi Id) | dto component {Id=null, Name="thân trước"} | Khớp component cũ (case-insensitive), không tạo bản ghi mới (#2) |
| UTCID299 | ProductSpecService.SaveAsync | B | Component 12 đã dùng trong OperationComponent | dto bỏ component 12 | Component 12 Status=Discontinued (soft), không xóa cứng |
| UTCID300 | ProductSpecService.SaveAsync | B | — | PiecesPerGarment=1 | Success |
| UTCID301 | ProductSpecService.SaveAsync | B | — | PiecesPerGarment=0 | Failure (Validation). ⚠ Service không check (#5) |
| UTCID302 | ProductSpecService.SaveAsync | B | Component chưa dùng ở đâu | Components=[] (0 phần tử) | Success; component cũ bị xóa (hoặc Failure nếu spec bắt buộc ≥1) |
| UTCID303 | ProductSpecService.SaveAsync | A | Variant đơn: Đỏ, Xanh | dto có variant "Vàng" (mới) | Failure (VariantNotInOrder); không ghi |
| UTCID304 | ProductSpecService.SaveAsync | A | Mock CheckAsync trả Failure (trùng tên component) | dto lỗi | Failure; BeginTransaction **không** gọi |
| UTCID305 | ProductSpecService.SaveAsync | A | Mock GetAsync sau ghi trả Failure | dto hợp lệ | Trả Failure. ⚠ Hiện tại không kiểm `spec.IsSuccess`, có thể Success với Data null (#6) |
| UTCID306 | ProductSpecService.SaveAsync | A | — | productId=999 | Failure `NotFound` |
| UTCID307 | ProductSpecService.CheckAsync | N | Product 1 | dto hợp lệ | Success; SaveChanges **không** gọi |
| UTCID308 | ProductSpecService.CheckAsync | B | — | 2 component tên "Tay" và "tay " | Failure (DuplicateComponent) |
| UTCID309 | ProductSpecService.CheckAsync | A | — | productId=999 | Failure `NotFound` |
| UTCID310 | ProductSpecService.CheckAsync | A | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) |
| UTCID311 | ProductSpecService.WriteAsync | N | Đã gọi CheckAsync Success; đã BeginTransaction bên ngoài | productId=1, dto hợp lệ | `Result<int>` Success; dữ liệu ghi đúng sau khi Commit bên ngoài |
| UTCID312 | ProductSpecService.WriteAsync | B | Không có norm nào | dto norm rỗng | Success; không ghi norm |
| UTCID313 | ProductSpecService.WriteAsync | A | Không gọi Check; dto norm tham chiếu component không tồn tại | productId=1 | Failure (Validation). ⚠ `KeyNotFoundException` / `.Single()` ném lỗi (#1) |
| UTCID314 | ProductSpecService.WriteAsync | A | Operation link tham chiếu component tên cũ không còn trong `componentByName` | productId=1 | Bỏ qua/Failure an toàn. ⚠ `KeyNotFoundException` trong RemoveStaleOperationLinksAsync (#7) |

---

## 7. WorkOrderService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID315 | WorkOrderService.GetPagedAsync | N | 4 lệnh cắt: tổ cắt 1 (3 lệnh), tổ cắt 2 (1 lệnh) | query {Page=1}, teamId=1 | Success; 3 items, có cut/handed qty |
| UTCID316 | WorkOrderService.GetPagedAsync | B | DB rỗng | query mặc định | Success; Items rỗng |
| UTCID317 | WorkOrderService.GetPagedAsync | B | 2 lệnh Status raw "IN_PROGRESS" | Status="InProgress" (Pascal) | Trả 2 lệnh (chuẩn hóa ToCode như đơn hàng). ⚠ So raw string → rỗng (#8) |
| UTCID318 | WorkOrderService.GetPagedAsync | A | — | Status="Foo" | Failure (Validation) hoặc list rỗng theo quy ước chung; không exception |
| UTCID319 | WorkOrderService.GetByIdAsync | N | Lệnh 60 Open, lệnh thường, tổ cắt 1; mock WorkOrderFabricChecker trả 2 check | workOrderId=60, teamId=1 | Success; FabricChecks có 2 phần tử |
| UTCID320 | WorkOrderService.GetByIdAsync | B | Lệnh 61 Open, là lệnh bù (replacement) | workOrderId=61 | FabricChecks rỗng (chỉ tính lệnh thường) |
| UTCID321 | WorkOrderService.GetByIdAsync | B | Lệnh 62 đang cắt (InProgress), kho thiếu vải | workOrderId=62 | Theo design không có FabricChecks. Ghi nhận rủi ro mất cảnh báo (#5) |
| UTCID322 | WorkOrderService.GetByIdAsync | A | Lệnh 60 tổ cắt 1 | teamId=2 | Failure `TeamMismatch` |
| UTCID323 | WorkOrderService.GetByIdAsync | A | — | workOrderId=999 | Failure `NotFound` |
| UTCID324 | WorkOrderService.CheckFabricAsync | N | Đơn 1: OrderLine 100 cần vải F1 120m; tồn F1 200m | dto {OrderId=1, Team=1, Lines=[100, Qty=300]} | Success; F1 đủ (Shortage=0) |
| UTCID325 | WorkOrderService.CheckFabricAsync | B | Tồn F1 = đúng nhu cầu 120m | như trên | F1 đủ, Shortage=0 |
| UTCID326 | WorkOrderService.CheckFabricAsync | B | Tồn F1 = 119,99m | như trên | F1 thiếu 0,01m |
| UTCID327 | WorkOrderService.CheckFabricAsync | B | — | Lines=[] | Success; danh sách rỗng |
| UTCID328 | WorkOrderService.CheckFabricAsync | A | OrderLine 555 thuộc đơn khác | Lines=[100, 555] | Failure (Validation: line không thuộc đơn). ⚠ Hiện tại bỏ im lặng, trả list ngắn hơn (#1) |
| UTCID329 | WorkOrderService.CheckFabricAsync | A | — | OrderId=999 | Failure `NotFound` |
| UTCID330 | WorkOrderService.GetFabricRequirementsAsync | N | Lệnh 60 cần F1, F2 | workOrderId=60 | Success; 2 dòng nhu cầu vải đúng định mức |
| UTCID331 | WorkOrderService.GetFabricRequirementsAsync | B | Lệnh 67: product chưa có định mức vải | workOrderId=67 | Success; list rỗng (không exception chia/null) |
| UTCID332 | WorkOrderService.GetFabricRequirementsAsync | A | — | workOrderId=999 | Failure `NotFound` |
| UTCID333 | WorkOrderService.GetFabricNeedsAsync | N | Lệnh 60: F1 NeedMeters=50 | workOrderId=60 | Success; có F1 = 50 |
| UTCID334 | WorkOrderService.GetFabricNeedsAsync | B | F2 NeedMeters=0 (đã đủ) | workOrderId=60 | F2 không có trong kết quả (chỉ NeedMeters > 0) |
| UTCID335 | WorkOrderService.GetFabricNeedsAsync | A | — | workOrderId=999 | Failure `NotFound` |
| UTCID336 | WorkOrderService.GetMaterialAllowanceAsync | N | Lệnh 60 có định mức NVL | workOrderId=60 | Success; allowance đúng PlannedQty × định mức |
| UTCID337 | WorkOrderService.GetMaterialAllowanceAsync | B | Lệnh 68 PlannedQty=1, định mức 0,25m | workOrderId=68 | Allowance = 0,25m (không làm tròn về 0) |
| UTCID338 | WorkOrderService.GetMaterialAllowanceAsync | A | — | workOrderId=999 | Failure `NotFound` |
| UTCID339 | WorkOrderService.CreateAsync | N | Đơn 1 IN_PRODUCTION, ReceivedDate=2026-11-10; tổ 1 là tổ cắt; đủ vải; mock CodeGenerator → "LC2611-001"; mock Notifier | dto {OrderId=1, CuttingTeamId=1, DueDate=2026-11-25, Lines=[100, PlannedQty=300]} | Success DTO Status=Open, Code="LC2611-001"; Notifier gọi |
| UTCID340 | WorkOrderService.CreateAsync | B | Như trên | DueDate=2026-11-22 (= Today) | Success |
| UTCID341 | WorkOrderService.CreateAsync | B | Như trên | DueDate=2026-11-21 (< Today) | Failure (DueDate invalid) |
| UTCID342 | WorkOrderService.CreateAsync | B | Đơn ReceivedDate=2026-11-22 (= Today) | DueDate=2026-11-22 (= ReceivedDate) | Failure (`DueDate <= ReceivedDate`). Xác nhận spec biên (#3) |
| UTCID343 | WorkOrderService.CreateAsync | B | Đơn ReceivedDate=2026-11-22 | DueDate=2026-11-23 | Success |
| UTCID344 | WorkOrderService.CreateAsync | B | — | PlannedQty=0 | Failure (Validation). ⚠ Service không check (#4) |
| UTCID345 | WorkOrderService.CreateAsync | B | — | Lines=[] | Failure (Validation) (#4) |
| UTCID346 | WorkOrderService.CreateAsync | A | — | PlannedQty=-10 | Failure (Validation) |
| UTCID347 | WorkOrderService.CreateAsync | A | Tồn F1 thiếu so với nhu cầu | dto hợp lệ (lệnh thường) | Failure (FabricShortage); không tạo lệnh |
| UTCID348 | WorkOrderService.CreateAsync | A | Đơn 1 Stopped/Completed | dto hợp lệ | Failure (OrderClosed) |
| UTCID349 | WorkOrderService.CreateAsync | A | Tổ 3 là tổ may | CuttingTeamId=3 | Failure (NotCuttingTeam) |
| UTCID350 | WorkOrderService.CreateAsync | A | OrderLine 555 không thuộc đơn 1 | Lines=[555] | Failure (Validation) |
| UTCID351 | WorkOrderService.CreateAsync | A | Mock tạo lệnh trả `created.IsSuccess=false` | dto hợp lệ | Trả Failure. ⚠ Vẫn gọi `GetByIdAsync(created.Data)` (#7) |
| UTCID352 | WorkOrderService.GetOpenReplacementRequestsAsync | N | Request bù 40 Approved chưa có WO; request 41 Approved đã có WO; request 42 Pending | — | Success; chỉ trả request 40 |
| UTCID353 | WorkOrderService.GetOpenReplacementRequestsAsync | B | Không có request nào thỏa | — | Success; list rỗng |
| UTCID354 | WorkOrderService.GetOpenReplacementRequestsAsync | A | Request 43 Approved nhưng OrderLine gốc bị xóa (FK lệch) | — | Success; request 43 bị bỏ qua hoặc trả field null; không exception |
| UTCID355 | WorkOrderService.CreateReplacementAsync | N | Request bù 40 Approved; đủ vải | dto {RequestId=40, CuttingTeamId=1, DueDate=2026-11-24} | Success; lệnh bù Open, IsReplacement=true |
| UTCID356 | WorkOrderService.CreateReplacementAsync | B | Request 40; kho thiếu vải | dto hợp lệ | Success + warning thiếu vải (lệnh bù không chặn, theo design) (#2) |
| UTCID357 | WorkOrderService.CreateReplacementAsync | A | Request 42 Pending | RequestId=42 | Failure (InvalidStatus) |
| UTCID358 | WorkOrderService.CreateReplacementAsync | A | Request 41 đã có WO | RequestId=41 | Failure (AlreadyHasWorkOrder) |
| UTCID359 | WorkOrderService.CreateReplacementAsync | A | — | RequestId=999 | Failure `NotFound` |
| UTCID360 | WorkOrderService.UpdateAsync | N | Lệnh 60 Open, cut qty=0 | workOrderId=60, DueDate=2026-11-28, PlannedQty=320 | Success; dữ liệu cập nhật |
| UTCID361 | WorkOrderService.UpdateAsync | B | Lệnh 60 Open | DueDate=2026-11-22 (= Today) | Success |
| UTCID362 | WorkOrderService.UpdateAsync | A | Lệnh 63 đã cắt 50 | workOrderId=63 | Failure `InvalidStatus` (đã cắt) |
| UTCID363 | WorkOrderService.UpdateAsync | A | — | workOrderId=999 | Failure `NotFound` |
| UTCID364 | WorkOrderService.CancelAsync | N | Lệnh 60 Open, chưa cắt, chưa cấp NVL | workOrderId=60 | Success; Status=Cancelled |
| UTCID365 | WorkOrderService.CancelAsync | B | Lệnh 64 Open, cut qty=0 nhưng đã có phiếu cấp vải Received | workOrderId=64 | Failure, hoặc Success kèm hoàn kho rõ ràng; không orphan stock (#6) |
| UTCID366 | WorkOrderService.CancelAsync | A | Lệnh 63 đã cắt 50 | workOrderId=63 | Failure `InvalidStatus` |
| UTCID367 | WorkOrderService.CancelAsync | A | Lệnh 65 đã Cancelled | workOrderId=65 | Failure `InvalidStatus` (hoặc idempotent theo spec) |
| UTCID368 | WorkOrderService.CancelAsync | A | — | workOrderId=999 | Failure `NotFound` |
| UTCID369 | WorkOrderService.CloseShortAsync | N | Lệnh 63 InProgress, Planned=300, Cut=250 | workOrderId=63, Reason="Hết vải" | Success; Status=ClosedShort; BTP 250 giữ nguyên |
| UTCID370 | WorkOrderService.CloseShortAsync | B | Lệnh 60 Cut=0 | workOrderId=60 | Failure (phải dùng Cancel) |
| UTCID371 | WorkOrderService.CloseShortAsync | B | Lệnh 66 Cut=Planned=300 | workOrderId=66 | Failure (đã đủ, không phải đóng thiếu) |
| UTCID372 | WorkOrderService.CloseShortAsync | A | Lệnh 65 Cancelled | workOrderId=65 | Failure `InvalidStatus` |
| UTCID373 | WorkOrderService.CloseShortAsync | A | — | workOrderId=999 | Failure `NotFound` |

---

## 8. DefectService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID374 | DefectService.ListForTeamAsync | N | ICurrentUser = TeamLeader tổ 3; 5 phiếu tổ 3, 4 phiếu tổ 5 | query {Page=1} | Success; 5 phiếu tổ 3 |
| UTCID375 | DefectService.ListForTeamAsync | N | ICurrentUser = Manager | query {Page=1} | Success; 9 phiếu (mọi tổ) |
| UTCID376 | DefectService.ListForTeamAsync | B | Phiếu tổ 3 ngày 2026-11-20 và 2026-11-21 | FromDate=ToDate=2026-11-20 | Chỉ phiếu ngày 2026-11-20 |
| UTCID377 | DefectService.ListForTeamAsync | B | — | Keyword="" | Không filter keyword |
| UTCID378 | DefectService.ListForTeamAsync | A | TeamLeader tổ 3 | query {TeamId=5} | Chỉ dữ liệu tổ 3 hoặc Failure (Forbidden); **không** trả phiếu tổ 5 |
| UTCID379 | DefectService.ListForTeamAsync | A | — | FromDate=2026-11-22, ToDate=2026-11-01 (From > To) | Failure (Validation) hoặc list rỗng |
| UTCID380 | DefectService.ListMineAsync | N | Công nhân W1 có 3 phiếu Active | workerId=W1 | Success; 3 phiếu |
| UTCID381 | DefectService.ListMineAsync | B | W1 có 1 phiếu Cancelled | workerId=W1 | Phiếu Cancelled bị ẩn |
| UTCID382 | DefectService.ListMineAsync | B | W1 có MaxPageSize + 1 phiếu | workerId=W1 | Có paging/Total để xem hết. ⚠ Hiện tại `Take(MaxPageSize)` cắt cụt, không báo total (#4) |
| UTCID383 | DefectService.ListMineAsync | A | — | workerId=0 | Failure (Validation). ⚠ Không validate (#4) |
| UTCID384 | DefectService.CreateAsync | N | TeamLeader tổ 3; OrderLine 100 Allotted tổ 3; OP10 Active; W1 thuộc tổ 3; kỳ mở; mock Notifier | dto {Type=Rework, OrderLineId=100, OperationId=OP10, WorkerId=W1, Quantity=5, Shift="CA1", Date=2026-11-22} | Success; phiếu tạo; Notifier được **await** |
| UTCID385 | DefectService.CreateAsync | N | Như trên; PieceRate OP10 APPROVED | Type=Scrap, Quantity=3 | Success; PieceRate bị khóa cho phiếu |
| UTCID386 | DefectService.CreateAsync | B | Như trên | Quantity=1 | Success |
| UTCID387 | DefectService.CreateAsync | B | — | Quantity=0 | Failure (Validation) |
| UTCID388 | DefectService.CreateAsync | B | Hàng trên chuyền = 50 | Type=Scrap, Quantity=1.000.000 | Failure (vượt MaxQty / vượt hàng trên chuyền). ⚠ Không có MaxQty (#1) |
| UTCID389 | DefectService.CreateAsync | B | W1×OP10 đã báo sản lượng cả ngày = 20 (CA1 10 + CA2 10) | Type=OutputError, Quantity=20 | Success (= sản lượng đã báo) (#5) |
| UTCID390 | DefectService.CreateAsync | B | Như trên | Type=OutputError, Quantity=21 | Failure (ExceedsReported) |
| UTCID391 | DefectService.CreateAsync | A | — | Quantity=-1 | Failure (Validation) |
| UTCID392 | DefectService.CreateAsync | A | ICurrentUser = Manager (không có tổ) | dto hợp lệ | Failure (chỉ TeamLeader được ghi) |
| UTCID393 | DefectService.CreateAsync | A | ICurrentUser = Admin | dto hợp lệ | Failure (Forbidden), xác nhận role map (#2) |
| UTCID394 | DefectService.CreateAsync | A | ProductionGuard: kỳ Locked | dto hợp lệ | Failure `PeriodLocked` |
| UTCID395 | DefectService.CreateAsync | A | PieceRate OP10 chưa APPROVED | Type=Scrap | Failure `PieceRateNotApproved` |
| UTCID396 | DefectService.CreateAsync | A | OrderLine 101 chưa chia cho tổ 3 | OrderLineId=101 | Failure (NotAllotted). ⚠ Hiện tại không check (#7) |
| UTCID397 | DefectService.CreateAsync | A | ICurrentUser teamId=999 (team không tồn tại) | dto hợp lệ | Failure (Team NotFound). ⚠ `FirstAsync` ném `InvalidOperationException` (#8) |
| UTCID398 | DefectService.CreateAsync | A | Mock Notifier ném exception | dto hợp lệ | Lỗi được xử lý/log; không mất im lặng trong transaction. ⚠ Không await (#6) |
| UTCID399 | DefectService.CreateAsync | A | OP99 không tồn tại | OperationId=OP99 | Failure `NotFound` |
| UTCID400 | DefectService.UpdateAsync | N | Phiếu 70 Rework qty=5, tạo bởi TeamLeader tổ 3, trong cửa sổ sửa | defectId=70, Quantity=4 | Success; qty=4; Audit log |
| UTCID401 | DefectService.UpdateAsync | N | Phiếu 70 Rework; PieceRate APPROVED | Type=Scrap | Success; rate được khóa; CheckIncreaseWithinOnLine được gọi (#3) |
| UTCID402 | DefectService.UpdateAsync | N | Phiếu 71 Scrap đang khóa rate | Type=Rework | Success; rate được release |
| UTCID403 | DefectService.UpdateAsync | B | Now = hạn cuối cửa sổ sửa | defectId=70, Quantity=4 | Success (biên `<=`) |
| UTCID404 | DefectService.UpdateAsync | B | Now = hạn cuối + 1 giây, TeamLeader | defectId=70 | Failure (NotMutable) |
| UTCID405 | DefectService.UpdateAsync | B | — | Quantity=0 | Failure (Validation) |
| UTCID406 | DefectService.UpdateAsync | A | ICurrentUser = Manager | defectId=70, Reason=null | Failure (ReasonRequired) |
| UTCID407 | DefectService.UpdateAsync | A | Phiếu 70 Rework; rate OP10 chưa APPROVED | Type=Scrap | Failure `PieceRateNotApproved` |
| UTCID408 | DefectService.UpdateAsync | A | Phiếu 72 Cancelled | defectId=72 | Failure (NotMutable/InvalidStatus) |
| UTCID409 | DefectService.UpdateAsync | A | — | defectId=999 | Failure `NotFound` |
| UTCID410 | DefectService.CancelAsync | N | Phiếu 70 trong cửa sổ | defectId=70, Reason="Ghi nhầm" | Success; Status=Cancelled; rate (nếu Scrap) release |
| UTCID411 | DefectService.CancelAsync | B | Phiếu 70 | Reason="" | Failure (ReasonRequired) |
| UTCID412 | DefectService.CancelAsync | A | Phiếu 70 | Reason=null | Failure (ReasonRequired), không NRE |
| UTCID413 | DefectService.CancelAsync | A | Phiếu 70 ngoài cửa sổ, TeamLeader | Reason="x" | Failure (NotMutable) |
| UTCID414 | DefectService.CancelAsync | A | Phiếu 72 đã Cancelled | Reason="x" | Failure (InvalidStatus) |
| UTCID415 | DefectService.CancelAsync | A | — | defectId=999 | Failure `NotFound` |

---

## 9. CustomerOrderService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID416 | CustomerOrderService.ListAsync | N | 5 đơn: 2 NEW, 2 IN_PRODUCTION, 1 CANCELLED | query {Status="InProduction", Page=1} | Success; 2 đơn |
| UTCID417 | CustomerOrderService.ListAsync | B | 5 đơn | Status=null, Page=1, PageSize=2 | Items=2, Total=5 |
| UTCID418 | CustomerOrderService.ListAsync | A | — | Status="Foo" | Failure (Validation 400) |
| UTCID419 | CustomerOrderService.ListOverviewAsync | N | 5 đơn có tiến độ | query {Page=1} | Success; overview gồm % tiến độ từng đơn |
| UTCID420 | CustomerOrderService.ListOverviewAsync | B | DB rỗng | query mặc định | Success; rỗng |
| UTCID421 | CustomerOrderService.ListOverviewAsync | A | — | Status="Foo" | Failure (Validation 400) |
| UTCID422 | CustomerOrderService.SuggestCodeAsync | N | Today=2026-11-22; đã có DH2611-001…DH2611-007 | — | "DH2611-008" |
| UTCID423 | CustomerOrderService.SuggestCodeAsync | B | Chưa có đơn tháng 11/2026 (chỉ có DH2610-xxx) | — | "DH2611-001" |
| UTCID424 | CustomerOrderService.SuggestCodeAsync | B | Đã có DH2611-999 | — | "DH2611-1000" (hoặc theo quy tắc độ dài; không tràn/lỗi format) |
| UTCID425 | CustomerOrderService.SuggestCodeAsync | B | Có mã "DH2611-ABC" (suffix không phải số) và DH2611-003 | — | "DH2611-004" (bỏ qua suffix không phải số) |
| UTCID426 | CustomerOrderService.SuggestCodeAsync | A | 2 request gọi đồng thời, cùng DB state DH2611-007 | 2 lần gọi | Cả 2 trả "DH2611-008"; CreateAsync thứ 2 phải Failure `CodeDuplicate`, không trùng dữ liệu (#1) |
| UTCID427 | CustomerOrderService.GetAsync | N | Đơn 1 có 3 dòng + 2 bản ghi audit | orderId=1 | Success; Lines=3; History=2 |
| UTCID428 | CustomerOrderService.GetAsync | B | Đơn 7 NEW, 0 dòng, chưa có audit | orderId=7 | Success; Lines rỗng; History rỗng |
| UTCID429 | CustomerOrderService.GetAsync | A | — | orderId=999 | Failure `NotFound` |
| UTCID430 | CustomerOrderService.GetOverviewAsync | N | Đơn 1 | orderId=1 | Success overview (cut/sewn/dispatched) |
| UTCID431 | CustomerOrderService.GetOverviewAsync | B | Đơn 7 chưa cắt/may/xuất | orderId=7 | Success; tiến độ 0% (không chia cho 0 khi ordered=0) |
| UTCID432 | CustomerOrderService.GetOverviewAsync | A | — | orderId=999 | Failure `NotFound` |
| UTCID433 | CustomerOrderService.CreateAsync | N | Product P1 có sẵn; mock Notifier, Audit | dto {OrderCode="DH2611-008", CustomerId=1, ReceivedDate=2026-11-22, DueDate=2026-12-20, Lines=[P1, Đỏ, 500]} | Success; OrderId > 0; Status=NEW; Audit log |
| UTCID434 | CustomerOrderService.CreateAsync | N | — | dto có StagedProducts=[{TempId=-1, Code="AO-MOI"}], Lines=[ProductId=-1, 200] | Success; product AO-MOI được tạo; line bind đúng ProductId thật |
| UTCID435 | CustomerOrderService.CreateAsync | B | — | Lines có 1 dòng | Success |
| UTCID436 | CustomerOrderService.CreateAsync | B | — | Lines=[] | Failure (Validation) |
| UTCID437 | CustomerOrderService.CreateAsync | B | — | DueDate = ReceivedDate = 2026-11-22 | Theo spec (Success hoặc Failure); ghi rõ kết quả |
| UTCID438 | CustomerOrderService.CreateAsync | B | — | OrderedQty=0 trên 1 dòng | Failure (Validation) |
| UTCID439 | CustomerOrderService.CreateAsync | A | — | DueDate=2026-11-21 < ReceivedDate=2026-11-22 | Failure (Validation). ⚠ Service không check tường minh (#6) |
| UTCID440 | CustomerOrderService.CreateAsync | A | Đã có DH2611-008 | OrderCode="DH2611-008" | Failure `CodeDuplicate` |
| UTCID441 | CustomerOrderService.CreateAsync | A | Product "AO-01" đã có trong DB | StagedProducts Code="AO-01" | Failure (ProductCodeDuplicate); không tạo đơn |
| UTCID442 | CustomerOrderService.CreateAsync | A | — | 2 StagedProducts cùng Code="AO-MOI" | Failure (DuplicateInPayload) |
| UTCID443 | CustomerOrderService.CreateAsync | A | — | StagedProducts TempId=-2 không dòng nào dùng | Failure hoặc warning rõ. ⚠ Hiện tại bỏ qua im lặng (#3) |
| UTCID444 | CustomerOrderService.CreateAsync | A | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) |
| UTCID445 | CustomerOrderService.UpdateAsync | N | Đơn 1 NEW, RowVer=v1 | orderId=1, dto đổi DueDate, thêm dòng, RowVer=v1 | Success; full edit được áp dụng |
| UTCID446 | CustomerOrderService.UpdateAsync | N | Đơn 2 IN_PRODUCTION (LimitedEdit) | sửa ghi chú/DueDate | Success; chỉ trường được phép thay đổi |
| UTCID447 | CustomerOrderService.UpdateAsync | B | Đơn 2 IN_PRODUCTION, dòng 200 đã cắt 300 | OrderedQty=300 (= đã cắt) | Success |
| UTCID448 | CustomerOrderService.UpdateAsync | B | Như trên | OrderedQty=299 (< đã cắt) | Failure (BelowProduced) (#2) |
| UTCID449 | CustomerOrderService.UpdateAsync | A | Đơn 2 IN_PRODUCTION, dòng 200 đã sản xuất | xóa dòng 200 | Failure (LineInUse) |
| UTCID450 | CustomerOrderService.UpdateAsync | A | Đơn 1 RowVer=v2 | RowVer=v1 | Failure (Concurrency) |
| UTCID451 | CustomerOrderService.UpdateAsync | A | Đơn 3 CANCELLED | dto hợp lệ | Failure `InvalidStatus` |
| UTCID452 | CustomerOrderService.UpdateAsync | A | — | orderId=999 | Failure `NotFound` |
| UTCID453 | CustomerOrderService.CancelAsync | N | Đơn 1 NEW, RowVer=v1; mock OrderCloseCascade | orderId=1, Reason="Khách hủy", RowVer=v1 | Success; Status=CANCELLED; cascade gọi 1 lần |
| UTCID454 | CustomerOrderService.CancelAsync | B | Đơn 1 NEW | Reason="" | Failure (ReasonRequired) |
| UTCID455 | CustomerOrderService.CancelAsync | A | Đơn 2 IN_PRODUCTION | Reason="x" | Failure `InvalidStatus` (message gợi ý dùng Stop) (#4) |
| UTCID456 | CustomerOrderService.CancelAsync | A | Đơn 1 RowVer=v2 | RowVer=v1 | Failure (Concurrency) |
| UTCID457 | CustomerOrderService.CancelAsync | A | — | orderId=999 | Failure `NotFound` |
| UTCID458 | CustomerOrderService.CancelAsync | A | CancellationToken đã cancel | orderId=1 | `OperationCanceledException`. ⚠ `GetByIdAsync` không truyền ct (#7) |
| UTCID459 | CustomerOrderService.StopAsync | N | Đơn 2 IN_PRODUCTION, đã xuất 40% | orderId=2, Reason="Khách dừng" | Success; Status=STOPPED; cascade gọi |
| UTCID460 | CustomerOrderService.StopAsync | B | Đơn 4 PARTIALLY_DELIVERED, chưa xuất đủ | Reason="x" | Success (xác nhận định nghĩa "đủ xuất") (#5) |
| UTCID461 | CustomerOrderService.StopAsync | B | Đơn 5 IN_PRODUCTION, 0 dòng (ordered=0) | Reason="x" | Failure, hoặc hành vi được định nghĩa rõ; không Stop bất thường (#5) |
| UTCID462 | CustomerOrderService.StopAsync | A | Đơn 6 đã xuất đủ 100% (FullyDispatched) | Reason="x" | Failure (FullyDispatched) |
| UTCID463 | CustomerOrderService.StopAsync | A | Đơn 3 CANCELLED | Reason="x" | Failure `InvalidStatus` |
| UTCID464 | CustomerOrderService.StopAsync | A | — | orderId=999 | Failure `NotFound` |
| UTCID465 | CustomerOrderService.StartProductionAsync | N | Đơn 1 NEW, MaterialStatus=Ready | orderId=1 | Success; Status=IN_PRODUCTION |
| UTCID466 | CustomerOrderService.StartProductionAsync | B | Đơn 1 NEW, MaterialStatus=Partial (khác Awaiting) | orderId=1 | Success |
| UTCID467 | CustomerOrderService.StartProductionAsync | A | Đơn 1 NEW, MaterialStatus=Awaiting | orderId=1 | Failure (MaterialAwaiting) |
| UTCID468 | CustomerOrderService.StartProductionAsync | A | Đơn 2 IN_PRODUCTION | orderId=2 | Failure `InvalidStatus` |
| UTCID469 | CustomerOrderService.StartProductionAsync | A | — | orderId=999 | Failure `NotFound` |

---

## 10. DailyWorkAllocationService

| Test Case ID | Function Name | Type (N/B/A) | Precondition & Mock Setup | Input Data | Expected Output / Exception |
|---|---|---|---|---|---|
| UTCID470 | DailyWorkAllocationService.GetBoardAsync | N | Tổ may 3; đơn 1; OrderLine 100 Allotted tổ 3; Assignment W1×OP10; Carry=20, DayGiven=30, DayOutput=25, Held=100, Output=60; rate APPROVED | teamId=3, workDate=2026-11-22, orderId=1, orderLineId=100 | Success; W1×OP10 Remaining = min(20+30−25, 100−60) = 25; có Unallocated, Rate |
| UTCID471 | DailyWorkAllocationService.GetBoardAsync | N | Đơn 1 có OrderLine 100, 101 Allotted, 102 không Allotted | orderLineId=null | Board gồm dòng 100 và 101; không có 102 |
| UTCID472 | DailyWorkAllocationService.GetBoardAsync | B | Carry+DayNet−DayOutput = -5 | như trên | Remaining = 0 (floor 0) |
| UTCID473 | DailyWorkAllocationService.GetBoardAsync | B | Đơn có OrderLine nhưng 0 assignment/position | orderLineId=100 | Success; rows rỗng |
| UTCID474 | DailyWorkAllocationService.GetBoardAsync | B | — | workDate=2026-11-21 (quá khứ) | Success board (chỉ xem); FE phải biết không Save được (#1) |
| UTCID475 | DailyWorkAllocationService.GetBoardAsync | B | Handover CONFIRMED có ConfirmedQty=null (data lỗi) | như trên | Remaining tính an toàn, không âm/sai lệch. ⚠ `?? 0` làm Held sai (#2) |
| UTCID476 | DailyWorkAllocationService.GetBoardAsync | A | — | teamId=999 | Failure (Team NotFound). ⚠ Hiện tại không validate (#1) |
| UTCID477 | DailyWorkAllocationService.GetBoardAsync | A | — | orderId=999 | Failure `NotFound`. ⚠ Không validate (#1) |
| UTCID478 | DailyWorkAllocationService.GetBoardAsync | A | Row có OperationId=777 không có trong dictionary `sequence` | như trên | Đẩy xuống cuối hoặc bỏ qua. ⚠ `KeyNotFoundException` khi sort (#7) |
| UTCID479 | DailyWorkAllocationService.SaveAsync | N | Tổ may 3; W1 thuộc tổ; line 100 Allotted, đơn mở; Unallocated OP10 = 50; rate APPROVED; mock IAppLock OK, Notifier | dto {WorkDate=2026-11-22, Items=[(W1,100,OP10,Qty=30)]} (đang 0 → 30), teamId=3 | Success {Added=1, Adjusted=0}; tạo WorkAllocation PENDING 30; AppLock theo line; Notifier await |
| UTCID480 | DailyWorkAllocationService.SaveAsync | N | W1×OP10 DayGiven=30, toàn PENDING | Qty=20, Reason="Đổi việc" | Success; PENDING giảm còn 20 (hạ pending trước) |
| UTCID481 | DailyWorkAllocationService.SaveAsync | N | W1×OP10 DayGiven=30 (10 PENDING + 20 CONFIRMED), Remaining=15 | Qty=15, Reason="Điều chuyển" | Success; hủy 10 PENDING + ADJUSTED −5 từ confirmed |
| UTCID482 | DailyWorkAllocationService.SaveAsync | B | Unallocated OP10 = 50, đang giao 0 | Qty=50 (delta = unallocated) | Success |
| UTCID483 | DailyWorkAllocationService.SaveAsync | B | Unallocated = 50 | Qty=51 | Failure (OverUnallocated) |
| UTCID484 | DailyWorkAllocationService.SaveAsync | B | W1×OP10 hiện 30 | Qty=30 (delta=0) | Success {Added=0, Adjusted=0}; không ghi DB |
| UTCID485 | DailyWorkAllocationService.SaveAsync | B | — | WorkDate=2026-11-21 (Today−1) | Failure (WorkDateMustBeToday) |
| UTCID486 | DailyWorkAllocationService.SaveAsync | B | — | WorkDate=2026-11-23 (Today+1) | Failure (WorkDateMustBeToday) |
| UTCID487 | DailyWorkAllocationService.SaveAsync | B | — | Items=[] | Success 0/0 hoặc Failure Validation; không lấy lock |
| UTCID488 | DailyWorkAllocationService.SaveAsync | B | DayGiven=10 toàn PENDING, Remaining=0 | Qty=0, Reason="x" | Success; chỉ hủy PENDING, không chạm confirmed (#4) |
| UTCID489 | DailyWorkAllocationService.SaveAsync | A | — | Qty=-1 | Failure (Validation). ⚠ Cần xác nhận ValidateAsync có chặn (#6) |
| UTCID490 | DailyWorkAllocationService.SaveAsync | A | — | Items=[(W1,100,OP10,10),(W1,100,OP10,20)] (trùng key) | Failure (DuplicateItem). ⚠ Nguy cơ delta kép (#6) |
| UTCID491 | DailyWorkAllocationService.SaveAsync | A | W1×OP10 hiện 30 | Qty=20, Reason=null | Failure (ReasonRequired) |
| UTCID492 | DailyWorkAllocationService.SaveAsync | A | DayGiven=30 (0 PENDING, 30 CONFIRMED), Remaining=5 | Qty=20 (cần hạ 10 từ confirmed > Remaining 5), Reason="x" | Failure (fromConfirmed > Remaining) |
| UTCID493 | DailyWorkAllocationService.SaveAsync | A | W9 thuộc tổ 5 | Items=[(W9,100,OP10,10)] | Failure (WorkerNotInTeam) |
| UTCID494 | DailyWorkAllocationService.SaveAsync | A | OrderLine 102 không Allotted cho tổ 3 | Items=[(W1,102,OP10,10)] | Failure (NotAllotted) |
| UTCID495 | DailyWorkAllocationService.SaveAsync | A | Đơn của line 100 đã Stopped | dto hợp lệ | Failure (OrderClosed) |
| UTCID496 | DailyWorkAllocationService.SaveAsync | A | Tổ 1 là tổ cắt | teamId=1 | Failure (NotSewingTeam) |
| UTCID497 | DailyWorkAllocationService.SaveAsync | A | — | teamId=999 | Failure (Team NotFound) |
| UTCID498 | DailyWorkAllocationService.SaveAsync | A | PieceRate OP10 chưa APPROVED | Qty=10 | Failure `PieceRateNotApproved` |
| UTCID499 | DailyWorkAllocationService.SaveAsync | A | PieceRate OP10 chưa APPROVED, W1 hiện 10 | Qty=0, Reason="x" | Success (Qty = 0 không cần rate) |
| UTCID500 | DailyWorkAllocationService.SaveAsync | A | Mock IAppLock timeout | dto hợp lệ | Failure (Busy); không SaveChanges |
| UTCID501 | DailyWorkAllocationService.SaveAsync | A | Mock Notifier ném exception | dto hợp lệ | Lỗi được log/báo; dữ liệu phân việc vẫn commit. ⚠ `NotifyWorkers` không await (#5) |

---

## Tổng hợp số lượng test case

| Service | Số method | N | B | A | Tổng |
|---|---:|---:|---:|---:|---:|
| OperationService | 7 | 13 | 33 | 24 | 70 |
| MaterialReceiptService | 7 | 12 | 24 | 28 | 64 |
| PayrollService | 13 | 15 | 26 | 28 | 69 |
| OutputService | 3 | 7 | 22 | 19 | 48 |
| MaterialIssueService | 5 | 8 | 14 | 18 | 40 |
| ProductSpecService | 4 | 5 | 9 | 9 | 23 |
| WorkOrderService | 12 | 12 | 22 | 25 | 59 |
| DefectService | 5 | 9 | 13 | 20 | 42 |
| CustomerOrderService | 10 | 12 | 17 | 25 | 54 |
| DailyWorkAllocationService | 2 | 5 | 11 | 16 | 32 |
| **Tổng** | **68** | **98** | **191** | **212** | **501** |

Có **55** case mang ký hiệu ⚠ (dự kiến FAIL trên code hiện tại). Đây là các case nên chạy đầu tiên để xác nhận bug.
