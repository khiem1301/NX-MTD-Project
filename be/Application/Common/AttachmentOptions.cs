namespace NX_MTD.Application.Common;

public sealed class AttachmentOptions
{
    public int MaxFileMb { get; set; } = 5;
    public long MaxFileBytes => MaxFileMb * 1024L * 1024L;
}
