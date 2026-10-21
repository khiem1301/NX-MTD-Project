using System.Security.Cryptography;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Security;

public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    /// <summary>D29: 6 chữ số ngẫu nhiên an toàn (không dùng Random).</summary>
    public string Generate() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
