using System.Linq.Expressions;

namespace NX_MTD.Application.Interfaces;

public interface IDocumentCodeGenerator
{
    /// <summary>Sinh mã "PREFIX-yyyyMMdd-001"; phải gọi trong transaction để khóa theo ngày.</summary>
    Task<string> NextAsync<T>(string prefix, Expression<Func<T, string>> codeSelector, CancellationToken ct)
        where T : class;
}
