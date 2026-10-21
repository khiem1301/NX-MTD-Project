namespace NX_MTD.Application.Common;

/// <summary>Nhận diện ảnh bằng byte đầu file, không tin phần mở rộng / Content-Type client gửi (D19).</summary>
public static class FileSignatures
{
    public sealed record ImageKind(string Extension, string ContentType);

    private static readonly byte[] PngHead = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public static ImageKind? DetectImage(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return new ImageKind(".jpg", "image/jpeg");
        if (head.Length >= 8 && head[..8].SequenceEqual(PngHead))
            return new ImageKind(".png", "image/png");
        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
            return new ImageKind(".webp", "image/webp");
        return null;
    }
}
