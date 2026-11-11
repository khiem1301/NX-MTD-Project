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

## 3. Bước 2: lập công thức cho từng bài toán

### Nói bằng lời

Với mỗi bìa, ta chọn **trải bao nhiêu lớp**. Mỗi lớp bìa j:
- **tốn** đúng chiều dài bìa, Dⱼ mét;
- **cho ra** rⱼₛ áo size s (theo tỉ lệ bìa).

Cần chọn số lớp sao cho **size nào cũng cắt đủ** và **tổng số mét nhỏ nhất**.

### Ký hiệu

| Ký hiệu | Ý nghĩa | Ví dụ 1 (mục 6) |
|---|---|---|
| j | một bìa đang dùng của đơn, thuộc nhóm bìa đang xét | A, B, C |
| Dⱼ | chiều dài bìa j (m) | D_A = 2,1 |
| rⱼₛ | số áo size s trên 1 lớp bìa j (0 nếu bìa không có size đó) | r_A,L = 2; r_A,M = 0 |
| Nₛ | số cần cắt của size s (áo; cắt lẻ thì là miếng) | N_L = 130 |
| f | số chi tiết trên 1 áo: cắt cả bộ thì f = 1; cắt lẻ tay áo thì f = 2 | 1 |
| **xⱼ** | **số lớp trải bìa j**: số nguyên ≥ 0, là **ẩn cần tìm** | a, b, c |

### Công thức

```
Tìm các số nguyên  x₁, x₂, …, xₙ ≥ 0

(1) Cắt đủ, với MỖI size s cần cắt:
        r₁ₛ·f·x₁ + r₂ₛ·f·x₂ + … + rₙₛ·f·xₙ  ≥  Nₛ
        └──────── số áo size s cắt ra ───────┘    └ số cần ┘

(2) Tổng mét nhỏ nhất:
        Mét = D₁·x₁ + D₂·x₂ + … + Dₙ·xₙ   →  nhỏ nhất

(3) Số dư của phương án (để so sánh khi bằng mét):
        Dư = (tổng số áo / miếng cắt ra, tính cả size lệnh không cần) − (tổng số cần)
```

Cách đọc:
- (1) có **một dòng cho mỗi size**. Vế trái là số áo size s cắt ra: lấy số áo size s trên 1 lớp nhân số lớp, rồi cộng qua mọi bìa. Dấu **≥** nghĩa là được cắt dư, không được thiếu.
- (2) là tổng mét của tất cả các lớp đã trải.
- Không giới hạn số loại bìa được ghép. Bìa nào có xⱼ = 0 là không dùng.

Ví dụ: bìa A (1S, 2L) trải 48 lớp cho 48 áo S và 96 áo L, tốn 48 × 2,1 = 100,8 m.

### Hai phương án bằng mét thì chọn theo thứ tự

1. **Ít dư hơn**: ít hàng thừa.
2. **Ít loại bìa hơn**: tổ cắt ít phải thay bìa.
3. **Ít lớp hơn**: trải bàn nhanh hơn.
4. Theo mã bìa, để kết quả luôn giống nhau.

### Không tính được khi

- có size cần cắt mà **không bìa nào** trong nhóm có size đó (đơn chưa nhập bìa, hoặc bìa thiếu size): điều kiện (1) của size đó không bao giờ đủ;
- chi tiết cắt lẻ chưa nằm trong nhóm bìa nào;
- vải có trong định mức nhưng không nhóm bìa nào dùng;
- vải không tính theo mét.

---

## 4. Bước 3: giải bài toán

### 4.1 Vì sao cần bộ giải

Số lớp phải là **số nguyên**, nên không giải được bằng phép chia đơn giản. Đây là bài toán **quy hoạch nguyên**. Nhóm 2–3 bìa còn tính tay được (xem mục 6), nhưng nhóm nhiều bìa có rất nhiều tổ hợp số lớp. Hệ thống giải theo sơ đồ:

