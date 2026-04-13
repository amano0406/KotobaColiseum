using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using KotobaColiseum.Web.Infrastructure;
using KotobaColiseum.Web.Models;
using KotobaColiseum.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KotobaColiseum.E2E;

public sealed class BattleServiceTests
{
    [Fact]
    public async Task FixedModeStartBattleReturnsFixedEncounterWithoutFallback()
    {
        var appDataRoot = CreateTempDirectory();
        try
        {
            var settingsStore = new SettingsStore(new AppPaths(appDataRoot));
            await settingsStore.SaveBattleGenerationModeAsync(BattleGenerationModes.Fixed, CancellationToken.None);

            var options = CreateOptions();
            var httpClient = new HttpClient(new StubOpenAiHandler(shouldFailResponses: false));
            var openAiService = new OpenAiService(httpClient, options, NullLogger<OpenAiService>.Instance);
            var battleService = new BattleService(
                options,
                settingsStore,
                openAiService,
                new PlaceholderArtService(),
                NullLogger<BattleService>.Instance);

            var result = await battleService.StartBattleAsync(CancellationToken.None);

            result.GenerationMode.Should().Be(BattleGenerationModes.Fixed);
            result.FellBackToFixed.Should().BeFalse();
            result.Provider.Should().Be("fixed");
            result.Enemy.Name.Should().Be("焼きそば食べたいマン");
            result.Enemy.Species.Should().Be("屋台妖人");
            result.Enemy.WeakPoints.Should().Equal("見栄", "知ったかぶり", "食の浅さ");
            result.Enemy.MaxHp.Should().BeInRange(84, 116);
            result.Enemy.CurrentHp.Should().Be(result.Enemy.MaxHp);
        }
        finally
        {
            DeleteTempDirectory(appDataRoot);
        }
    }

    [Fact]
    public async Task DynamicModeStartBattleReturnsGeneratedEncounter()
    {
        var appDataRoot = CreateTempDirectory();
        try
        {
            var settingsStore = new SettingsStore(new AppPaths(appDataRoot));
            await settingsStore.SaveOpenAiKeyAsync("sk-test-1234567890abcdef", CancellationToken.None);
            await settingsStore.SaveBattleGenerationModeAsync(BattleGenerationModes.Dynamic, CancellationToken.None);

            var options = CreateOptions();
            var httpClient = new HttpClient(new StubOpenAiHandler(shouldFailResponses: false));
            var openAiService = new OpenAiService(httpClient, options, NullLogger<OpenAiService>.Instance);
            var battleService = new BattleService(
                options,
                settingsStore,
                openAiService,
                new PlaceholderArtService(),
                NullLogger<BattleService>.Instance);

            var result = await battleService.StartBattleAsync(CancellationToken.None);

            result.GenerationMode.Should().Be(BattleGenerationModes.Dynamic);
            result.FellBackToFixed.Should().BeFalse();
            result.Provider.Should().Be("openai");
            result.Encounter.Enemies.Should().ContainSingle();
            result.Enemy.Name.Should().Be("ラーメン説教伯");
            result.Enemy.Species.Should().Be("麺霊貴族");
            result.Enemy.WeakPoints.Should().Contain(new[] { "知ったかぶり", "味の浅さ", "見栄" });
            result.WorldIntro.Should().NotBeNullOrWhiteSpace();
            result.OpeningLine.Should().NotBeNullOrWhiteSpace();
            result.EnemyImagePrompt.Should().NotBeNullOrWhiteSpace();
            result.BgImagePrompt.Should().NotBeNullOrWhiteSpace();
            result.EnemyImage.Should().StartWith("data:image/png;base64,");
            result.BgImage.Should().StartWith("data:image/png;base64,");
            result.Enemy.MaxHp.Should().BeInRange(84, 116);
            result.Enemy.CurrentHp.Should().Be(result.Enemy.MaxHp);
        }
        finally
        {
            DeleteTempDirectory(appDataRoot);
        }
    }

