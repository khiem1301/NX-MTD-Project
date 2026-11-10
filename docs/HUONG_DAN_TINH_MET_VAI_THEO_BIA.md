# Cách hệ thống ước số mét vải và chọn bìa cho lệnh cắt

> DEV-ALL-85 (bìa cắt theo đơn), DEV-ALL-87 (định mức theo màu), DEV-ALL-88 (ghép bìa cho ít mét nhất). Cập nhật 10/11/2026.

Khi lập hoặc xem một lệnh cắt, hệ thống ước:
- **số mét vải cần** cho từng loại vải;
- **bìa nên dùng**: trải bìa nào, bao nhiêu lớp, sao cho tốn **ít mét nhất**.

Kết quả chỉ để **tham khảo**: không lưu vào lệnh, không chặn lưu lệnh (kể cả lệnh cắt bù). Thực tế có thể chênh lệch vì hao hụt đầu bàn, vải lỗi, hoặc tổ cắt trải theo cách khác.

---

## 1. Dữ liệu đầu vào

| Dữ liệu | Nhập ở đâu | Dùng để |
|---|---|---|
| Thành phần của mã hàng: chi tiết cắt (Panel) và phụ liệu (Trim), số chi tiết trên 1 áo | Mã hàng → Định mức → tab "Thành phần" | biết chi tiết nào đi qua tổ cắt; tay áo = 2 chi tiết/áo |
| Định mức NVL theo **màu áo**: chi tiết dùng vải nào | Mã hàng → Định mức → tab "Định mức NVL" | mọi size của một màu dùng chung định mức |
| **Nhóm bìa** của mã hàng: chi tiết nào cắt chung một bìa, vải theo từng màu áo | Mã hàng → Định mức → tab "Nhóm bìa" | vd nhóm 1 = thân, nhóm 2 = tay |
| **Bìa của đơn**: mã bìa, chiều dài (m), **số áo mỗi size trên 1 lớp** (tỉ lệ bìa), trạng thái | Đơn hàng → chi tiết → tab "Bìa cắt" | phía trên giác theo tỉ lệ size của đơn |
| Số cần cắt theo màu × size | dòng lệnh cắt | nhu cầu |
| Vải khả dụng | kho + phiếu cấp / trả vải | so đủ / thiếu |

**Quy tắc định mức:** mỗi chi tiết cắt chỉ khai **một vải** ở mỗi màu áo. Vải lót, mex, bo cũng là vải, nên phải khai thành chi tiết riêng (vd "Tay áo" vải chính, "Lót tay áo" vải lót, "Mex cổ" mex). Nhờ vậy biết chính xác mỗi chi tiết trải trên vải nào.

---

## 2. Bước 1: chia lệnh cắt thành các bài toán nhỏ

Một lần trải bàn chỉ dùng **một nhóm bìa** trên **một loại vải**. Hệ thống tách lệnh thành các bài toán theo khóa **(mã hàng, nhóm bìa, vải)** và giải từng bài.

**Cắt cả bộ:** mỗi dòng (màu × size × số lượng) được cộng vào **mọi nhóm bìa** có khai vải cho màu đó. Một bộ áo cần đủ thân và tay, nên dòng đó xuất hiện ở cả nhóm thân lẫn nhóm tay. Nhu cầu tính bằng **số áo**.

**Cắt lẻ / cắt bù một chi tiết:** tìm nhóm bìa chứa chi tiết đó và trải trên đúng vải của nó. Nhu cầu tính bằng **số miếng chi tiết**, và mỗi lớp bìa cho ra:

```
số miếng trên 1 lớp = số áo của size đó trên bìa × số chi tiết trên 1 áo
```

Bài cắt lẻ / cắt bù được giải **riêng** với bài cắt cả bộ, kể cả khi cùng vải, rồi cộng số mét.

---

## 3. Bước 2: công thức chung

### Ký hiệu (cho một bài toán)

| Ký hiệu | Ý nghĩa |
|---|---|
| j | các bìa đang dùng của đơn thuộc nhóm bìa đó |
| Dⱼ | chiều dài bìa j (m) |
| rⱼₛ | số áo size s trên 1 lớp của bìa j (0 nếu bìa không có size đó) |
| Nₛ | số cần cắt của size s (áo, hoặc miếng khi cắt lẻ) |
| f | hệ số: 1 khi cắt cả bộ; số chi tiết trên 1 áo khi cắt lẻ |
| **xⱼ** | **số lớp trải bìa j**: số nguyên ≥ 0, đây là thứ cần tìm |