```
Bài toán ──► (A) Duyệt nhánh nhanh, tối đa 20.000 bước
                 ├─ duyệt hết  ──► chắc chắn ít mét nhất ──► DÙNG
                 └─ chưa hết (thường từ 5 bìa trở lên)
                        ▼
             (B) Google.OrTools CP-SAT, mỗi bước tối đa 1 giây
                 ├─ có kết quả ──► so với (A), lấy phương án tốt hơn ──► DÙNG
                 └─ lỗi / không ra kết quả
                        ▼
             (C) Duyệt nhánh đầy đủ, tối đa 200.000 bước ──► DÙNG
```

Kết quả không chứng minh được là ít mét nhất (hết giờ, hết số bước) vẫn được dùng, nhưng có ghi thêm **"(chưa chắc là ít mét nhất)"**.

### 4.2 Duyệt nhánh có cận dưới (`BranchAndBoundMarkerSolver`)

Các bìa được xếp theo mã. Ở mỗi bước, "còn thiếu" của size s là Nₛ trừ số áo size s đã cắt ra.

**Bước 3a: tìm phương án mốc (tham lam).** Lặp lại đến khi đủ hàng: chọn bìa có

```
giá 1 áo của bìa j = Dⱼ / (số áo còn thiếu mà 1 lớp bìa j bù được)
```

nhỏ nhất, thêm 1 lớp bìa đó. Kết quả là một phương án đủ hàng nhưng chưa chắc ít mét nhất. Nó dùng làm **mốc**: phương án tìm sau phải tốt hơn mốc mới được giữ.

**Bước 3b: thử lần lượt số lớp từng bìa.**
- Bìa thứ nhất: thử số lớp từ **nhiều nhất còn có ích** xuống 0:
  ```
  số lớp nhiều nhất còn có ích = max theo size ⌈ còn thiếu của size s / (rⱼₛ · f) ⌉
  ```
  (trải thêm nữa chỉ ra hàng dư).
- Với mỗi lựa chọn, tính lại phần còn thiếu, rồi thử bìa thứ hai, cứ thế.
- **Bìa cuối không thử**: lấy đúng số lớp vừa đủ bù phần còn thiếu.

**Bước 3c: cắt nhánh bằng cận dưới.** Trước khi thử tiếp, ước số mét **ít nhất** mà nhánh này còn có thể đạt:

```
cận dưới = mét đã dùng + max theo size còn thiếu ( số còn thiếu × giá 1 áo rẻ nhất của size đó trong các bìa chưa xét )
                                                                 └─ giá 1 áo = Dⱼ / (rⱼₛ · f) ─┘
```

Nếu **cận dưới > số mét của mốc** thì nhánh này không thể tốt hơn mốc: bỏ cả nhánh, khỏi thử tiếp.

**Bước 3d: cập nhật mốc.** Khi một nhánh đủ hàng, so phương án đó với mốc theo thứ tự ở mục 3 (mét → dư → số loại bìa → số lớp → mã bìa). Tốt hơn thì nó thành mốc mới.

Duyệt hết mà không vượt giới hạn số bước thì mốc cuối cùng **chắc chắn ít mét nhất**. Nhóm 2–4 bìa thường chỉ mất vài mili giây.

**Minh họa** (nhóm 1, màu Xanh ở ví dụ 2): cần M = 10. Bìa 1A (2M, 2,2 m) có giá 1 áo M = 2,2 / 2 = 1,1 m; bìa 1B (2M, 1,0 m) có giá 1 áo M = 1,0 / 2 = 0,5 m.

- 3a: 1B luôn rẻ hơn, nên mốc = 1B × 5 lớp = **5 m**.
- 3b–3c: thử số lớp 1A từ ⌈10/2⌉ = 5 xuống 0. Bìa còn lại chỉ có 1B, giá 0,5 m/áo M.

