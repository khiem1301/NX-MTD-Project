using Microsoft.AspNetCore.Http;
using NX_MTD.Application.Common;
using NX_MTD.Domain.Constants;

namespace NX_MTD.API.Common;

/// <summary>
/// DEV-ALL-187: kiểm file Excel tải lên trước khi đọc: có chọn file, không rỗng, đuôi .xlsx (không nhận .xls, .csv), không quá lớn.
/// Trả thông báo tiếng Việt thay vì để lỗi 400 chung của ASP.NET hoặc lỗi kỹ thuật của thư viện đọc Excel.
/// </summary>
public static class ExcelUploadGuard
{
    public const int MaxMegabytes = 10;

    /// <summary>Null = file dùng được; còn lại là lỗi để trả về ngay.</summary>
    public static Result? Check(IFormFile? file)
    {
        if (file is null)
            return Result.Failure(ErrorCodes.ImportFileInvalid, Messages.ImportFileMissing, ResultKind.Validation);
        if (file.Length == 0)
            return Result.Failure(ErrorCodes.ImportFileInvalid, Messages.ImportFileEmpty, ResultKind.Validation);
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(ErrorCodes.ImportFileInvalid, Messages.ImportFileWrongType, ResultKind.Validation);
        if (file.Length > MaxMegabytes * 1024L * 1024L)
            return Result.Failure(ErrorCodes.ImportFileInvalid, string.Format(Messages.ImportFileTooLarge, MaxMegabytes), ResultKind.Validation);
        return null;
    }
}
