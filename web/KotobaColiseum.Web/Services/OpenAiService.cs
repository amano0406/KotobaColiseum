using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KotobaColiseum.Web.Models;
using Microsoft.Extensions.Options;

namespace KotobaColiseum.Web.Services;

public sealed class OpenAiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonElement BattleStartSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "enemy": {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "id": { "type": "string" },
                "name": { "type": "string" },
                "species": { "type": "string" },
                "archetype": { "type": "string" },
                "personaSummary": { "type": "string" },
                "speechStyle": { "type": "string" },
                "weakPoints": {
                  "type": "array",
                  "minItems": 3,
                  "maxItems": 4,
                  "items": { "type": "string" }
                }
              },
              "required": ["id", "name", "species", "archetype", "personaSummary", "speechStyle", "weakPoints"]
            },
            "worldIntro": { "type": "string" },
            "openingLine": { "type": "string" },
            "enemyImagePrompt": { "type": "string" },
            "bgImagePrompt": { "type": "string" }
          },
          "required": ["enemy", "worldIntro", "openingLine", "enemyImagePrompt", "bgImagePrompt"]
        }
        """);

    private static readonly JsonElement AttackSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "damage": { "type": "integer", "minimum": 0, "maximum": 40 },
            "reason": { "type": "string" },
            "enemyLine": { "type": "string" },
            "animation": { "type": "string", "enum": ["none", "hit", "critical", "defeat"] }
          },
          "required": ["damage", "reason", "enemyLine", "animation"]
        }
        """);

    private readonly HttpClient _httpClient;
    private readonly AppRuntimeOptions _options;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(
        HttpClient httpClient,
        IOptions<AppRuntimeOptions> options,
        ILogger<OpenAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<OpenAiConnectivityResult> TestApiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, $"{_options.OpenAi.ApiBaseUrl.TrimEnd('/')}/models", apiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return new OpenAiConnectivityResult(true, "OpenAI API に接続できました。");
        }

        var detail = await ExtractErrorAsync(response, cancellationToken);
        return new OpenAiConnectivityResult(false, $"OpenAI API 接続に失敗しました。HTTP {(int)response.StatusCode}: {detail}");
    }

    public async Task<GeneratedBattleStart> GenerateBattleStartAsync(string apiKey, CancellationToken cancellationToken)
    {
        var prompt = """
        日本語の言葉バトルゲーム向けに、敵 1 体ぶんの encounter を生成してください。

        ルール:
        - 軽いRPG風
        - バカゲー寄りだが、ふざけすぎない
        - 中学生でも分かるノリ
        - 日本語は読みやすさ優先。難しい漢字や珍しい漢字は避け、迷ったらひらがなかカタカナにする
        - 言葉で殴るゲームとして成立する
        - 弱点は言葉で突きやすい短い日本語フレーズにする
        - 敵は 1 体だけ
        - worldIntro は 2〜4 文
        - openingLine は敵本人の初手セリフとして 1〜2 文
        - enemy.id は lowercase-hyphen の slug
        - enemy.species は RPG の種族名として短く分かりやすい日本語にする
        - enemy.archetype は短い肩書き
        - enemy.personaSummary は性格と弱さが分かる 1〜2 文
        - enemy.speechStyle は話し方の要約を 1 文
        - enemy.weakPoints は 3 個以上 4 個以下
        - enemyImagePrompt と bgImagePrompt は英語で、安全な画像生成プロンプト
        - 画像はどちらも 16-bit JRPG のドット絵風にする
        - 画像には readable text, letters, typography, speech bubbles, UI を入れない
        - enemyImagePrompt は敵 1 体の立ち絵向きで、transparent background を指定する
        - bgImagePrompt は戦闘背景向きに分ける
        """;

        return await CreateStructuredObjectAsync<GeneratedBattleStart>(
            apiKey,
            "battle_start_payload",
            BattleStartSchema,
            systemPrompt: "You generate one comedic but stable RPG encounter for a Japanese word-battle game and always obey the JSON schema.",
            userPrompt: prompt,
            maxOutputTokens: 700,
            maxAttempts: 3,
            validate: IsValidBattleStart,
            cancellationToken);
    }

    public async Task<GeneratedAttackResult> GenerateAttackAsync(
        string apiKey,
        EnemyStateDto enemy,
        IReadOnlyList<BattleHistoryEntry> history,
        string playerText,
        CancellationToken cancellationToken)
    {
        var historyText = history.Count == 0
            ? "なし"
            : string.Join('\n', history.Select(item =>
                $"- {item.Speaker}: {item.Text}" +
                (item.Damage.HasValue ? $" (damage={item.Damage.Value})" : string.Empty)));

        var prompt = $$"""
        あなたは日本語の言葉バトル判定役です。
        敵:
        - 名前: {{enemy.Name}}
        - 種族: {{enemy.Species}}
        - アーキタイプ: {{enemy.Archetype}}
        - 性格: {{enemy.PersonaSummary}}
        - 弱点: {{string.Join("、", enemy.WeakPoints)}}
        - 話し方: {{enemy.SpeechStyle}}
        - 現在HP: {{enemy.CurrentHp}} / {{enemy.MaxHp}}

        これまでの会話:
        {{historyText}}

        今回のプレイヤー発言:
        {{playerText}}

        次のルールで判定してください。
        - damage は 0〜40
        - 弱点に刺さるほど高ダメージ
        - 4〜6 ターン程度で終わるくらいのテンポ
        - 日本語は読みやすさ優先。難しい漢字や珍しい漢字は避け、迷ったらひらがなかカタカナにする
        - enemyLine は敵本人のセリフとして自然な日本語
        - reason は 1 文で短く
        - HP が尽きそうなら animation は defeat
        """;

        return await CreateStructuredObjectAsync<GeneratedAttackResult>(
            apiKey,
            "attack_payload",
            AttackSchema,
            systemPrompt: "You judge comedic RPG verbal attacks and always return compact Japanese JSON.",
            userPrompt: prompt,
            maxOutputTokens: 300,
            maxAttempts: 2,
            validate: result => result is { Reason.Length: > 0, EnemyLine.Length: > 0 },
            cancellationToken);
    }

    public async Task<string?> GenerateImageDataUrlAsync(
        string apiKey,
        string prompt,
        bool transparentBackground,
        CancellationToken cancellationToken)
    {
        if (!_options.OpenAi.EnableImageGeneration || string.IsNullOrWhiteSpace(_options.OpenAi.ImageModel))
        {
            return null;
        }

        var body = new
        {
            model = _options.OpenAi.ImageModel,
            prompt,
            size = _options.OpenAi.ImageSize,
            quality = transparentBackground ? "medium" : _options.OpenAi.ImageQuality,
            output_format = "png",
            background = transparentBackground ? "transparent" : "opaque",
        };
        foreach (var endpoint in new[] { "/images", "/images/generations" })
        {
            using var request = CreateRequest(HttpMethod.Post, $"{_options.OpenAi.ApiBaseUrl.TrimEnd('/')}{endpoint}", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await ExtractErrorAsync(response, cancellationToken);
                if ((response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                     response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed) &&
                    endpoint != "/images/generations")
                {
                    continue;
                }

                _logger.LogWarning("Image generation failed with status {StatusCode}: {Detail}", (int)response.StatusCode, detail);
                return null;
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            {
                return null;
            }

            var item = data[0];
            if (item.TryGetProperty("b64_json", out var b64))
            {
                return $"data:image/png;base64,{b64.GetString()}";
            }

            if (item.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
            {
                return url.GetString();
            }

            return null;
        }

        return null;
    }

    private async Task<T> CreateStructuredObjectAsync<T>(
        string apiKey,
        string schemaName,
        JsonElement schema,
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        int maxAttempts,
        Func<T, bool> validate,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var output = await CreateStructuredOutputAsync(
                    apiKey,
                    schemaName,
                    schema,
                    systemPrompt,
                    userPrompt,
                    maxOutputTokens,
                    cancellationToken);

                var parsed = JsonSerializer.Deserialize<T>(output, JsonOptions)
                    ?? throw new InvalidOperationException("OpenAI returned an empty payload.");

                if (!validate(parsed))
                {
                    throw new InvalidOperationException($"OpenAI returned an invalid payload on attempt {attempt}.");
                }

                return parsed;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Structured output attempt {Attempt} failed for schema {SchemaName}. Retrying.", attempt, schemaName);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException($"Structured output failed for schema {schemaName}.", lastError);
    }

    private async Task<string> CreateStructuredOutputAsync(
        string apiKey,
        string schemaName,
        JsonElement schema,
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model = _options.OpenAi.TextModel,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = new object[]
                    {
                        new { type = "input_text", text = systemPrompt },
                    },
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = userPrompt },
                    },
                },
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = schemaName,
                    schema,
                    strict = true,
                },
            },
            max_output_tokens = maxOutputTokens,
        };

        using var request = CreateRequest(HttpMethod.Post, $"{_options.OpenAi.ApiBaseUrl.TrimEnd('/')}/responses", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await ExtractErrorAsync(response, cancellationToken);
            throw new InvalidOperationException($"OpenAI text generation failed. HTTP {(int)response.StatusCode}: {detail}");
        }

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

        var outputText = ExtractOutputText(document.RootElement);
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new InvalidOperationException("OpenAI response did not contain usable text output.");
        }

        return outputText.Trim();
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    builder.Append(textElement.GetString());
                }
            }
        }

        return builder.ToString();
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return response.ReasonPhrase ?? "Unknown error";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // Ignore and return raw text.
        }

        return body;
    }

    private static bool IsValidBattleStart(GeneratedBattleStart result)
    {
        return result is
        {
            WorldIntro.Length: > 0,
            OpeningLine.Length: > 0,
            EnemyImagePrompt.Length: > 0,
            BgImagePrompt.Length: > 0,
            Enemy.Name.Length: > 0,
            Enemy.Species.Length: > 0,
            Enemy.Archetype.Length: > 0,
            Enemy.PersonaSummary.Length: > 0,
            Enemy.SpeechStyle.Length: > 0
        } && result.Enemy.WeakPoints.Length >= 3;
    }
}

public sealed record OpenAiConnectivityResult(bool Success, string Message);

public sealed record GeneratedBattleStart(
    GeneratedEnemyBlueprint Enemy,
    string WorldIntro,
    string OpeningLine,
    string EnemyImagePrompt,
    string BgImagePrompt);

public sealed record GeneratedEnemyBlueprint(
    string Id,
    string Name,
    string Species,
    string Archetype,
    string PersonaSummary,
    string SpeechStyle,
    string[] WeakPoints);

public sealed record GeneratedAttackResult(
    int Damage,
    string Reason,
    string EnemyLine,
    string Animation);
