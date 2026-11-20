using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>
/// DEV-ALL-148: AI (Gemini) gợi ý bậc thợ cho mọi công nhân của tổ; tổ trưởng / tổ phó xem lại, sửa, rồi xác nhận
/// thành đề xuất bậc (<c>GradeEvaluation</c> SUBMITTED). Bản gợi ý được lưu tạm theo tổ.
/// </summary>
public interface IGradeSuggestionService
{
    /// <summary>Bản gợi ý còn hạn của tổ, null nếu chưa có hoặc đã hết hạn.</summary>
    Task<Result<GradeSuggestionDto?>> GetAsync(int teamId, CancellationToken ct);

    /// <summary>Tổng hợp số liệu <c>dto.LookbackDays</c> ngày gần nhất, gọi AI và thay bản gợi ý cũ của tổ (dùng cho cả Tạo lại).</summary>
    Task<Result<GradeSuggestionDto>> GenerateAsync(int teamId, GenerateGradeSuggestionDto dto, CancellationToken ct);

    Task<Result<GradeSuggestionDto>> UpdateItemAsync(int teamId, int workerId, UpdateGradeSuggestionItemDto dto, CancellationToken ct);

    /// <summary>Bỏ dòng gợi ý của một công nhân khỏi bản gợi ý (không gửi đề xuất cho người đó); tạo lại thì có lại.</summary>
    Task<Result<GradeSuggestionDto>> RemoveItemAsync(int teamId, int workerId, CancellationToken ct);

    /// <summary>
    /// Chỉ dòng có bậc khác bậc hiện tại mới thành đề xuất; công nhân đang có đề xuất chờ quyết định thì đề xuất đó được
    /// thay bằng bản mới thay vì tạo thêm.
    /// </summary>
    Task<Result<GradeSuggestionConfirmDto>> ConfirmAsync(int teamId, CancellationToken ct);
}

/// <summary>Gọi mô hình AI gợi ý bậc. Trả null khi AI lỗi / quá thời gian / trả sai định dạng.</summary>
public interface IGradeAdvisor
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<GradeAdvice>?> SuggestAsync(GradeAdvisorRequest request, CancellationToken ct);
}

/// <summary>Kho lưu tạm bản gợi ý AI, mỗi tổ một bản (bản mới thay bản cũ).</summary>
public interface IGradeSuggestionStore
{
    Task<GradeSuggestionDto?> GetAsync(int teamId, CancellationToken ct);
    Task SaveAsync(GradeSuggestionDto draft, CancellationToken ct);
    Task DeleteAsync(int teamId, CancellationToken ct);
}