### Công thức

```
Điều kiện (mọi size s):   Σⱼ rⱼₛ · f · xⱼ  ≥  Nₛ        cắt đủ, được phép dư
Mục tiêu:                 Σⱼ Dⱼ · xⱼ        →  nhỏ nhất  tổng mét ít nhất
Số dư:                    Σ (mọi áo / miếng cắt ra) − Σ Nₛ   tính cả size lệnh không cần
```

Không giới hạn số loại bìa được ghép. Bìa nào có xⱼ = 0 là không dùng.

### Hai phương án bằng mét thì chọn theo thứ tự

1. **Ít dư hơn**: ít hàng thừa.
2. **Ít loại bìa hơn**: tổ cắt ít phải thay bìa.
3. **Ít lớp hơn**: trải bàn nhanh hơn.
4. Theo mã bìa, để kết quả luôn giống nhau.

### Không tính được khi

- có size cần cắt mà **không bìa nào** trong nhóm có size đó (đơn chưa nhập bìa, hoặc bìa thiếu size);
- chi tiết cắt lẻ chưa nằm trong nhóm bìa nào;
- vải có trong định mức nhưng không nhóm bìa nào dùng;
- vải không tính theo mét.

---

## 4. Bước 3: giải bài toán

Số lớp phải là số nguyên, nên đây là bài toán **quy hoạch nguyên**. Nhóm có nhiều bìa thì số tổ hợp rất lớn, không thể thử hết bằng tay. Hệ thống giải theo thứ tự:

### 4.1 Thử nhanh: duyệt nhánh có cận dưới (`BranchAndBoundMarkerSolver`, tối đa 20.000 bước)

1. **Phương án ban đầu (tham lam):** lặp đến khi đủ hàng. Mỗi lần chọn bìa có **mét / số áo còn thiếu mà 1 lớp bù được** thấp nhất, rồi thêm 1 lớp. Cách này cho ngay một phương án đủ hàng, làm mốc so sánh.
2. **Duyệt nhánh:** thử số lớp của bìa thứ nhất (từ nhiều đến ít), với mỗi lựa chọn thì thử tiếp bìa thứ hai trên phần còn thiếu, và cứ thế. Bìa cuối không cần thử: lấy đúng số lớp vừa đủ bù phần còn thiếu.
3. **Cắt nhánh:** ở mỗi bước, ước số mét tối thiểu còn phải trải:
   ```
   cận dưới = mét đã dùng + max theo từng size còn thiếu của
              ( số còn thiếu × mét rẻ nhất cho 1 áo size đó trong các bìa chưa xét )
   ```
   Nếu cận dưới đã lớn hơn phương án tốt nhất đang có thì bỏ cả nhánh.
4. Duyệt hết trong giới hạn thì kết quả **chắc chắn tối ưu** và dùng luôn. Nhóm 2–4 bìa thường chỉ mất vài mili giây.

### 4.2 Nhóm lớn: Google.OrTools CP-SAT (`OrToolsMarkerPlanSolver`)

Khi lần thử nhanh chưa duyệt hết (thường từ 5 bìa trở lên), hệ thống đưa đúng công thức ở mục 3 cho bộ giải quy hoạch nguyên CP-SAT:
- chiều dài đổi sang **cm** vì CP-SAT chỉ nhận số nguyên (2,2 m thành 220);
- tối ưu **lần lượt** 4 mục tiêu; mỗi bước giữ nguyên kết quả bước trước: tổng mét → số dư → số loại bìa → số lớp;
- mỗi bước tối đa **1 giây**. Đo thử với nhóm 5–20 bìa: 0,1–0,2 giây, đều chứng minh được tối ưu.

### 4.3 Dự phòng

- CP-SAT lỗi (vd thiếu thư viện native) thì dùng duyệt nhánh đầy đủ (200.000 bước).
- Hết giờ mà chưa chứng minh được tối ưu thì vẫn dùng phương án tốt nhất tìm được, và ghi thêm **"(chưa chắc là ít mét nhất)"**.

