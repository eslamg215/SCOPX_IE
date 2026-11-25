namespace SCOPX.Infrastructure.AI;

/// <summary>Provider-neutral contracts for future, clinician-supervised AI assistance.</summary>
public enum AiTaskKind { EducationalExplanation, DraftingSupport, ImageReviewPlaceholder }

public sealed record AiRequest(AiTaskKind Task, string Prompt, string? Context = null);
public sealed record AiResponse(string Text, string Provider, bool IsClinicalDecision, string SafetyNotice);

public interface IAiAnalysisProvider
{
    string ProviderName { get; }
    Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken);
}

/// <summary>Fail-closed placeholder. No inference is performed until a provider is configured and validated.</summary>
public sealed class DisabledAiProvider : IAiAnalysisProvider
{
    public string ProviderName => "disabled";
    public Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("AI provider is not configured. No analysis was performed.");
}

