using Microsoft.Extensions.Options;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class AttachmentService : IAttachmentService
{
    private const int DefaultMaxFiles = 5;

    private readonly IAttachmentRepository _attachments;
    private readonly IFileStorage _storage;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly AttachmentOptions _options;
    private readonly Dictionary<string, IAttachmentOwnerAccess> _access;

    public AttachmentService(IAttachmentRepository attachments, IFileStorage storage, IUnitOfWork uow, ICurrentUser user,
        IClock clock, IOptions<AttachmentOptions> options, IEnumerable<IAttachmentOwnerAccess> access)
    {
        _attachments = attachments; _storage = storage; _uow = uow; _user = user; _clock = clock;
        _options = options.Value;
        _access = access.ToDictionary(a => a.OwnerType);
    }

    public async Task<Result<AttachmentDto>> UploadAsync(string ownerType, int ownerId, string purpose, FileUpload file,
        CancellationToken ct)
    {
        var owner = CodeText.ToCode(ownerType);
        var purposeCode = CodeText.ToCode(purpose);
        var invalid = ValidateInput(owner, purposeCode, file);
        if (invalid is not null) return Fail.As<AttachmentDto>(invalid);

        var kind = FileSignatures.DetectImage(file.Content.AsSpan(0, Math.Min(12, file.Content.Length)));
        if (kind is null)
            return Result<AttachmentDto>.Failure(ErrorCodes.AttachmentFileTypeNotAllowed, Messages.AttachmentFileTypeNotAllowed,
                ResultKind.Validation);

        if (!await CanWriteAsync(owner, ownerId, ct)) return Fail.NotFound<AttachmentDto>();
        var max = await MaxFilesAsync(owner, ct);
        if (await _attachments.CountCurrentAsync(owner, ownerId, ct) >= max)
            return Result<AttachmentDto>.Failure(ErrorCodes.AttachmentLimitReached, string.Format(Messages.AttachmentLimitReached, max));

        var key = await _storage.SaveAsync(file.Content, owner, kind.Extension, ct);
        var entity = new Attachment
        {
            OwnerType = owner, OwnerId = ownerId, Purpose = purposeCode, FileUrl = key,
            FileName = Path.GetFileName(file.FileName), ContentType = kind.ContentType, IsCurrent = true, UploadedAt = _clock.Now,
        };
        await _attachments.AddAsync(entity, ct);
        await _uow.SaveChangesAsync();
        return Result<AttachmentDto>.Success(ToDto(entity), Messages.AttachmentUploaded);
    }

    public async Task<Result<IReadOnlyList<AttachmentDto>>> ListAsync(string ownerType, int ownerId, CancellationToken ct)
    {
        var owner = CodeText.ToCode(ownerType);
        if (!AttachmentOwnerTypes.All.Contains(owner) || !await CanReadAsync(owner, ownerId, ct))
            return Fail.NotFound<IReadOnlyList<AttachmentDto>>();
        return Result<IReadOnlyList<AttachmentDto>>.Success(await _attachments.ListCurrentAsync(owner, ownerId, ct));
    }

    public async Task<Result<AttachmentContent>> OpenAsync(long attachmentId, CancellationToken ct)
    {
        var a = await _attachments.GetAsync(attachmentId, ct);
        if (a is null || !await CanReadAsync(a.OwnerType, a.OwnerId, ct)) return Fail.NotFound<AttachmentContent>();
        var stream = await _storage.OpenReadAsync(a.FileUrl, ct);
        return stream is null
            ? Fail.NotFound<AttachmentContent>()
            : Result<AttachmentContent>.Success(new AttachmentContent(stream, a.ContentType ?? "application/octet-stream", a.FileName));
    }

    public async Task<Result> RemoveAsync(long attachmentId, CancellationToken ct)
    {
        var a = await _attachments.GetAsync(attachmentId, ct);
        if (a is null || !a.IsCurrent || !await CanWriteAsync(a.OwnerType, a.OwnerId, ct)) return Fail.NotFound();
        a.IsCurrent = false;
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.AttachmentRemoved);
    }

    private Result? ValidateInput(string owner, string purpose, FileUpload file)
    {
        if (!AttachmentOwnerTypes.All.Contains(owner))
            return Result.Failure(ErrorCodes.AttachmentOwnerTypeInvalid, Messages.AttachmentOwnerTypeInvalid, ResultKind.Validation);
        if (!AttachmentPurposes.All.Contains(purpose) || purpose == AttachmentPurposes.ImportFile)
            return Result.Failure(ErrorCodes.AttachmentPurposeInvalid, Messages.AttachmentPurposeInvalid, ResultKind.Validation);
        if (file.Content.Length == 0)
            return Result.Failure(ErrorCodes.AttachmentFileEmpty, Messages.AttachmentFileEmpty, ResultKind.Validation);
        if (file.Content.Length > _options.MaxFileBytes)
            return Result.Failure(ErrorCodes.AttachmentFileTooLarge,
                string.Format(Messages.AttachmentFileTooLarge, _options.MaxFileMb), ResultKind.Validation);
        return null;
    }

    // D20: OwnerType chưa có ai đăng ký quyền → chỉ Admin.
    private Task<bool> CanReadAsync(string owner, int id, CancellationToken ct) =>
        _access.TryGetValue(owner, out var a) ? a.CanReadAsync(id, ct) : Task.FromResult(_user.Role == Roles.Admin);

    private Task<bool> CanWriteAsync(string owner, int id, CancellationToken ct) =>
        _access.TryGetValue(owner, out var a) ? a.CanWriteAsync(id, ct) : Task.FromResult(_user.Role == Roles.Admin);

    private Task<int> MaxFilesAsync(string owner, CancellationToken ct) =>
        _access.TryGetValue(owner, out var a) ? a.MaxFilesAsync(ct) : Task.FromResult(DefaultMaxFiles);

    private static AttachmentDto ToDto(Attachment a) => new(a.AttachmentId, CodeText.ToPascal(a.OwnerType), a.OwnerId,
        CodeText.ToPascal(a.Purpose), a.FileName, a.ContentType, a.UploadedAt);
}
