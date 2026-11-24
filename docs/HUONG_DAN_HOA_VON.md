# Hướng dẫn số liệu: Hòa vốn & giá trị thặng dư

Màn **Tài chính → Hòa vốn & giá trị thặng dư** (`/admin/break-even`, chỉ Giám đốc / Admin xem được) cho biết từng mã hàng trong một tháng đã có lãi chưa, cần làm bao nhiêu sản phẩm mới đủ bù chi phí, và lãi / lỗ bao nhiêu.

File này giải thích từng con số trên màn: lấy từ đâu, ai nhập, tính thế nào.

---

## 1. Luồng số liệu một lần nhìn

```
Thủ kho xác nhận nhập kho thành phẩm ─┐
  (Bàn giao thành phẩm từ QA/QC)       │      ┌─ Doanh thu, SL hoàn thiện, đơn giá CMT ─┐
Đơn giá CMT trên dòng đơn hàng ───────┘──────┤                                          │
Kế toán nhập Chi phí tháng ───────────────────┼─ Biến phí (theo mã hàng) ───────────────┼──► Hòa vốn + thặng dư
  (định phí / biến phí)                       ├─ Định phí tháng ── chia theo 2 cách ────┘      theo từng mã hàng
Chấm công (giờ làm) + Sản lượng công nhân ────┘   (sản lượng | giờ công)
Bảng lương tháng đã duyệt ────────────────────────► chỉ là ĐIỀU KIỆN được tính (không cộng vào chi phí)
```

---

## 2. Điều kiện được bấm "Tính ngay"

Popup **Tính hòa vốn** kiểm 4 mục trước khi cho tính:

| Mục | Bắt buộc? | Lấy từ đâu | Thiếu thì làm gì |
|---|---|---|---|
| Bảng lương tháng | **Có** (MSG25) | Bảng lương của tháng có trạng thái **Đã duyệt** (bảng `Payroll`) | Kế toán lập, Giám đốc duyệt ở màn **Duyệt bảng lương** |
| Chi phí tháng | **Có** (MSG25) | Có ít nhất một dòng chi phí của tháng (bảng `MonthlyCost`) | Kế toán nhập ở màn **Chi phí tháng** |
| Thành phẩm nhập kho | **Có** | Có ít nhất một phiếu bàn giao thành phẩm được Thủ kho **xác nhận** trong tháng | QA/QC lập phiếu, Thủ kho xác nhận nhập kho |
| Giờ công | Không | Giờ làm từ chấm công + sản lượng công nhân của tháng | Chỉ cảnh báo: cách "theo giờ công" sẽ tạm chia theo sản lượng |

Tháng chưa có kỳ kế toán (chưa nhập chi phí, chưa lập lương) thì cả ba mục bắt buộc đều thiếu.

**Tháng đã khóa sổ:** duyệt bảng lương là hệ thống khóa luôn tháng đó, nên mọi tháng đủ điều kiện tính đều đã khóa. Hòa vốn vẫn tính lại được; popup chỉ báo "số liệu gốc không đổi, kết quả tính mới để tham khảo". Khóa sổ chặn sửa dữ liệu nguồn (chấm công, sản lượng, chi phí, lương), không chặn tính hòa vốn vì kết quả luôn lưu thêm vào lịch sử, không ghi đè.

---

## 3. Từng cột trên bảng kết quả

Mỗi mã hàng một dòng. Năm cột đầu giống nhau cho cả hai cách; hai nhóm cột sau là hai cách chia định phí.

### 3.1 SL hoàn thiện, Doanh thu, Đơn giá CMT

- **Nguồn:** view `vw_MonthlyRevenue`, đọc từ **phiếu bàn giao thành phẩm đã được Thủ kho xác nhận** (`FgHandover` trạng thái `CONFIRMED`).
- **Tháng tính theo** ngày Thủ kho xác nhận (`ConfirmedAt`), không theo ngày may xong hay ngày giao khách.
- **SL hoàn thiện** = tổng số lượng Thủ kho xác nhận nhận (`FgHandoverLine.ConfirmedQty`) của mã hàng trong tháng, cộng mọi đơn hàng.
- **Doanh thu** = Σ (số lượng xác nhận × **đơn giá CMT** của dòng đơn hàng `OrderLine.CmtUnitPrice`).
- **Đơn giá CMT** trên màn = Doanh thu ÷ SL hoàn thiện, tức đơn giá **bình quân** khi một mã hàng nằm ở nhiều đơn có giá khác nhau.

> Ví dụ: mã AO01 tháng 10 có 100 sp của đơn A (giá 100.000) và 20 sp của đơn B (giá 100.000) được nhập kho
> → SL hoàn thiện 120, doanh thu 12.000.000, đơn giá 100.000.

### 3.2 Biến phí

