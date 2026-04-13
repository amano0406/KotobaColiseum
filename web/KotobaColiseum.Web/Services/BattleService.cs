using KotobaColiseum.Web.Models;
using Microsoft.Extensions.Options;

namespace KotobaColiseum.Web.Services;

public sealed class BattleService
{
    private static readonly int[] StartingHpOffsets = [-16, -12, -8, -4, 0, 4, 8, 12, 16];

    private readonly AppRuntimeOptions _options;
    private readonly SettingsStore _settingsStore;
    private readonly OpenAiService _openAiService;
    private readonly PlaceholderArtService _placeholderArtService;
    private readonly ILogger<BattleService> _logger;

    public BattleService(
        IOptions<AppRuntimeOptions> options,
        SettingsStore settingsStore,
        OpenAiService openAiService,
        PlaceholderArtService placeholderArtService,
        ILogger<BattleService> logger)
    {
        _options = options.Value;
        _settingsStore = settingsStore;
        _openAiService = openAiService;
        _placeholderArtService = placeholderArtService;
        _logger = logger;
    }

    public async Task<StartBattleResponse> StartBattleAsync(CancellationToken cancellationToken)
    {
        var requestedMode = await _settingsStore.GetBattleGenerationModeAsync(cancellationToken);
        if (requestedMode == BattleGenerationModes.Fixed)
        {
            return BuildFixedBattleStart(fellBackToFixed: false);
        }

        var apiKey = await _settingsStore.GetOpenAiKeyAsync(cancellationToken);
        if (_options.OpenAi.ForceMockMode || string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildFixedBattleStart(fellBackToFixed: true);
        }

        try
        {
            var generated = await _openAiService.GenerateBattleStartAsync(apiKey!, cancellationToken);
            var startingHp = RollStartingHp();
            var enemy = BuildDynamicEnemyState(generated.Enemy, startingHp);
            var enemyImage = await _openAiService.GenerateImageDataUrlAsync(apiKey!, BuildEnemySpritePrompt(generated.EnemyImagePrompt), transparentBackground: true, cancellationToken)
                ?? _placeholderArtService.CreateEnemyPortrait(enemy.Name);
            var bgImage = await _openAiService.GenerateImageDataUrlAsync(apiKey!, BuildBackgroundPrompt(generated.BgImagePrompt), transparentBackground: false, cancellationToken)
                ?? _placeholderArtService.CreateBattlefield();

            return new StartBattleResponse(
                new EncounterStateDto([enemy]),
                enemy,
                generated.WorldIntro,
                generated.OpeningLine,
                generated.EnemyImagePrompt,
                generated.BgImagePrompt,
                enemyImage,
                bgImage,
                "openai",
                BattleGenerationModes.Dynamic,
                false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to fixed battle start.");
            return BuildFixedBattleStart(fellBackToFixed: true);
        }
    }

    public async Task<AttackResponse> ResolveAttackAsync(AttackRequest request, CancellationToken cancellationToken)
    {
        if (request.Enemy is null)
        {
            throw new InvalidOperationException("Enemy payload is required.");
        }

        var playerText = request.PlayerText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(playerText))
        {
            throw new InvalidOperationException("Player text is required.");
        }

        var history = request.BattleHistory ?? Array.Empty<BattleHistoryEntry>();
        var apiKey = await _settingsStore.GetOpenAiKeyAsync(cancellationToken);
        if (ShouldUseLocalJudge(request.Enemy, apiKey))
        {
            return BuildLocalAttackResult(request.Enemy, history, playerText);
        }

        try
        {
            var generated = await _openAiService.GenerateAttackAsync(apiKey!, request.Enemy, history, playerText, cancellationToken);
            var damage = Math.Clamp(generated.Damage, 0, _options.Battle.MaxDamage);
            var nextHp = Math.Max(0, request.Enemy.CurrentHp - damage);
            var animation = nextHp <= 0 ? "defeat" : generated.Animation;
            return new AttackResponse(damage, generated.Reason, generated.EnemyLine, animation, "openai");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to local attack result.");
            return BuildLocalAttackResult(request.Enemy, history, playerText);
        }
    }

    private EnemyStateDto BuildFixedEnemyState(int startingHp)
    {
        var weakPoints = _options.Enemy.WeakPoints
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new EnemyStateDto(
            _options.Enemy.Id,
            _options.Enemy.Name,
            _options.Enemy.Species,
            _options.Enemy.Archetype,
            _options.Enemy.PersonaSummary,
            _options.Enemy.SpeechStyle,
            weakPoints,
            startingHp,
            startingHp,
            BattleGenerationModes.Fixed);
    }