    [Fact]
    public async Task DynamicModeFallsBackToFixedWhenOpenAiFails()
    {
        var appDataRoot = CreateTempDirectory();
        try
        {
            var settingsStore = new SettingsStore(new AppPaths(appDataRoot));
            await settingsStore.SaveOpenAiKeyAsync("sk-test-1234567890abcdef", CancellationToken.None);
            await settingsStore.SaveBattleGenerationModeAsync(BattleGenerationModes.Dynamic, CancellationToken.None);

            var options = CreateOptions();
            var httpClient = new HttpClient(new StubOpenAiHandler(shouldFailResponses: true));
            var openAiService = new OpenAiService(httpClient, options, NullLogger<OpenAiService>.Instance);
            var battleService = new BattleService(
                options,
                settingsStore,
                openAiService,
                new PlaceholderArtService(),
                NullLogger<BattleService>.Instance);

            var result = await battleService.StartBattleAsync(CancellationToken.None);

            result.GenerationMode.Should().Be(BattleGenerationModes.Fixed);
            result.FellBackToFixed.Should().BeTrue();
            result.Provider.Should().Be("fixed");
            result.Enemy.Name.Should().Be("焼きそば食べたいマン");
        }
        finally
        {
            DeleteTempDirectory(appDataRoot);
        }
    }

    private static IOptions<AppRuntimeOptions> CreateOptions()
    {
        return Options.Create(new AppRuntimeOptions
        {
            OpenAi = new OpenAiRuntimeOptions
            {
                ApiBaseUrl = "https://unit.test/v1",
                TextModel = "gpt-4o-mini",
                ImageModel = "gpt-image-1-mini",
                EnableImageGeneration = true,
            },
            Battle = new BattleRuntimeOptions
            {
                StartingHp = 100,
                MaxDamage = 40,
                AllowMockWithoutKey = true,
            },
            Enemy = new EnemyProfile
            {
                Id = "yakisoba-man",
                Name = "焼きそば食べたいマン",
                Species = "屋台妖人",
                Archetype = "屋台見栄っぱり",
                PersonaSummary = "見栄っ張りで、すぐムキになるが、芯は弱い。",
                SpeechStyle = "少し荒い口調。追い詰められると語尾が崩れる。",
                WeakPoints = ["見栄", "知ったかぶり", "食の浅さ"],
            },
        });
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "KotobaColiseumTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class StubOpenAiHandler : HttpMessageHandler
    {
        private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAASsJTYQAAAAASUVORK5CYII=";
        private readonly bool _shouldFailResponses;

        public StubOpenAiHandler(bool shouldFailResponses)
        {
            _shouldFailResponses = shouldFailResponses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.EndsWith("/responses", StringComparison.Ordinal))
            {
                if (_shouldFailResponses)
                {
                    return Task.FromResult(CreateJsonResponse(
                        HttpStatusCode.InternalServerError,
                        """{"error":{"message":"stub failure"}}"""));
                }

                var payload = """
                {
                  "output_text": "{\"enemy\":{\"id\":\"ramen-prince\",\"name\":\"ラーメン説教伯\",\"species\":\"麺霊貴族\",\"archetype\":\"講釈グルメ騎士\",\"personaSummary\":\"知識を盛って語るが、核心を突かれると弱い。味の違いを分かったふりで押し切ろうとする。\",\"speechStyle\":\"やや偉そうで、語尾に余裕を作るが、追い詰められると早口になる。\",\"weakPoints\":[\"知ったかぶり\",\"味の浅さ\",\"見栄\"]},\"worldIntro\":\"屋台通りの中央で、言葉が武器になる夜の決闘が始まる。今日の相手は、ラーメン知識を盛って語る講釈グルメ騎士だ。\",\"openingLine\":\"ふっ、君はまだ本物の一杯を知らないようだね。では、私の講釈で目を開かせてあげよう。\",\"enemyImagePrompt\":\"Pixel art JRPG portrait of a boastful ramen-themed fantasy knight, full body sprite, transparent background, no readable text, no UI.\",\"bgImagePrompt\":\"Pixel art JRPG battle background of a lively night food alley, glowing lanterns, ramen stalls, no readable text, no UI.\"}"
                }
                """;

                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, payload));
            }

            if (path.EndsWith("/images/generations", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateJsonResponse(
                    HttpStatusCode.OK,
                    $$"""{"data":[{"b64_json":"{{TinyPngBase64}}"}]}"""));
            }

            if (path.EndsWith("/models", StringComparison.Ordinal))
            {
                return Task.FromResult(CreateJsonResponse(HttpStatusCode.OK, """{"data":[{"id":"gpt-4o-mini"}]}"""));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                Headers =
                {
                    { "x-test-handler", "stub-openai" },
                },
            };
        }
    }
}