---

## 5. Bước 4: so với vải đang có

```
Cần  = tổng số mét của mọi bài toán dùng cùng một vải
Có   = kho khả dụng của vải + vải tổ cắt đang giữ của đơn
       (tổ cắt đang giữ = đã nhận theo phiếu cấp − đã trả kho; không âm)
```

| Kết quả | Khi nào |
|---|---|
| **Đủ** | Có ≥ Cần |
| **Thiếu** | Có < Cần; báo thiếu bao nhiêu mét, kho bao nhiêu, tổ đang giữ bao nhiêu |
| **Chưa tính được** | một trong các trường hợp ở mục 3 |

---

## 6. Ví dụ 1: ghép bìa cho một nhóm

**Cần cắt:** S = 33, M = 100, L = 130 áo.

**Bìa của đơn trong nhóm thân:**

| Bìa | Tỉ lệ (áo / lớp) | Dài |
|---|---|---|
| A | 1S, 2L | 2,1 m |
| B | 3M, 1L | 2,8 m |
| C | 1S, 1M, 1L | 2,1 m |

**Công thức:** gọi a, b, c là số lớp của A, B, C.

```
S:  a + c        ≥  33
M:  3b + c       ≥ 100
L:  2a + b + c   ≥ 130
Nhỏ nhất:  2,1a + 2,8b + 2,1c
```

**Nếu chỉ dùng một bìa:** chỉ C có đủ cả S, M, L. Khi đó cần max(33, 100, 130) = 130 lớp = **273 m**, dư 97 áo S và 30 áo M (127 áo).

**Hệ thống ghép bìa:**

| Bìa | Số lớp | Mét | S | M | L |
|---|---|---|---|---|---|
| A (1S, 2L) | 48 | 100,8 | 48 | 0 | 96 |
| B (3M, 1L) | 33 | 92,4 | 0 | 99 | 33 |
| C (1S, 1M, 1L) | 1 | 2,1 | 1 | 1 | 1 |
| **Cộng** | **82** | **195,3** | 49 (dư 16) | 100 | 130 |

Kết quả ít hơn **77,7 m** (khoảng 28%) so với chỉ dùng C. Không có cách nào đủ hàng mà không dư: giải hệ cho đúng số cần ra số lớp âm.

Trên màn hình:

> V01 Vải gió (Đen): cần khoảng 195,3 m. Nhóm bìa 1: A (1S, 2L) × 48 lớp, B (3M, 1L) × 33 lớp, C (1S, 1M, 1L) × 1 lớp (dư 16)

---

## 7. Ví dụ 2: một lệnh cắt đầy đủ

**Mã hàng AO01:**
- Chi tiết cắt: Thân trước, Thân sau, Tay áo (2 tay/áo).
- Nhóm bìa 1 (thân) gồm Thân trước, Thân sau; nhóm bìa 2 (tay) gồm Tay áo.
- Màu Đen dùng vải V01-Đen; màu Xanh dùng vải V01-Xanh.

**Bìa của đơn:**

| Bìa | Nhóm | Tỉ lệ | Dài |
|---|---|---|---|
| 1A | 1 | 1S, 2M, 1L | 2,2 m |
| 1B | 1 | 2M | 1,0 m |
| 2A | 2 | 1S, 2M, 1L | 1,6 m |

**Lệnh cắt:** Đen S10, M20, L10; Xanh M10 (cắt cả bộ); cộng **cắt lẻ 7 tay áo Đen M**.

**Bước 1: chia bài toán**

| Bài toán | Nhu cầu |
|---|---|
| (AO01, nhóm 1, V01-Đen) | S10, M20, L10 áo |
| (AO01, nhóm 2, V01-Đen) | S10, M20, L10 áo |
| (AO01, nhóm 2, V01-Đen, cắt lẻ) | 7 miếng tay M |
| (AO01, nhóm 1, V01-Xanh) | M10 áo |
| (AO01, nhóm 2, V01-Xanh) | M10 áo |

**Bước 2–3: giải từng bài**

