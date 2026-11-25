# IT Batch 4 — Round 1: các ca Failed

Kết quả: **172 ca, 134 Passed, 38 Failed**. Log: `IT_B4_Round1.trx`.

| Ca | Thông báo lỗi |
|---|---|
| 006 | Expected ((int)res.StatusCode) to be 403 because body: , but found 415 (difference of 12). |
| 009 | Expected team.Code to be 200 because 409 {"success":false,"errorCode":"TEAM_ORDER_BUSY","message":"Tổ đang làm đơn DH-0002 (đã nhập kho TP 0% / đặt). Chỉ giao đơn khác khi đạt từ 90% hoặc đơn đã đóng… |
| 012 | Expected ((int)res.StatusCode) to be 403 because body: {"success":false,"errorCode":"AUTH_UNAUTHORIZED","message":"Bạn chưa đăng nhập hoặc phiên đã hết hạn."}, but found 401 (difference of -2). |
| 013 | Expected r.Code to be 200 because electrician /api/v1/common/order-lookups/orders: 401 {"success":false,"errorCode":"AUTH_UNAUTHORIZED","message":"Bạn chưa đăng nhập hoặc phiên đã hết hạn."}, but fou… |
| 017 | Expected code to match regex "^DH-\d+$", but "DH2611-001" does not match. |
| 025 | Expected B1Json.Str(map, "productCode") to be the same string, but they differ at index 7: ↓ (actual) "SPB4429D89" "SPB4429d89" ↑ (expected). |
| 034 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"COMMON_NOT_FOUND","message":"Khách hàng không tồn tại."}, but found 409. |
| 037 | Expected r.Code to be one of {200, 201} because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"lines[0].OrderedQty":["'Ordered Qty' must be between … |
| 038 | Expected api.Code to be one of {200, 201} because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"lines[0].CmtUnitPrice":["'Cmt Unit Price' must be b… |
| 040 | Expected r.Code to be one of {404, 400} because 409 {"success":false,"errorCode":"PRODUCT_NOT_ACTIVE","message":"Mã hàng #1017315 đã ngừng sản xuất."}, but found 409. |
| 055 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Vui lòng nhập lý do."}, but found 400 (difference of 200). |
| 065 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"items":[{"orderLineId":3,"orderId":1,"orderCode":"DEMO-DH001","dueDate":"2026-11-10","productId":1,"productCode":"DEMO-AO01","c… |
| 071 | Expected ids {14659, 14660, 14662} to not contain {14659, 14661, 14662, 14663}, but found {14659, 14662}. |
| 081 | Expected r.Code to be one of {400} because 200 {"success":true,"data":[],"message":null}, but found 200. |
| 082 | Expected r.Code to be 200 because 409 {"success":false,"errorCode":"TEAM_ORDER_BUSY","message":"Tổ đang làm đơn DH-0002 (đã nhập kho TP 0% / đặt). Chỉ giao đơn khác khi đạt từ 90% hoặc đơn đã đóng."}… |
| 085 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"ORDER_LINE_TEAM_INVALID","message":"Tổ được chia phải là tổ may đang hoạt động."}, but found 409. |
| 086 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"ORDER_LINE_TEAM_INVALID","message":"Tổ được chia phải là tổ may đang hoạt động."}, but found 409. |
| 088 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"ORDER_LINE_TEAM_INVALID","message":"Tổ được chia phải là tổ may đang hoạt động."}, but found 409. |
| 090 | Expected r.Code to be 200 because 409 {"success":false,"errorCode":"TEAM_ORDER_BUSY","message":"Tổ đang làm đơn DH-0002 (đã nhập kho TP 0% / đặt). Chỉ giao đơn khác khi đạt từ 90% hoặc đơn đã đóng."}… |
| 092 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"ORDER_LINE_TEAM_INVALID","message":"Tổ được chia phải là tổ may đang hoạt động."}, but found 409. |
| 094 | Expected results.Count(IsSuccess) to be greater than or equal to 1 because 409 {"success":false,"errorCode":"TEAM_ORDER_BUSY","message":"Tổ đang làm đơn DH-0002 (đã nhập kho TP 0% / đặt). Chỉ giao đơ… |
| 095 | Expected results.Count(IsSuccess) to be greater than or equal to 1 because 409 {"success":false,"errorCode":"TEAM_ORDER_BUSY","message":"Tổ đang làm đơn DH-0002 (đã nhập kho TP 0% / đặt). Chỉ giao đơ… |
| 096 | Expected (PutAsync(M + "order-line-teams", token, new { OrderLineId = id, TeamIds = new[] { S0.TSew01.TeamId } })).Code to be 200, but found 409 (difference of 209). |
| 116 | Expected r.Code to be 200 because 409 {"success":false,"errorCode":"ORDER_MARKER_INVALID","message":"Bìa S1: size M không có trong đơn ở các màu dùng nhóm bìa của bìa."}, but found 409 (difference of… |
| 118 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"ORDER_MARKER_INVALID","message":"Mã hàng không có trong đơn hàng."}, but found 409. |
| 123 | Expected B1Json.Bool(r.Data, "canImport") to be True, but found False. |
| 125 | Expected keep.Code to be 200 because 400 {"success":false,"errorCode":"SPEC_IMPORT_INVALID","message":"File không có thay đổi nào hợp lệ để nhập."}, but found 400 (difference of 200). |
| 133 | Expected r.Code to be one of {200, 201} because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"requiredQty":["Số cần tối đa 100.000.000."]}}, but fo… |
| 135 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"WAREHOUSE_MATERIAL_INACTIVE","message":"NVL đã ngừng sử dụng."}, but found 409. |
| 143 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":{"orderMaterialId":4634,"orderId":15350,"materialId":17184,"materialCode":"V-0001","materialName":"Kaki","materialType":"FAB… |
| 150 | Expected (DsdAsync(D.Od1.OrderId)) to contain only items matching (Convert(x.SeqNo, Int32) >= 1), but the collection is empty. |
| 151 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"orderId":15211,"deliveryCount":2,"deliveryDate":"2026-12-05","deliveryDateTo":"2026-12-05","receivedCount":0,"hasCustomerMateri… |
| 155 | Expected B1Json.Prop(r.Data, "rows")!.Value.EnumerateArray().Select(x => B1Json.Str(x, "action")) to contain only items matching B4OrderMaterialTests.Actions.Contains(a), but {"Error", "Error", "Erro… |
| 158 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"ORDER_MATERIAL_IMPORT_INVALID","message":"File không có dòng hợp lệ nào thay đổi NVL của đơn."}, but found 400 (difference of 200). |
| 162 | Expected B1Json.Prop(preview.Data, "blocked")!.Value.EnumerateArray().Select(x => x.GetString()) {empty} to contain "V-0001". |
| 163 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"created":1,"updated":0,"removed":0},"message":"Đã nhập NVL của đơn: thêm 1, cập nhật 0."}, but found 200. |
| 166 | Expected r.Code to be one of {400} because 404 {"success":false,"errorCode":"COMMON_NOT_FOUND","message":"Không tìm thấy dữ liệu hoặc bạn không có quyền xem."}, but found 404. |
| 170 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"ORDER_LINE_TEAM_INVALID","message":"Tổ được chia phải là tổ may đang hoạt động."}, but found 409. |
