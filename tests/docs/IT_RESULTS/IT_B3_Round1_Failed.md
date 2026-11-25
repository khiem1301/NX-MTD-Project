# IT Batch 3 — Round 1: các ca Failed

Kết quả: **171 ca, 115 Passed, 56 Failed**. Log: `IT_B3_Round1.trx`.

| Ca | Thông báo lỗi |
|---|---|
| 015 | Expected r.Code to be 200 because 404 , but found 404 (difference of 204). |
| 017 | Expected results.Count(IsSuccess) to be 1 because 409 {"success":false,"errorCode":"PRODUCT_CODE_DUPLICATE","message":"Mã hàng đã tồn tại."} \| 409 {"success":false,"errorCode":"PRODUCT_CODE_DUPLICATE… |
| 021 | Expected big.Code to be 200 because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"pageSize":["Số dòng mỗi trang phải từ 1 đến 100."]}}, but found 4… |
| 022 | Expected j to be in descending order, but found {1, 2, 2922, 2923, 2924, 8935, 8936, 8937, 8938, 8939, 8940, 8941, 8942, 8943, 8944, 8945, 8946, 8947, 8948, 8949} where item at index 0 is in wrong or… |
| 026 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"PRODUCT_SPEC_INVALID","message":"Nhóm bìa 1, màu Đen: các chi tiết dùng vải khác nhau. Một bìa chỉ trải trên một vải: chuyển màu Đe… |
| 028 | Expected r.Code to be one of {200, 409}, but found 400. |
| 035 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"PRODUCT_SPEC_INVALID","message":"Chi tiết cắt \"Thân trước\" màu Đen khai hơn một vải. Mỗi chi tiết cắt chỉ một vải: tách vải lót, … |
| 045 | Did not expect r.Body "{"success":false,"errorCode":"COMMON_DB_RULE","message":"it-b3 fault injection"}" to contain any of the strings: {"it-b3 fault"}. |
| 049 | Expected B1Json.Str(FindBy(r.Data, "productCode", "SP400")!.Value, "action") to be one of {"New", "Pending"}, but found "Error". |
| 050 | Expected B1Json.Str(FindBy(r.Data, "materialCode", "V-9999", skip: B1Json.Prop(r.Data, "missingMaterials")!.Value)!.Value, "action") to be the same string, but they differ at index 0: ↓ (actual) "New… |
| 052 | Expected B1Json.Str(B1Json.Prop(r.Data, "orderMarkers")!.Value.EnumerateArray().Single(), "action") to be "New" with a length of 3, but "Error" has a length of 5, differs near "Err" (index 0). |
| 053 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"SPEC_IMPORT_INVALID","message":"File không có thay đổi nào hợp lệ để nhập."}, but found 400 (difference of 200). |
| 057 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"components":0,"norms":0,"markerGroups":0,"orderMarkers":1,"removed":0},"message":"Đã nhập: 0 thành phần, 0 dòng định mức, 0 nhó… |
| 058 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":{"components":0,"norms":0,"markerGroups":0,"orderMarkers":1,"removed":0},"message":"Đã nhập: 0 thành phần, 0 dòng định mức, … |
| 059 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"ORDER_MARKER_INVALID","message":"Mã hàng không có trong đơn hàng."}, but found 409. |
| 060 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"SPEC_IMPORT_INVALID","message":"File không có thay đổi nào hợp lệ để nhập."}, but found 400 (difference of 200). |
| 061 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"SPEC_IMPORT_INVALID","message":"File không có thay đổi nào hợp lệ để nhập."}, but found 400 (difference of 200). |
| 062 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"SPEC_IMPORT_INVALID","message":"Màu Đen: chi tiết cắt \"Cổ\" chưa nằm trong nhóm bìa nào của màu này."}, but found 400 (difference … |
| 063 | Expected B1Json.Int(r.Data, "removed") to be 2, but found 1. |
| 064 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"components":1,"norms":0,"markerGroups":0,"orderMarkers":0,"removed":0},"message":"Đã nhập: 1 thành phần, 0 dòng định mức, 0 nhó… |
| 074 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":5910,"message":"Đã lưu."}, but found 200. |
| 075 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":null,"message":"Đã lưu."}, but found 200. |
| 077 | Expected off.Code to be 200 because 409 {"success":false,"errorCode":"OPERATION_FINAL_STEP_REQUIRED","message":"Không ngừng dùng được: đây là công đoạn may cuối đang dùng duy nhất của mã hàng. Hãy ch… |
| 084 | Expected Code to be less than 500 because mọi lỗi phải xử lý êm, không được 5xx. Body: {"success":false,"errorCode":"COMMON_INTERNAL_ERROR","message":"Lỗi hệ thống. Vui lòng thử lại."}, but found 500. |
| 087 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"PRODUCT_NOT_ACTIVE","message":"Mã hàng SP003 đã ngừng sản xuất."}, but found 409. |
| 089 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"OPERATION_INVALID","message":"Công đoạn chỉ gắn chi tiết vải (PANEL) của đúng mã hàng; phụ liệu không gắn công đoạn."}, but found 4… |
| 090 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":null,"message":"Đã lưu."}, but found 200. |
| 092 | Expected r.Code to be one of {400, 404} because 200 {"success":true,"data":null,"message":"Đã lưu."}, but found 200. |
| 094 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":null,"message":"Đã lưu."}, but found 200. |
| 098 | Expected r.Code to be one of {400} because 404 {"success":false,"errorCode":"COMMON_NOT_FOUND","message":"Không tìm thấy dữ liệu hoặc bạn không có quyền xem."}, but found 404. |
| 101 | Expected (Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.PieceRates)) to be 5 because S0 có 5 đơn giá, import không tạo thêm, but found 310 (difference of 305). |
| 102 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"created":3,"updated":0,"unchanged":0,"skipped":0,"rateCreated":3,"rateUpdated":0,"rateUnchanged":0,"rateSkipped":0,"removed":0}… |
| 103 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"created":2,"updated":0,"unchanged":0,"skipped":1,"rateCreated":2,"rateUpdated":0,"rateUnchanged":0,"rateSkipped":0,"removed":0}… |
| 105 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"OPERATION_FINAL_STEP_EXISTS","message":"Mã hàng này đã có công đoạn may cuối đang dùng. Chỉ được một công đoạn cuối."}, bu… |
| 106 | Expected r.Code to be 200 because 409 {"success":false,"errorCode":"OPERATION_FINAL_STEP_EXISTS","message":"Mã hàng này đã có công đoạn may cuối đang dùng. Chỉ được một công đoạn cuối."}, but found 4… |
| 107 | Expected r.Code to be 200 because 500 {"success":false,"errorCode":"COMMON_INTERNAL_ERROR","message":"Lỗi hệ thống. Vui lòng thử lại."}, but found 500 (difference of 300). |
| 109 | Expected B1Json.Int(r.Data, "rateUpdated") to be 1, but found 0 (difference of -1). |
| 110 | Expected B1Json.Int(r.Data, "rateSkipped") to be greater than or equal to 1, but found 0 (difference of -1). |
| 111 | Expected r.Code to be one of {400} because 200 {"success":true,"data":{"created":1,"updated":0,"unchanged":0,"skipped":0,"rateCreated":0,"rateUpdated":0,"rateUnchanged":0,"rateSkipped":0,"removed":0}… |
| 112 | Expected r.Code to be 400 because 409 {"success":false,"errorCode":"COMMON_CONSTRAINT_VIOLATION","message":"Không lưu được: dữ liệu đang được dùng ở nơi khác hoặc giá trị không hợp lệ."}, but found 4… |
| 115 | Did not expect r.Body "{"success":false,"errorCode":"COMMON_DB_RULE","message":"it-b3 fault injection"}" to contain any of the strings: {"it-b3 fault"}. |
| 130 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"OPERATION_INVALID","message":"Công đoạn và nhóm phải cùng mã hàng."}, but found 409. |
| 131 | Expected dup.Code to be 200 because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"operationIds":["Công đoạn bị chọn trùng."]}}, but found 400 (diff… |
| 136 | Expected r.Code to be 400 because 200 {"success":true,"data":3636,"message":"Đã lưu."}, but found 200 (difference of -200). |
| 137 | Expected r.Code to be one of {200, 201} because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"rate":["Đơn giá không được vượt quá 100,000,000 đồng.… |
| 138 | Expected r.Code to be one of {400} because 409 {"success":false,"errorCode":"OPERATION_NOT_ACTIVE","message":"Công đoạn đã ngừng dùng."}, but found 409. |
| 143 | Expected r.Code to be one of {400, 404} because 200 {"success":true,"data":null,"message":"Đã lưu."}, but found 200. |
| 149 | Expected r.Code to be 200 because 400 {"success":false,"errorCode":"COMMON_VALIDATION","message":"Dữ liệu không hợp lệ.","errors":{"ids":["The specified condition was not met for 'Ids'."]}}, but foun… |
| 150 | Expected B1Json.Str(r.Data, "approvalNote") to be "Đồng ý", but found <null>. |
| 153 | Expected Code to be less than 500 because mọi lỗi phải xử lý êm, không được 5xx. Body: {"success":false,"errorCode":"COMMON_INTERNAL_ERROR","message":"Lỗi hệ thống. Vui lòng thử lại."}, but found 500. |
| 154 | Expected B1Json.Str(r.Data, "rejectReason") to be "Đơn giá cao hơn thị trường", but found <null>. |
| 160 | Expected r.Code to be one of {400, 404} because 409 {"success":false,"errorCode":"PIECE_RATE_INVALID_STATUS","message":"Trạng thái đơn giá không cho phép thao tác này."}, but found 409. |
| 161 | Expected results.Count(IsSuccess) to be 1 because 200 {"success":true,"data":null,"message":"Đã duyệt đơn giá."} \| 200 {"success":true,"data":null,"message":"Đã từ chối đơn giá."}, but found 2. |
| 165 | Expected pending.Items().Select(i => B1Json.Int(i, "pieceRateId")!.Value) to be a collection with 2 item(s), but {2336, 2335, 2334, 2333, 2332, 2331, 3698, 3695, 2375, 2372, 2369, 2366, 2364, 2361, 2… |
| 167 | Expected op2.GetRawText() "{"operationId":5358,"productId":10742,"productCode":"SP001","operationCode":"CD02","operationName":"Công đoạn CD02","stationSequence":2,"skillLevel":null,"standardTimeSec":… |
| 169 | Expected r.Code to be one of {400, 409} because 200 {"success":true,"data":{"components":0,"norms":0,"markerGroups":0,"orderMarkers":1,"removed":0},"message":"Đã nhập: 0 thành phần, 0 dòng định mức, … |
