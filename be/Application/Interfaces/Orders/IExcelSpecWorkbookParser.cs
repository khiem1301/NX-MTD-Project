using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>Đọc file Excel định mức mã hàng (DEV-ALL-115): thành phần, định mức NVL, nhóm bìa, bìa của đơn.</summary>
public interface IExcelSpecWorkbookParser
{
    /// <summary>Nhận bảng theo tên cột (không theo tên sheet). Ném InvalidDataException khi file không có bảng nào đúng mẫu.</summary>
    SpecWorkbookDto Parse(Stream stream);
}