| Bài toán | Các lựa chọn | Hệ thống chọn | Mét | Dư |
|---|---|---|---|---|
| nhóm 1, Đen | 1A × 10 = 22 m | 1A × 10 | 22,0 | 0 |
| nhóm 2, Đen | 2A × 10 = 16 m | 2A × 10 | 16,0 | 0 |
| nhóm 2, Đen, cắt lẻ 7 tay M | 1 lớp 2A = 2 áo M × 2 tay = 4 miếng → cần ⌈7/4⌉ = 2 lớp | 2A × 2 | 3,2 | 1 miếng |
| nhóm 1, Xanh M10 | 1A × 5 = 11 m (dư 5S, 5L) hoặc **1B × 5 = 5 m** | 1B × 5 | 5,0 | 0 |
| nhóm 2, Xanh M10 | 2A × 5 = 8 m | 2A × 5 | 8,0 | 10 (5S, 5L) |

**Bước 4: so với vải đang có** (giả sử kho V01-Đen 30 m, tổ cắt đang giữ 5 m; kho V01-Xanh 20 m)

| Vải | Cần | Có | Kết quả |
|---|---|---|---|
| V01-Đen | 22 + 16 + 3,2 = **41,2 m** | 30 + 5 = 35 m | **Thiếu khoảng 6,2 m** |
| V01-Xanh | 5 + 8 = **13 m** | 20 m | **Đủ** |

Trên màn hình:

> ℹ️ **Số mét vải cần và bìa nên dùng**
> V01 Vải (Đen): cần khoảng 41,2 m. Nhóm bìa 1: 1A (1S, 2M, 1L) × 10 lớp; Nhóm bìa 2: 2A (1S, 2M, 1L) × 10 lớp; Cắt lẻ / cắt bù, nhóm bìa 2: 2A (1S, 2M, 1L) × 2 lớp (dư 1)
> V01 Vải (Xanh): cần khoảng 13 m. Nhóm bìa 1: 1B (2M) × 5 lớp; Nhóm bìa 2: 2A (1S, 2M, 1L) × 5 lớp (dư 10)
>
> ⚠️ **Không đủ vải cho lệnh cắt**
> V01 Vải (Đen): thiếu khoảng 6,2 m (cần khoảng 41,2 m, có 35 m: kho 30, tổ đang giữ 5).

Nếu đơn chưa nhập bìa cho nhóm 2, vải V01-Đen sẽ báo:

> ℹ️ **Chưa tính được vải**
> V01 Vải (Đen): chưa tính được vì còn thiếu: bìa của đơn cho nhóm 2 (size S, M, L).

---

## 8. Giới hạn hiện tại

- Không tính hao hụt đầu bàn hay vải lỗi: bìa dài bao nhiêu thì tính bấy nhiêu.
- Bài cắt lẻ / cắt bù được tính riêng, không ghép chung bàn với cắt cả bộ, nên số mét có thể cao hơn thực tế một chút.
- Khi các phương án bằng nhau ở mọi tiêu chí, CP-SAT (dùng cho nhóm lớn) có thể chọn một trong các phương án tương đương.
- Đơn chưa nhập bìa ở tab "Bìa cắt" thì lệnh cắt chỉ báo "chưa tính được".

---

## 9. Vị trí trong code

| Phần | File |
|---|---|
| Gom dữ liệu, chia bài toán, so với vải có | `be/Application/Services/Cutting/WorkOrderFabricChecker.cs` |
| Dựng bài toán (cả bộ / cắt lẻ) | `be/Application/Services/Cutting/MarkerFabricEstimator.cs` |
| Mô hình bài toán, số dư, thứ tự ưu tiên | `be/Application/Services/Cutting/MarkerPlanning.cs` |
| Interface bộ giải | `be/Application/Interfaces/Cutting/IMarkerPlanSolver.cs` |
| Bộ giải chính (Google.OrTools CP-SAT) | `be/Infrastructure/Optimization/OrToolsMarkerPlanSolver.cs` |
| Bộ giải nhanh / dự phòng (duyệt nhánh) | `be/Application/Services/Cutting/BranchAndBoundMarkerSolver.cs` |
| Thứ tự size | `be/Application/Common/SizeOrder.cs` |
| Hiển thị | `fe/src/features/cutting/components/FabricCheckAlerts.tsx`, `fe/src/features/cutting/utils/fabricPlan.ts` |
| Test (có ví dụ 1) | `tests/Application.Tests/Cutting/MarkerFabricEstimatorTests.cs` |
