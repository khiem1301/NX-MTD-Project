namespace NX_MTD.Application.Common;

/// <summary>
/// DEV-ALL-121: file Excel đọc được nhưng không có bảng cần nhập (không thấy dòng tiêu đề đúng tên cột). Tách khỏi lỗi sai mẫu
/// chung (InvalidDataException, lớp sealed nên không kế thừa được) để màn hình biết mục đó chỉ cần bỏ qua khi dùng một file nhiều
/// sheet cho nhiều mục. Nơi gọi parser phải bắt riêng ngoại lệ này.
/// </summary>
public sealed class ExcelTableMissingException(string message) : Exception(message);