| Số lớp 1A | Mét đã dùng | M còn thiếu | Cận dưới | Xử lý |
|---|---|---|---|---|
| 5 | 11,0 | 0 | | đủ hàng, 11 m > 5 m: không thay mốc |
| 4 | 8,8 | 2 | 8,8 + 2 × 0,5 = 9,8 | > 5: bỏ nhánh |
| 3 | 6,6 | 4 | 8,6 | > 5: bỏ nhánh |
| 2 | 4,4 | 6 | 7,4 | > 5: bỏ nhánh |
| 1 | 2,2 | 8 | 6,2 | > 5: bỏ nhánh |
| 0 | 0 | 10 | 0 + 10 × 0,5 = 5,0 | không lớn hơn 5: thử tiếp, 1B (bìa cuối) = ⌈10/2⌉ = 5 lớp = 5 m, bằng mốc |

Kết quả: **1B × 5 lớp = 5 m**, chắc chắn ít mét nhất.

### 4.3 Nhóm nhiều bìa: Google.OrTools CP-SAT (`OrToolsMarkerPlanSolver`)

Hệ thống đưa đúng công thức (1), (2), (3) ở mục 3 cho bộ giải quy hoạch nguyên CP-SAT:
- chiều dài đổi sang **cm** vì CP-SAT chỉ nhận số nguyên (2,2 m thành 220);
- tối ưu **lần lượt** 4 mục tiêu, bước sau giữ nguyên kết quả bước trước: **tổng mét → số dư → số loại bìa → số lớp**;
- mỗi bước tối đa **1 giây**. Đo thử với nhóm 5–20 bìa: 0,1–0,2 giây, đều chứng minh được tối ưu.

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

### Đề bài

**Cần cắt:** S = 33, M = 100, L = 130 áo (cắt cả bộ, f = 1). Tổng cần: 33 + 100 + 130 = **263 áo**.

**Bìa của đơn trong nhóm thân:**

| Bìa | Tỉ lệ (áo / lớp) | Áo / lớp | Dài |
|---|---|---|---|
| A | 1S, 2L | 3 | 2,1 m |
| B | 3M, 1L | 4 | 2,8 m |
| C | 1S, 1M, 1L | 3 | 2,1 m |

### Lập công thức

Gọi a, b, c là số lớp của A, B, C (số nguyên ≥ 0).

```
(1) S:  1a + 0b + 1c  ≥  33
    M:  0a + 3b + 1c  ≥ 100
    L:  2a + 1b + 1c  ≥ 130

(2) Mét = 2,1a + 2,8b + 2,1c  →  nhỏ nhất
```

### Lời giải

**Bước 1: nếu chỉ dùng một bìa.** Chỉ C có đủ cả S, M, L. Size cần nhiều lớp nhất là L: c = max(33, 100, 130) = 130 lớp, tức 130 × 2,1 = **273 m**. Dư 97 áo S và 30 áo M (**127 áo**).

**Bước 2: ít mét tương đương ít dư.** Tính số mét cho 1 áo của mỗi bìa = chiều dài bìa / tổng số áo trên 1 lớp (cột "Áo / lớp"):

| Bìa | Tổng áo trên 1 lớp | Mét cho 1 áo |
|---|---|---|
| A | 1S + 2L = 3 áo | 2,1 / 3 = 0,7 m |
| B | 3M + 1L = 4 áo | 2,8 / 4 = 0,7 m |
| C | 1S + 1M + 1L = 3 áo | 2,1 / 3 = 0,7 m |

Cả 3 bìa đều tốn **0,7 m cho 1 áo**, dù áo đó là size nào. Do đó:

```
Mét = 0,7 × (tổng áo cắt ra) = 0,7 × (263 + Dư)
```

Muốn ít mét thì phải ít dư.

**Bước 3: có cách nào không dư không?** Cho cả 3 điều kiện đúng bằng:

```
a + c = 33          →  a = 33 − c
3b + c = 100        →  b = (100 − c) / 3
2a + b + c = 130    →  2(33 − c) + (100 − c)/3 + c = 130  →  c = −23
```

c âm, nên **không thể cắt mà không dư**.