- **Nguồn:** màn **Chi phí tháng** của Kế toán (bảng `MonthlyCost`), các dòng loại **Biến phí** (`VARIABLE`) có gắn mã hàng.
- Biến phí của mã hàng = tổng các dòng biến phí gắn mã hàng đó trong tháng.
- **Biến phí / sp** = Biến phí ÷ SL hoàn thiện (dùng trong công thức hòa vốn).
- **Lưu ý:** lương công nhân **không** được cộng vào biến phí. Bảng lương đã duyệt chỉ là điều kiện để được tính. Muốn tính lương vào chi phí thì phải nhập thành một dòng chi phí tháng.

Nhóm chi phí trong file chi phí tháng: tiền ăn, xăng xe, điện nước, mặt bằng, sửa máy, vật tư máy, văn phòng phẩm, NVL mua thêm, phế phẩm, khác. Hệ thống không phân biệt nhóm khi tính hòa vốn, chỉ dùng **loại** (định phí / biến phí).

### 3.3 Định phí tháng

- **Nguồn:** cũng là màn **Chi phí tháng**, các dòng loại **Định phí** (`FIXED`), không gắn mã hàng.
- Định phí tháng = tổng mọi dòng định phí của tháng. Số này phải được **chia** cho các mã hàng có thành phẩm trong tháng; hai cách chia ở mục 4.

### 3.4 Các cột của mỗi cách chia

| Cột | Công thức |
|---|---|
| **Định phí** (được chia) | Định phí tháng × tỉ lệ của mã hàng (mục 4) |
| **SL hòa vốn** | Định phí được chia ÷ (Đơn giá CMT − Biến phí / sp) |
| **% đạt** | SL hoàn thiện ÷ SL hòa vốn × 100% |
| **Thặng dư** (giá trị thặng dư) | Doanh thu − Biến phí − Định phí được chia |
| **Rủi ro** | "Nguy cơ lỗ" khi thặng dư < 0; ghi rõ lỗ theo cách nào hoặc cả hai |

Các trường hợp đặc biệt:
- **Đơn giá CMT ≤ Biến phí / sp:** mỗi sản phẩm làm ra đã lỗ, nên không có điểm hòa vốn. SL hòa vốn và % đạt để trống ("—"); chi tiết ghi "Không đạt được".
- **Định phí được chia = 0:** SL hòa vốn = 0, % đạt = 100%.

---

## 4. Hai cách chia định phí

Mỗi lần bấm **Tính ngay**, hệ thống chạy **cả hai cách** và lưu cạnh nhau để so sánh. Hai cách chỉ khác nhau ở **tỉ lệ** chia định phí; doanh thu, biến phí và công thức còn lại giống hệt.

### 4.1 Theo sản lượng

```
Tỉ lệ mã hàng = SL hoàn thiện của mã hàng ÷ tổng SL hoàn thiện của mọi mã hàng trong tháng
```
- Đơn giản, dễ hiểu; phù hợp khi các mã hàng tốn công gần như nhau.
- Nhược điểm: mã hàng làm nhanh, số lượng lớn (vd quần short) gánh nhiều định phí dù tốn ít công.

### 4.2 Theo giờ công

```
Giờ công của mã hàng = Σ theo từng công nhân, từng ngày:
     giờ làm của công nhân trong ngày × (sản lượng mã hàng đó ÷ tổng sản lượng công nhân làm trong ngày)
Tỉ lệ mã hàng = Giờ công của mã hàng ÷ tổng giờ công
```
- **Giờ làm** lấy từ **chấm công** (`DailyTimesheet.WorkedHours`): giờ vào / giờ ra từ máy chấm công hoặc Kế toán chấm hộ.
- **Sản lượng** lấy từ **sản lượng công nhân** tổ trưởng nhập theo công đoạn (`OutputRecord`).
- Một công nhân làm 8 giờ, trong ngày nhập 60 sp mã AO01 và 40 sp mã QU02 → AO01 được tính 4,8 giờ, QU02 3,2 giờ.
- Mã hàng không quy ra được giờ nào (không có sản lượng công nhân, hoặc ngày đó công nhân không có giờ làm) thì **tạm dùng tỉ lệ sản lượng**. Sau đó hệ thống **chuẩn hóa** để tổng các tỉ lệ đúng bằng 100%, tức định phí chia ra cộng lại đúng bằng định phí tháng.
- Phù hợp hơn khi các mã hàng tốn công rất khác nhau (áo khoác nhiều công đoạn so với áo thun).

### 4.3 Đọc kết quả khi hai cách khác nhau

- **Cả hai cách đều lãi:** mã hàng an toàn.
- **Cả hai cách đều lỗ:** mã hàng lỗ thật; xem lại đơn giá CMT hoặc biến phí.
- **Một cách lãi, một cách lỗ:** kết quả phụ thuộc cách chia định phí. Thường "theo giờ công" phản ánh đúng hơn khi mã hàng tốn nhiều công.

