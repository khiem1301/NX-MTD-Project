using System.Text.Json;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Infrastructure.Ai;

/// <summary>DEV-ALL-148: nội dung gửi Gemini để gợi ý bậc thợ. Chỉ có số liệu đã ẩn danh, không tên / mã nhân viên / tiền.</summary>
internal static class GeminiGradePrompt
{
    public const string System = """
        Bạn là trợ lý đánh giá bậc thợ cho tổ trưởng một xưởng may gia công (CMT).
        Nhiệm vụ: với mỗi công nhân, gợi ý MỘT bậc trong danh sách bậc được phép, kèm lý do ngắn bằng tiếng Việt.

        Ý nghĩa số liệu của mỗi công nhân (trong kỳ periodDays ngày gần nhất):
        - currentGrade: bậc hiện tại (null = chưa xếp bậc).
        - tenureMonths: số tháng làm việc tại xưởng.
        - workDays: số ngày có công; workdayTotal: tổng ngày công quy đổi (1 công = 8 giờ); workedHours: tổng giờ làm; overtimeHours: giờ tăng ca.
        - outputQty: tổng sản lượng công đoạn; operationCount: số công đoạn khác nhau đã làm (càng nhiều càng đa năng).
        - avgSkillLevel: độ khó trung bình của công đoạn đã làm (số càng lớn càng khó; null = chưa có dữ liệu).
        - efficiencyPercent: giờ chuẩn làm ra / giờ làm thực × 100 (null = chưa đủ dữ liệu thời gian chuẩn).
        - reworkQty: số sản phẩm phải sửa lại; scrapQty: số sản phẩm hỏng phải cắt bù (nặng hơn rework); defectRatePercent: (rework + scrap) / sản lượng × 100.

        Cách đánh giá:
        - Ưu tiên theo thứ tự: chất lượng (tỉ lệ lỗi, scrap), hiệu suất, độ khó và số công đoạn, chuyên cần (ngày công), thâm niên.
        - rank của bậc cho biết thứ hạng (1 = bậc cao nhất, số càng lớn bậc càng thấp). So sánh với mặt bằng chung của cả tổ.
        - Mỗi lần chỉ nên lên hoặc xuống tối đa 1 bậc so với bậc hiện tại, trừ khi số liệu chênh lệch rất rõ.
        - Ít dữ liệu (dưới 10 ngày công hoặc không có sản lượng) thì giữ bậc hiện tại và nói rõ là chưa đủ dữ liệu; chưa có bậc thì xếp bậc thấp nhất hợp lý.
        - Lý do tối đa 250 ký tự, nêu 2–3 số liệu chính đã dựa vào. Không bịa số liệu không có trong dữ liệu.

        Trả về đúng một phần tử cho mỗi công nhân, giữ nguyên "key".
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string User(GradeAdvisorRequest request) => JsonSerializer.Serialize(new
    {
        periodDays = request.PeriodDays,
        grades = request.Grades.Select(g => new { code = g.Code, rank = g.Rank }),
        workers = request.Workers.Select(w => new
        {
            key = w.Key,
            currentGrade = w.CurrentGrade,
            w.Metrics.TenureMonths, w.Metrics.WorkDays, w.Metrics.WorkdayTotal, w.Metrics.WorkedHours, w.Metrics.OvertimeHours,
            w.Metrics.OutputQty, w.Metrics.OperationCount, w.Metrics.AvgSkillLevel, w.Metrics.EfficiencyPercent,
            w.Metrics.ReworkQty, w.Metrics.ScrapQty, w.Metrics.DefectRatePercent,
        }),
    }, Json);

    /// <summary>Schema JSON ép Gemini trả <c>{ suggestions: [{ key, grade, reason }] }</c>, bậc chỉ trong danh sách cho phép.</summary>
    public static object ResponseSchema(IEnumerable<string> gradeCodes) => new
    {
        type = "OBJECT",
        properties = new
        {
            suggestions = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        key = new { type = "STRING" },
                        grade = new { type = "STRING", @enum = gradeCodes.ToArray() },
                        reason = new { type = "STRING" },
                    },
                    required = new[] { "key", "grade", "reason" },
                },
            },
        },
        required = new[] { "suggestions" },
    };
}
