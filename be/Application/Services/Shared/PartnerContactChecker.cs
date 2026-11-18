using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-125: SĐT / email không được trùng giữa mọi khách hàng và nhà cung cấp (kể cả đã ngừng). SĐT so theo phần chữ số (bỏ
/// khoảng trắng, dấu chấm, gạch...), email so không phân biệt hoa thường. Hai danh sách nhỏ nên đọc các liên hệ đang có rồi so
/// trong bộ nhớ (SQL không chuẩn hóa được SĐT). Không có unique index ở DB vì phải xét chéo hai bảng.
/// </summary>
public sealed class PartnerContactChecker : IPartnerContactChecker
{
    private const string PhoneField = "phone";
    private const string EmailField = "email";

    private readonly IUnitOfWork _uow;

    public PartnerContactChecker(IUnitOfWork uow) => _uow = uow;

    private sealed record Contact(string Owner, string? Phone, string? Email);

    public async Task<Result?> CheckAsync(string? phone, string? email, int? exceptCustomerId, int? exceptSupplierId,
        CancellationToken ct)
    {
        var phoneKey = PhoneKey(phone);
        var emailKey = EmailKey(email);
        if (phoneKey is null && emailKey is null) return null;

        var contacts = await ContactsAsync(exceptCustomerId, exceptSupplierId, ct);
        var samePhone = phoneKey is null ? null : contacts.FirstOrDefault(c => PhoneKey(c.Phone) == phoneKey);
        var sameEmail = emailKey is null ? null : contacts.FirstOrDefault(c => EmailKey(c.Email) == emailKey);
        if (samePhone is null && sameEmail is null) return null;

        var errors = new Dictionary<string, string[]>();
        if (samePhone is not null) errors[PhoneField] = [string.Format(Messages.PartnerPhoneDuplicate, samePhone.Owner)];
        if (sameEmail is not null) errors[EmailField] = [string.Format(Messages.PartnerEmailDuplicate, sameEmail.Owner)];
        var (code, message) = samePhone is not null
            ? (ErrorCodes.PartnerPhoneDuplicate, errors[PhoneField][0])
            : (ErrorCodes.PartnerEmailDuplicate, errors[EmailField][0]);
        return Result.Failure(code, message, ResultKind.Validation, errors);
    }

    /// <summary>Liên hệ của mọi khách hàng và nhà cung cấp có SĐT hoặc email, trừ đơn vị đang sửa; Owner = "khách hàng X" / "nhà cung cấp Y".</summary>
    private async Task<List<Contact>> ContactsAsync(int? exceptCustomerId, int? exceptSupplierId, CancellationToken ct)
    {
        var customers = await _uow.Repository<Customer>().Query().AsNoTracking()
            .Where(c => (c.Phone != null || c.Email != null) && (exceptCustomerId == null || c.CustomerId != exceptCustomerId))
            .Select(c => new { c.CustomerName, c.Phone, c.Email }).ToListAsync(ct);
        var suppliers = await _uow.Repository<Supplier>().Query().AsNoTracking()
            .Where(s => (s.Phone != null || s.Email != null) && (exceptSupplierId == null || s.SupplierId != exceptSupplierId))
            .Select(s => new { s.SupplierName, s.Phone, s.Email }).ToListAsync(ct);
        return customers.Select(c => new Contact(string.Format(Messages.PartnerOwnerCustomer, c.CustomerName), c.Phone, c.Email))
            .Concat(suppliers.Select(s => new Contact(string.Format(Messages.PartnerOwnerSupplier, s.SupplierName), s.Phone, s.Email)))
            .ToList();
    }

    private static string? PhoneKey(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static string? EmailKey(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToUpperInvariant();
}