---

## 5. Ví dụ đầy đủ

Tháng 10: định phí tháng 1.500.000. Hai mã hàng có thành phẩm nhập kho:

| | AO01 | QU02 |
|---|---|---|
| SL hoàn thiện | 100 | 50 |
| Doanh thu | 10.000.000 (đơn giá 100.000) | 4.000.000 (đơn giá 80.000) |
| Biến phí | 2.000.000 → 20.000 / sp | 4.500.000 → 90.000 / sp |

**Theo sản lượng** (tỉ lệ 100/150 = 66,67% và 50/150 = 33,33%):

| | AO01 | QU02 |
|---|---|---|
| Định phí được chia | 1.000.000 | 500.000 |
| SL hòa vốn | 1.000.000 ÷ (100.000 − 20.000) = **12,5** | — (80.000 ≤ 90.000, không đạt được) |
| % đạt | 100 ÷ 12,5 = **800%** | — |
| Thặng dư | 10.000.000 − 2.000.000 − 1.000.000 = **+7.000.000** | 4.000.000 − 4.500.000 − 500.000 = **−1.000.000** (nguy cơ lỗ) |

**Theo giờ công:** giả sử AO01 chiếm 60% giờ công, QU02 40% → định phí chia 900.000 / 600.000; AO01 thặng dư +7.100.000, QU02 −1.100.000.

---

## 6. Lịch sử và xuất Excel

- Mỗi lần **Tính ngay** lưu thêm một bộ kết quả (bảng `BreakEvenResult`, mỗi mã hàng 2 dòng: theo sản lượng và theo giờ công, cùng giờ tính). Lần trước **không bị ghi đè**.
- Mặc định màn chỉ hiện lần tính mới nhất. Bật **Xem lịch sử các lần tính** để xem cả các lần trước. Các lần tính trước ngày 24/11/2026 chỉ có một cách chia, cách còn lại hiện "—".
- **Xuất Excel** xuất đúng phần đang xem, mỗi mã hàng 2 dòng với cột "Cách phân bổ định phí".
- Nhập thêm chi phí, duyệt lại lương hay nhập kho thêm thành phẩm **không tự cập nhật** kết quả cũ: cần bấm Tính lại.

---

## 7. Câu hỏi hay gặp

**Vì sao mã hàng đã may xong mà không có trên bảng?**
Doanh thu chỉ tính khi thành phẩm được Thủ kho **xác nhận nhập kho** trong tháng. Hàng may xong nhưng chưa bàn giao, hoặc bàn giao chưa được xác nhận, chưa được tính.

**Vì sao % đạt để trống?**
Đơn giá CMT không cao hơn biến phí / sp: làm thêm sản phẩm chỉ lỗ thêm, không có điểm hòa vốn.

**Vì sao không bấm được Tính ngay?**
Thiếu một trong ba mục bắt buộc ở popup: bảng lương chưa duyệt, chưa nhập chi phí tháng, hoặc chưa có thành phẩm nhập kho.

**Biến phí có gồm lương công nhân không?**
Không. Lương chỉ là điều kiện được tính. Muốn đưa lương vào chi phí, Kế toán nhập thành dòng chi phí tháng.

**Số liệu này ai xem được?**
Chỉ Admin. Đơn giá CMT, hòa vốn và thặng dư là dữ liệu mật (GBR-02).

---

## 8. Tra cứu nhanh: số liệu và nơi nhập

| Số liệu | Bảng / view | Ai nhập | Màn nhập |
|---|---|---|---|
| Số lượng thành phẩm, ngày xác nhận | `FgHandover`, `FgHandoverLine` | QA/QC lập, Thủ kho xác nhận | Bàn giao thành phẩm |
| Đơn giá CMT | `OrderLine.CmtUnitPrice` | Admin / Quản đốc khi lập đơn | Đơn hàng |
| Doanh thu theo tháng | `vw_MonthlyRevenue` | (tự tính) | — |
| Định phí, biến phí | `MonthlyCost` (`FIXED` / `VARIABLE`) | Kế toán | Chi phí tháng |
| Bảng lương đã duyệt | `Payroll` | Kế toán lập, Admin duyệt | Bảng lương / Duyệt bảng lương |
| Giờ làm | `DailyTimesheet.WorkedHours` | Máy chấm công, Kế toán chấm hộ | Chấm công |
| Sản lượng công nhân | `OutputRecord` | Tổ trưởng may | Sản lượng |
| Kết quả hòa vốn | `BreakEvenResult` | (tự tính khi bấm Tính ngay) | Hòa vốn & giá trị thặng dư |

Mã nguồn tính toán: `be/Application/Services/Finance/FinanceService.cs` (`CalculateAsync`, `BuildEntitiesAsync`, `LabourShares`, `ComputeBreakEven`, `GetBreakEvenInputsAsync`).