**Bước 4: dư ít nhất là bao nhiêu?** Nhân điều kiện L với 9, điều kiện M với 5 rồi cộng:

```
9 × (2a + b + c) + 5 × (3b + c)  ≥  9 × 130 + 5 × 100
                18a + 24b + 14c  ≥  1670
```

Tổng áo cắt ra là 3a + 4b + 3c (mỗi lớp A cho 3 áo, B cho 4 áo, C cho 3 áo, theo bảng ở bước 2). Nhân 6 được 18a + 24b + 18c, không nhỏ hơn 18a + 24b + 14c. Suy ra:

```
6 × (tổng áo) ≥ 1670  →  tổng áo ≥ 278,3  →  tổng áo ≥ 279
Dư ≥ 279 − 263 = 16;   Mét ≥ 0,7 × 279 = 195,3 m
```

Không có phương án nào dưới **195,3 m**.

**Bước 5: tìm phương án đạt 195,3 m.** Thử c nhỏ, rồi lấy b vừa đủ M, a vừa đủ L:

```
b = ⌈(100 − c) / 3⌉        (điều kiện M: 3b + c ≥ 100)
a = ⌈(130 − b − c) / 2⌉    (điều kiện L: 2a + b + c ≥ 130)
```

Hai công thức trên chỉ lo cho M và L, nên phải **kiểm tra lại điều kiện S**: số áo S cắt ra là a + c, phải ≥ 33. Ở mọi dòng dưới đây a + c đều ≥ 48, nên S luôn đủ, và phần S thừa chính là số dư.

| c | b | a | S cắt ra = a + c (cần ≥ 33) | Tổng áo = 3a + 4b + 3c | Mét |
|---|---|---|---|---|---|
| 0 | 34 | 48 | 48 | 280 | 196,0 |
| **1** | **33** | **48** | **49** | **279** | **195,3** |
| 2 | 33 | 48 | 50 | 282 | 197,4 |
| 3 | 33 | 47 | 50 | 282 | 197,4 |
| 4 | 32 | 47 | 51 | 281 | 196,7 |

c = 1 cho đúng 279 áo, bằng cận dưới ở bước 4, nên đây là **ít mét nhất**. Bỏ C (c = 0) chỉ đạt 280 áo: ghép 3 bìa là cần thiết.

**Kiểm tra:**

| Bìa | Số lớp | Mét | S | M | L |
|---|---|---|---|---|---|
| A (1S, 2L) | 48 | 48 × 2,1 = 100,8 | 48 | 0 | 96 |
| B (3M, 1L) | 33 | 33 × 2,8 = 92,4 | 0 | 99 | 33 |
| C (1S, 1M, 1L) | 1 | 1 × 2,1 = 2,1 | 1 | 1 | 1 |
| **Cộng** | **82** | **195,3** | 49 ≥ 33 (dư 16) | 100 ≥ 100 | 130 ≥ 130 |

**Đáp số:** 195,3 m, dư 16 áo S. Ít hơn **77,7 m** (khoảng 28%) so với chỉ dùng C.

Trên màn hình:

> V01 Vải gió (Đen): cần khoảng 195,3 m. Nhóm bìa 1: A (1S, 2L) × 48 lớp, B (3M, 1L) × 33 lớp, C (1S, 1M, 1L) × 1 lớp (dư 16)

---

## 7. Ví dụ 2: một lệnh cắt đầy đủ

### Đề bài

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

**Vải đang có:** kho V01-Đen 30 m, tổ cắt đang giữ 5 m; kho V01-Xanh 20 m.

### Bước 1: chia bài toán

| Bài toán | Nhu cầu | f |
|---|---|---|
| (I) nhóm 1, V01-Đen | S10, M20, L10 áo | 1 |
| (II) nhóm 2, V01-Đen | S10, M20, L10 áo | 1 |
| (III) nhóm 2, V01-Đen, cắt lẻ | 7 miếng tay M | 2 |
| (IV) nhóm 1, V01-Xanh | M10 áo | 1 |
| (V) nhóm 2, V01-Xanh | M10 áo | 1 |

