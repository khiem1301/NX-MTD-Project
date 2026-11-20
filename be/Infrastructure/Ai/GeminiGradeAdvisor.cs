using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;

namespace NX_MTD.Infrastructure.Ai;

/// <summary>
/// DEV-ALL-148: gọi Gemini (<c>models/{model}:generateContent</c>) với đầu ra JSON theo schema để gợi ý bậc thợ.
/// Lỗi mạng, quá thời gian, HTTP lỗi hoặc JSON sai đều trả null để service báo "AI chưa trả được gợi ý" (không lộ chi tiết).
/// </summary>
public sealed class GeminiGradeAdvisor : IGradeAdvisor
{
    private const string ApiKeyHeader = "x-goog-api-key";
    private const string JsonMimeType = "application/json";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiGradeAdvisor> _logger;

    public GeminiGradeAdvisor(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiGradeAdvisor> logger)
    {
        _http = http; _options = options.Value; _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<IReadOnlyList<GradeAdvice>?> SuggestAsync(GradeAdvisorRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{_options.Model}:generateContent")
        {
            Content = JsonContent.Create(BuildBody(request), options: Json),
        };
        message.Headers.Add(ApiKeyHeader, _options.ApiKey);
        try
        {
            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini trả HTTP {Status} khi gợi ý bậc thợ", (int)response.StatusCode);
                return null;
            }
            var body = await response.Content.ReadFromJsonAsync<GenerateResponse>(Json, ct);
            return Parse(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Không gọi được Gemini để gợi ý bậc thợ");
            return null;
        }
    }

    private object BuildBody(GradeAdvisorRequest request) => new
    {
        systemInstruction = new { parts = new[] { new { text = GeminiGradePrompt.System } } },
        contents = new[] { new { role = "user", parts = new[] { new { text = GeminiGradePrompt.User(request) } } } },
        generationConfig = new
        {
            temperature = _options.Temperature,
            responseMimeType = JsonMimeType,
            responseSchema = GeminiGradePrompt.ResponseSchema(request.Grades.Select(g => g.Code)),
            thinkingConfig = new { thinkingBudget = _options.ThinkingBudget },
        },
    };

    private IReadOnlyList<GradeAdvice>? Parse(GenerateResponse? body)
    {
        var text = string.Concat(body?.Candidates?.FirstOrDefault()?.Content?.Parts?
            .Where(p => p.Thought != true).Select(p => p.Text) ?? []);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Gemini không trả nội dung gợi ý bậc thợ");
            return null;
        }
        var parsed = JsonSerializer.Deserialize<AdviceEnvelope>(text, Json);
        return parsed?.Suggestions?
            .Where(s => !string.IsNullOrWhiteSpace(s.Key) && !string.IsNullOrWhiteSpace(s.Grade))
            .Select(s => new GradeAdvice(s.Key!.Trim(), s.Grade!.Trim().ToUpperInvariant(), s.Reason ?? string.Empty))
            .ToList();
    }

    private sealed record GenerateResponse(List<Candidate>? Candidates);
    private sealed record Candidate(Content? Content);
    private sealed record Content(List<Part>? Parts);
    private sealed record Part(string? Text, bool? Thought);
    private sealed record AdviceEnvelope(List<AdviceItem>? Suggestions);
    private sealed record AdviceItem(string? Key, string? Grade, string? Reason);
}
