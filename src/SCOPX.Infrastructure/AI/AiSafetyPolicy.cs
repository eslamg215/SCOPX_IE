namespace SCOPX.Infrastructure.AI;

/// <summary>Input guardrails for the AI integration scaffold. Not a clinical safety system.</summary>
public static class AiSafetyPolicy
{
    public const int MaxPromptCharacters = 4000;
    public const int MaxContextCharacters = 8000;
    public const string SafetyNotice = "AI output is unvalidated decision-support content. A qualified clinician must independently verify it. Not for emergency or autonomous diagnosis.";

    public static bool IsWithinLimits(AiRequest request)
        => request is not null
           && !string.IsNullOrWhiteSpace(request.Prompt)
           && request.Prompt.Length <= MaxPromptCharacters
           && (request.Context?.Length ?? 0) <= MaxContextCharacters;
}
