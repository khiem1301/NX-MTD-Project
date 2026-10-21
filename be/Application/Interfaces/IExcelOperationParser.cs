using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Đọc file Excel quy trình công nghệ của mã hàng (sheet "BƯỚC CÔNG VIỆC") — KI-01.</summary>
public interface IExcelOperationParser
{
    /// <summary>Trả các sheet đang hiển thị có bảng bước công việc. Ném InvalidDataException khi không có sheet nào đúng mẫu.</summary>
    IReadOnlyList<OperationExcelSheetDto> Parse(Stream stream);
}
