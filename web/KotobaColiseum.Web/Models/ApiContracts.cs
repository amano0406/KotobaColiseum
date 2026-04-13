namespace KotobaColiseum.Web.Models;

public static class BattleGenerationModes
{
    public const string Fixed = "fixed";
    public const string Dynamic = "dynamic";

    public static bool IsSupported(string? value)
    {
        return string.Equals(value, Fixed, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Dynamic, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? value)
    {
        return string.Equals(value, Fixed, StringComparison.OrdinalIgnoreCase)
            ? Fixed
            : Dynamic;
    }
}

public sealed record ErrorResponse(string Error);

public sealed record SettingsStatusResponse(bool HasOpenAiKey, string BattleGenerationMode);

public sealed record OpenAiSaveRequest(string ApiKey);

public sealed record OpenAiTestResponse(bool Success, string Message);

public sealed record BattleGenerationModeSaveRequest(string Mode);

public sealed record EncounterStateDto(EnemyStateDto[] Enemies);

public sealed record EnemyStateDto(
    string Id,
    string Name,
    string Species,
    string Archetype,
    string PersonaSummary,
    string SpeechStyle,
    string[] WeakPoints,
    int MaxHp,
    int CurrentHp,
    string GenerationMode);

public sealed record StartBattleResponse(
    EncounterStateDto Encounter,
    EnemyStateDto Enemy,
    string WorldIntro,
    string OpeningLine,
    string EnemyImagePrompt,
    string BgImagePrompt,
    string EnemyImage,
    string BgImage,
    string Provider,
    string GenerationMode,
    bool FellBackToFixed);

public sealed record BattleHistoryEntry(
    string Speaker,
    string Text,
    int? Damage = null,
    string? Reason = null);

public sealed record AttackRequest(
    EnemyStateDto Enemy,
    IReadOnlyList<BattleHistoryEntry>? BattleHistory,
    string PlayerText);

public sealed record AttackResponse(
    int Damage,
    string Reason,
    string EnemyLine,
    string Animation,
    string Provider);