### Bước 2–3: lập công thức và giải từng bài

Ký hiệu x = số lớp 1A, y = số lớp 1B, z = số lớp 2A.

**(I) Nhóm 1, Đen**

```
S:  x       ≥ 10
M:  2x + 2y ≥ 20
L:  x       ≥ 10
Mét = 2,2x + 1,0y → nhỏ nhất
```

S và L chỉ có ở 1A, nên x ≥ 10. Với x = 10 thì M = 20, đã đủ, nên y = 0.
→ **1A × 10 = 22,0 m**, dư 0.

**(II) Nhóm 2, Đen**

```
S: z ≥ 10     M: 2z ≥ 20     L: z ≥ 10
Mét = 1,6z → nhỏ nhất
```

→ z = 10, **2A × 10 = 16,0 m**, dư 0.

**(III) Nhóm 2, Đen, cắt lẻ 7 tay M** (f = 2: mỗi áo M trên bìa cho 2 miếng tay)

```
M:  2 × 2 × z ≥ 7   →   4z ≥ 7   →   z ≥ 1,75   →   z = 2
Mét = 1,6 × 2 = 3,2 m
```

→ **2A × 2 = 3,2 m**. 2 lớp cắt ra 8 miếng tay M (dư 1 miếng), cùng 2 áo S và 2 áo L trên bìa mà lệnh không cần (mỗi áo tính 1). Hệ thống ghi **dư 5** = 1 + 2 + 2.

**(IV) Nhóm 1, Xanh M10**

```
M:  2x + 2y ≥ 10
Mét = 2,2x + 1,0y → nhỏ nhất
```

Giá 1 áo M: 1A = 2,2 / 2 = 1,1 m, 1B = 1,0 / 2 = 0,5 m. Dùng toàn 1B: y = 10 / 2 = 5, x = 0 (cách tìm từng bước xem minh họa ở mục 4.2).
→ **1B × 5 = 5,0 m**, dư 0. Nếu dùng 1A × 5 thì tốn 11 m và dư 5S, 5L.

**(V) Nhóm 2, Xanh M10**

```
M:  2z ≥ 10  →  z = 5
Mét = 1,6 × 5 = 8,0 m
```

→ **2A × 5 = 8,0 m**. Bìa chỉ có 2A, nên cắt kèm 5S, 5L: dư 10.

**Tổng hợp:**

| Bài toán | Hệ thống chọn | Mét | Dư |
|---|---|---|---|
| (I) nhóm 1, Đen | 1A × 10 | 22,0 | 0 |
| (II) nhóm 2, Đen | 2A × 10 | 16,0 | 0 |
| (III) nhóm 2, Đen, cắt lẻ | 2A × 2 | 3,2 | 5 |
| (IV) nhóm 1, Xanh | 1B × 5 | 5,0 | 0 |
| (V) nhóm 2, Xanh | 2A × 5 | 8,0 | 10 |

### Bước 4: so với vải đang có

| Vải | Cần | Có | Kết quả |
|---|---|---|---|
| V01-Đen | (I) + (II) + (III) = 22 + 16 + 3,2 = **41,2 m** | 30 + 5 = 35 m | 35 < 41,2: **thiếu 41,2 − 35 = 6,2 m** |
| V01-Xanh | (IV) + (V) = 5 + 8 = **13 m** | 20 m | 20 ≥ 13: **đủ** |

Trên màn hình:

> ℹ️ **Số mét vải cần và bìa nên dùng**
> V01 Vải (Đen): cần khoảng 41,2 m. Nhóm bìa 1: 1A (1S, 2M, 1L) × 10 lớp; Nhóm bìa 2: 2A (1S, 2M, 1L) × 10 lớp; Cắt lẻ / cắt bù, nhóm bìa 2: 2A (1S, 2M, 1L) × 2 lớp (dư 5)
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
