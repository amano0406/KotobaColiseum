namespace KotobaColiseum.Web.Models;

public sealed class AppRuntimeOptions
{
    public const string SectionName = "KotobaColiseum";

    public string DisplayName { get; set; } = "ことばコロシアム";

    public OpenAiRuntimeOptions OpenAi { get; set; } = new();

    public BattleRuntimeOptions Battle { get; set; } = new();

    public EnemyProfile Enemy { get; set; } = new();
}

public sealed class OpenAiRuntimeOptions
{
    public string ApiBaseUrl { get; set; } = "https://api.openai.com/v1";

    public string TextModel { get; set; } = "gpt-4o-mini";

    public string ImageModel { get; set; } = "gpt-image-1-mini";

    public string ImageSize { get; set; } = "1024x1024";

    public string ImageQuality { get; set; } = "low";

    public bool EnableImageGeneration { get; set; } = true;

    public bool ForceMockMode { get; set; }
}

public sealed class BattleRuntimeOptions
{
    public int StartingHp { get; set; } = 100;

    public int MaxDamage { get; set; } = 40;

    public bool AllowMockWithoutKey { get; set; } = true;
}

public sealed class EnemyProfile
{
    public string Id { get; set; } = "yakisoba-man";

    public string Name { get; set; } = "焼きそば食べたいマン";

    public string Species { get; set; } = "屋台妖人";

    public string Archetype { get; set; } = "屋台見栄っぱり";

    public string PersonaSummary { get; set; } = "見栄っ張りで、すぐムキになるが、芯は弱い。";

    public string SpeechStyle { get; set; } = "少し荒い口調。追い詰められると語尾が崩れる。";

    public string[] WeakPoints { get; set; } =
    [
        "見栄",
        "知ったかぶり",
        "食の浅さ",
    ];
}