    private EnemyStateDto BuildDynamicEnemyState(GeneratedEnemyBlueprint blueprint, int startingHp)
    {
        var weakPoints = blueprint.WeakPoints
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();

        if (weakPoints.Length < 3)
        {
            throw new InvalidOperationException("Generated enemy weak points were insufficient.");
        }

        var id = SanitizeId(blueprint.Id, blueprint.Name);
        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(blueprint.Name) ||
            string.IsNullOrWhiteSpace(blueprint.Species) ||
            string.IsNullOrWhiteSpace(blueprint.Archetype) ||
            string.IsNullOrWhiteSpace(blueprint.PersonaSummary) ||
            string.IsNullOrWhiteSpace(blueprint.SpeechStyle))
        {
            throw new InvalidOperationException("Generated enemy payload was incomplete.");
        }

        return new EnemyStateDto(
            id,
            blueprint.Name.Trim(),
            blueprint.Species.Trim(),
            blueprint.Archetype.Trim(),
            blueprint.PersonaSummary.Trim(),
            blueprint.SpeechStyle.Trim(),
            weakPoints,
            startingHp,
            startingHp,
            BattleGenerationModes.Dynamic);
    }

    private bool ShouldUseLocalJudge(EnemyStateDto enemy, string? apiKey)
    {
        if (_options.OpenAi.ForceMockMode)
        {
            return true;
        }

        return enemy.GenerationMode != BattleGenerationModes.Dynamic || string.IsNullOrWhiteSpace(apiKey);
    }

    private StartBattleResponse BuildFixedBattleStart(bool fellBackToFixed)
    {
        var enemy = BuildFixedEnemyState(RollStartingHp());
        var worldIntro = "夜の屋台通りでは、言葉の切れ味が強さになる。そこへ現れたのは、ソースの香りだけで通を気取る男『焼きそば食べたいマン』。見栄で膨らんだプライドを、ことばで削り落とせ。";
        var openingLine = "へっ、焼きそばを語らせたら俺の右に出るやつはいねえ。お前、まさか中身で勝負する気じゃねえよな？";
        const string enemyImagePrompt = "Pixel art JRPG enemy portrait of a boastful yakisoba-themed street yokai posing like a food champion, full body sprite, transparent background, bold silhouette, warm lantern lighting, expressive face, no readable text, no speech bubbles, no UI.";
        const string bgImagePrompt = "Pixel art JRPG battle background of a night street-stall alley, warm lanterns, tiled ground, food stalls and fantasy atmosphere, clean composition, no readable text, no typography, no speech bubbles, no UI.";
        return new StartBattleResponse(
            new EncounterStateDto([enemy]),
            enemy,
            worldIntro,
            openingLine,
            enemyImagePrompt,
            bgImagePrompt,
            _placeholderArtService.CreateEnemyPortrait(enemy.Name),
            _placeholderArtService.CreateBattlefield(),
            "fixed",
            BattleGenerationModes.Fixed,
            fellBackToFixed);
    }

    private int RollStartingHp()
    {
        var offset = StartingHpOffsets[Random.Shared.Next(StartingHpOffsets.Length)];
        return Math.Max(60, _options.Battle.StartingHp + offset);
    }

    private static string BuildEnemySpritePrompt(string prompt)
    {
        return $"{prompt.Trim()} Pixel art JRPG full-body battle sprite, transparent background, no readable text, no letters, no UI, no speech bubbles.";
    }

    private static string BuildBackgroundPrompt(string prompt)
    {
        return $"{prompt.Trim()} Pixel art JRPG battle background, no readable text, no letters, no typography, no UI, no speech bubbles.";
    }

    private AttackResponse BuildLocalAttackResult(
        EnemyStateDto enemy,
        IReadOnlyList<BattleHistoryEntry> history,
        string playerText)
    {
        var text = playerText.Trim();
        var reasonTags = new List<string>();
        var damage = 6;
        var matchedWeakPoints = MatchWeakPoints(enemy.WeakPoints, text);

        if (matchedWeakPoints.Count > 0)
        {
            damage += matchedWeakPoints.Count * 9;
            reasonTags.AddRange(matchedWeakPoints.Select(point => $"弱点「{point}」を突かれた"));
        }

        if (ContainsAny(text, "見栄", "虚勢", "背伸び", "イキ", "本当は", "わかってない", "知らない", "にわか", "浅い"))
        {
            damage += 9;
            reasonTags.Add("核心を刺された");
        }

        if (text.Length >= 24)
        {
            damage += 4;
        }

        if (history.Any(item => item.Speaker.Equals("player", StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(item.Text.Trim(), text, StringComparison.OrdinalIgnoreCase)))
        {
            damage -= 10;
            reasonTags.Add("同じ手を繰り返した");
        }

        if (text.Length <= 4)
        {
            damage -= 14;
            reasonTags.Clear();
            reasonTags.Add("言葉が短すぎて刺さらなかった");
        }

        damage = Math.Clamp(damage, 0, _options.Battle.MaxDamage);

        var nextHp = Math.Max(0, enemy.CurrentHp - damage);
        var animation = damage switch
        {
            0 => "none",
            >= 30 => "critical",
            _ => "hit",
        };

        if (nextHp == 0)
        {
            animation = "defeat";
        }

        var reason = reasonTags.Count == 0
            ? "勢いはあったが、決定打にはならなかった"
            : string.Join("、", reasonTags.Distinct());

        var enemyLine = BuildEnemyReaction(enemy, damage, nextHp, matchedWeakPoints);

        return new AttackResponse(damage, reason, enemyLine, animation, "fixed");
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> MatchWeakPoints(IEnumerable<string> weakPoints, string text)
    {
        var matches = new List<string>();
        foreach (var weakPoint in weakPoints)
        {
            var tokens = ExpandWeakPointTokens(weakPoint).ToArray();
            if (tokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                matches.Add(weakPoint);
            }
        }

        return matches.Distinct(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<string> ExpandWeakPointTokens(string weakPoint)
    {
        yield return weakPoint;

        var primaryParts = weakPoint
            .Split(['、', ',', '・', '/', '／', ' ', '　'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in primaryParts)
        {
            if (part.Length >= 2)
            {
                yield return part;
            }

            foreach (var subPart in part.Split(['の', 'を', 'は', 'が'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (subPart.Length >= 2)
                {
                    yield return subPart;
                }
            }
        }
    }

    private static string BuildEnemyReaction(EnemyStateDto enemy, int damage, int nextHp, IReadOnlyList<string> matchedWeakPoints)
    {
        var focus = matchedWeakPoints.FirstOrDefault();
        var isPolite = enemy.SpeechStyle.Contains("丁寧", StringComparison.OrdinalIgnoreCase) ||
                       enemy.SpeechStyle.Contains("です", StringComparison.OrdinalIgnoreCase);

        if (nextHp == 0)
        {
            return isPolite
                ? $"うっ……{focus ?? "そこ"}を突かれると厳しいです……。今回は認めます……。"
                : $"ぐっ……{focus ?? "そこ"}を突くのは反則だろ……もう立てねえ……。";
        }

        if (damage == 0)
        {
            return isPolite
                ? "その程度では、まだ余裕がありますよ。"
                : "へっ、その程度じゃ腹の足しにもならねえな。";
        }

        if (damage >= 30)
        {
            return isPolite
                ? $"そ、その{focus ?? "指摘"}は効きます……かなり図星です……。"
                : $"ぐっ……！ その{focus ?? "指摘"}はマジで効いた……！";
        }

        if (damage >= 18)
        {
            return isPolite
                ? $"おっと……{focus ?? "そこ"}を言われると、少し困りますね……。"
                : $"お、おい待て……{focus ?? "そこ"}はちょっと図星なんだが……。";
        }

        return isPolite
            ? "くっ……軽い言葉でも、少しは刺さりますね。"
            : "ちっ、軽口のわりには、ちょっとだけ刺さったじゃねえか。";
    }

    private static string SanitizeId(string rawId, string fallbackName)
    {
        var source = string.IsNullOrWhiteSpace(rawId) ? fallbackName : rawId;
        var builder = new List<char>();
        var previousWasHyphen = false;

        foreach (var character in source.Trim().ToLowerInvariant())
        {
            if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
            {
                builder.Add(character);
                previousWasHyphen = false;
                continue;
            }

            if (!previousWasHyphen)
            {
                builder.Add('-');
                previousWasHyphen = true;
            }
        }

        var sanitized = new string(builder.ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "dynamic-enemy" : sanitized;
    }
}
