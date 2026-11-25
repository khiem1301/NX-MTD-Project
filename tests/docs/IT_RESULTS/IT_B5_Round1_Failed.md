# IT Batch 5 — Round 1: các ca Failed

Kết quả: **122 ca, 80 Passed, 42 Failed**. Log: `IT_B5_Round1.trx`.

| Ca | Thông báo lỗi |
|---|---|
| 013 | Expected upper.Code to be 200 because 404 , but found 404 (difference of 204). |
| 015 | Expected r.Code to be 201 because 200 {"success":true,"data":14230,"message":"Đã lưu nhà cung cấp."}, but found 200 (difference of -1). |
| 016 | Expected ok.Code to be 201 because 200 {"success":true,"data":14255,"message":"Đã lưu nhà cung cấp."}, but found 200 (difference of -1). |
| 017 | Expected a.Code to be 201 because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"phone":["Vui lòng nhập số điện thoại.","Số điện thoại không hợp lệ … |
| 018 | Expected r.Code to be 201 because 200 {"success":true,"data":14205,"message":"Đã lưu nhà cung cấp."}, but found 200 (difference of -1). |
| 022 | Expected already.Code to be one of {400, 409}, but found 200. |
| 023 | Expected r.Code to be 201 because 200 {"success":true,"data":{"purchaseOrderId":16593,"poCode":"DM-20261124-001","orderId":null,"orderCode":null,"supplierId":14685,"supplierName":"Vải Thành Công","go… |
| 030 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"purchaseOrderId":16576,"poCode":"DM-20261124-001","orderId":null,"orderCode":null,"supplierId":14679,"supplierName":"Vải Thành … |
| 032 | Expected r.Code to be 201 because 200 {"success":true,"data":{"purchaseOrderId":16335,"poCode":"DM-20261124-001","orderId":null,"orderCode":null,"supplierId":14592,"supplierName":"Vải Thành Công","go… |
| 033 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"SUPPLIER_NOT_ACTIVE","message":"Nhà cung cấp đã ngừng hợp tác, vui lòng chọn nhà cung cấp khác."}, but found 409. |
| 034 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"WAREHOUSE_ORDER_CLOSED","message":"Đơn hàng đã đóng, không thể lập thêm phiếu."}, but found 409. |
| 035 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"WAREHOUSE_MATERIAL_INACTIVE","message":"NVL đã ngừng sử dụng."}, but found 409. |
| 036 | Expected Code to be less than 500 because mọi lỗi phải xử lý êm, không được 5xx. Body: {"success":false,"errorCode":"COMMON_INTERNAL_ERROR","message":"Lỗi hệ thống. Vui lòng thử lại."}, but found 500. |
| 037 | Expected Code to be less than 500 because mọi lỗi phải xử lý êm, không được 5xx. Body: {"success":false,"errorCode":"COMMON_INTERNAL_ERROR","message":"Lỗi hệ thống. Vui lòng thử lại."}, but found 500. |
| 038 | Expected Code to be less than 500 because mọi lỗi phải xử lý êm, không được 5xx. Body: {"success":false,"errorCode":"COMMON_INTERNAL_ERROR","message":"Lỗi hệ thống. Vui lòng thử lại."}, but found 500. |
| 043 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"purchaseOrderId":16458,"poCode":"DM-20261124-001","orderId":null,"orderCode":null,"supplierId":14637,"supplierName":"Vải Thành … |
| 044 | Expected r.Code to be 201 because 200 {"success":true,"data":{"purchaseOrderId":16233,"poCode":"DM-20261124-001","orderId":null,"orderCode":null,"supplierId":14556,"supplierName":"Vải Thành Công","go… |
| 046 | Expected results to contain only items matching ((r.Code == 201) OrElse (r.Code == 409)) because 200 {"success":true,"data":{"purchaseOrderId":15323,"poCode":"DM-20261124-004","orderId":null,"orderCo… |
| 047 | Did not expect r.Body "{"success":false,"errorCode":"COMMON_DB_RULE","message":"it-b5 injected fault"}" to contain any of the strings: {"it-b5 injected fault"}. |
| 050 | Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details. ---- Microsoft.Data.SqlClient.SqlException : The INSERT state… |
| 057 | Expected codes to be in ascending order because ASC, but found {"PO-B5-06", "PO-B5-05", "PO-B5-04", "PO-B5-03", "PO-B5-02", "PO-B5-01", "PO-B2-02", "PO-B2-01", "DM-20261101-001", "DM-DEMO-009", "DM-D… |
| 064 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"PURCHASE_ORDER_LINES_MISMATCH","message":"Danh sách dòng phải đủ mọi dòng của đơn mua, không thêm hoặc bớt."}, but fo… |
| 065 | Expected r.Code to be 400, but found 409 (difference of 9). |
| 066 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"PURCHASE_ORDER_LINES_MISMATCH","message":"Danh sách dòng phải đủ mọi dòng của đơn mua, không thêm hoặc bớt."}, but fo… |
| 067 | Expected Code to be less than 500 because mọi lỗi phải xử lý êm, không được 5xx. Body: {"success":false,"errorCode":"COMMON_INTERNAL_ERROR","message":"Lỗi hệ thống. Vui lòng thử lại."}, but found 500. |
| 068 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"SUPPLIER_NOT_ACTIVE","message":"Nhà cung cấp đã ngừng hợp tác, vui lòng chọn nhà cung cấp khác."}, but found 409. |
| 069 | Expected r.Code to be one of {400} because 200 {"success":true,"data":null,"message":"Đã đặt hàng. Kế toán sẽ xác nhận thanh toán."}, but found 200. |
| 071 | Expected r.Code to be 404 because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"rowVer":["The RowVer field is required."]}}, but found 400 (differe… |
| 072 | Did not expect r.Body "{"success":false,"errorCode":"COMMON_DB_RULE","message":"it-b5 injected fault"}" to contain any of the strings: {"it-b5 injected fault"}. |
| 081 | Expected r.Code to be 200 because 409 {"success":false,"errorCode":"PURCHASE_ORDER_INVALID_STATUS","message":"Trạng thái đơn mua không cho phép thao tác này."}, but found 409 (difference of 209). |
| 083 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"PURCHASE_ORDER_INVALID_STATUS","message":"Trạng thái đơn mua không cho phép thao tác này."}, but found 409. |
| 084 | Expected r.Code to be one of {200, 400}, but found 409. |
| 085 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"PURCHASE_ORDER_INVALID_STATUS","message":"Trạng thái đơn mua không cho phép thao tác này."}, but found 409. |
| 089 | Expected payments to contain 1 item(s), but found 0: {empty}. |
| 095 | Expected r.Code to be one of {400} because 200 {"success":true,"data":null,"message":"Đã xác nhận thanh toán đơn mua."}, but found 200. |
| 096 | Expected r.Code to be 400 because 200 {"success":true,"data":null,"message":"Đã xác nhận thanh toán đơn mua."}, but found 200 (difference of -200). |
| 100 | Did not expect r.Body "{"success":false,"errorCode":"COMMON_DB_RULE","message":"it-b5 injected fault"}" to contain any of the strings: {"it-b5 injected fault"}. |
| 109 | Expected r.Code to be 200 because 409 {"success":false,"errorCode":"PURCHASE_ORDER_INVALID_STATUS","message":"Trạng thái đơn mua không cho phép thao tác này."}, but found 409 (difference of 209). |
| 110 | Expected r.Code to be 200 because 409 {"success":false,"errorCode":"PURCHASE_ORDER_INVALID_STATUS","message":"Trạng thái đơn mua không cho phép thao tác này."}, but found 409 (difference of 209). |
| 111 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":null,"message":"Đã trả lại Quản đốc kèm lý do."}, but found 200. |
| 112 | Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException : The database operation was expected to affect 1 row(s), but actually affected 0 row(s); data may have been modified or deleted since entit… |
| 120 | Expected place.Code to be 200 because 409 {"success":false,"errorCode":"PURCHASE_ORDER_INVALID_STATUS","message":"Trạng thái đơn mua không cho phép thao tác này."}, but found 409 (difference of 209). |
