using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

public class AttachmentServiceTests
{
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

    private readonly IAttachmentRepository _repo = Substitute.For<IAttachmentRepository>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAttachmentOwnerAccess _requestAccess = Substitute.For<IAttachmentOwnerAccess>();

    public AttachmentServiceTests()
    {
        _user.Role.Returns(Roles.Manager);
        _clock.Now.Returns(new DateTime(2026, 10, 22, 8, 0, 0));
        _storage.SaveAsync(default!, default!, default!, default).ReturnsForAnyArgs("REQUEST/2026/10/x.png");
        _requestAccess.OwnerType.Returns(AttachmentOwnerTypes.Request);
        _requestAccess.CanWriteAsync(default, default).ReturnsForAnyArgs(true);
        _requestAccess.MaxFilesAsync(default).ReturnsForAnyArgs(5);
    }

    private AttachmentService Create(int maxMb = 5) => new(_repo, _storage, _uow, _user, _clock,
        Options.Create(new AttachmentOptions { MaxFileMb = maxMb }), new[] { _requestAccess });

    [Fact]
    public async Task Upload_PngWithJpgName_SavesAsPng()
    {
        var result = await Create().UploadAsync("Request", 1, "Photo", new FileUpload(PngBytes, "a.jpg"), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ContentType.Should().Be("image/png");
        await _storage.Received(1).SaveAsync(Arg.Any<byte[]>(), "REQUEST", ".png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upload_PdfBytes_ReturnsFileTypeNotAllowed()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 1, 2, 3 };

        var result = await Create().UploadAsync("Request", 1, "Photo", new FileUpload(pdf, "a.jpg"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AttachmentFileTypeNotAllowed);
        result.Kind.Should().Be(ResultKind.Validation);
        await _storage.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Upload_OverMaxSize_ReturnsTooLarge()
    {
        var big = new byte[1024 * 1024 + 1];
        PngBytes.CopyTo(big, 0);

        var result = await Create(maxMb: 1).UploadAsync("Request", 1, "Photo", new FileUpload(big, "a.png"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AttachmentFileTooLarge);
    }

    [Fact]
    public async Task Upload_SixthPhoto_ReturnsLimitReached()
    {
        _repo.CountCurrentAsync(default!, default, default).ReturnsForAnyArgs(5);

        var result = await Create().UploadAsync("Request", 1, "Photo", new FileUpload(PngBytes, "a.png"), default);

        result.ErrorCode.Should().Be(ErrorCodes.AttachmentLimitReached);
    }

    [Fact]
    public async Task Upload_OwnerWithoutAccessRegistration_NonAdmin_ReturnsNotFound()
    {
        var result = await Create().UploadAsync("DispatchNote", 1, "Photo", new FileUpload(PngBytes, "a.png"), default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task Remove_SetsIsCurrentFalse()
    {
        var attachment = new Attachment { AttachmentId = 9, OwnerType = "REQUEST", OwnerId = 1, IsCurrent = true };
        _repo.GetAsync(9, default).ReturnsForAnyArgs(attachment);

        var result = await Create().RemoveAsync(9, default);

        result.IsSuccess.Should().BeTrue();
        attachment.IsCurrent.Should().BeFalse();
        await _uow.Received(1).SaveChangesAsync();
    }
}

public class FileSignaturesTests
{
    [Fact]
    public void DetectImage_Jpg() => FileSignatures.DetectImage(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })!.Extension.Should().Be(".jpg");

    [Fact]
    public void DetectImage_Png() =>
        FileSignatures.DetectImage(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })!.Extension.Should().Be(".png");

    [Fact]
    public void DetectImage_Webp() =>
        FileSignatures.DetectImage("RIFF\0\0\0\0WEBP"u8)!.Extension.Should().Be(".webp");

    [Fact]
    public void DetectImage_GifPdfAndTiny_ReturnNull()
    {
        FileSignatures.DetectImage("GIF89a......"u8).Should().BeNull();
        FileSignatures.DetectImage("%PDF-1.4"u8).Should().BeNull();
        FileSignatures.DetectImage(new byte[] { 0xFF, 0xD8 }).Should().BeNull();
    }
}
