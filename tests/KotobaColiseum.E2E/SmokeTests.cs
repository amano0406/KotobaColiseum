using FluentAssertions;
using Microsoft.Playwright;

namespace KotobaColiseum.E2E;

public sealed class SmokeTests
{
    [Fact]
    public async Task DynamicModeCanSaveSettingsAndShowsFallbackBadge()
    {
        await using var session = await OpenPageAsync();
        var page = session.Page;

        (await page.TitleAsync()).Should().Contain("ことばコロシアム");
        await page.SelectOptionAsync("#battle-generation-mode", "dynamic");
        await page.ClickAsync("#save-mode-button");
        await ExpectTextAsync(page, "#battle-mode-summary", "dynamic");

        await page.FillAsync("#openai-key-input", "sk-local-test-1234567890abcdef");
        await page.ClickAsync("#save-key-button");
        await ExpectTextAsync(page, "#settings-panel-badge", "設定済み");

        await page.ClickAsync("#start-battle-button");
        await page.WaitForSelectorAsync("#battle-panel:not(.hidden)");
        await ExpectTextAsync(page, "#provider-badge", "dynamic -> fixed");
        await ExpectTextAsync(page, "#enemy-name", "焼きそば食べたいマン");
    }

    [Fact]
    public async Task FixedModeStillStartsBattleAndWins()
    {
        await using var session = await OpenPageAsync();
        var page = session.Page;

        await page.SelectOptionAsync("#battle-generation-mode", "fixed");
        await page.ClickAsync("#save-mode-button");
        await ExpectTextAsync(page, "#battle-mode-summary", "fixed");

        await page.ClickAsync("#start-battle-button");
        await page.WaitForSelectorAsync("#battle-panel:not(.hidden)");
        await ExpectTextAsync(page, "#provider-badge", "fixed");
        await ExpectTextAsync(page, "#enemy-persona-summary", "屋台妖人");

        var attacks = new[]
        {
            "お前、本当は焼きそばの違いもわかってないのに見栄で語ってるだけだろ。",
            "カップ焼きそばの知識だけでイキるな、食の浅さが丸見えだ。",
            "知ったかぶりで通ぶるな、ソースの名前を並べても中身を知らないにわかだろ。",
            "背伸びした見栄っぱりが語ってるだけで、本当は何も分かってないのが出てるぞ。",
            "見栄と知ったかぶりだけで食を語るな、浅さが全部出てるぞ。",
            "お前の焼きそば論は虚勢だけだ。本当は味も違いもわかってない。"
        };

        foreach (var attack in attacks)
        {
            var hpBefore = (await page.TextContentAsync("#hp-text"))?.Trim();
            if (hpBefore?.StartsWith("0", StringComparison.Ordinal) == true) {
                break;
            }
            await page.FillAsync("#attack-input", attack);
            await page.ClickAsync("#attack-button");
            await page.WaitForTimeoutAsync(800);
        }

        var hpText = (await page.TextContentAsync("#hp-text"))?.Trim();
        hpText.Should().NotBeNullOrWhiteSpace();

        var currentHp = int.Parse(hpText!.Split('/')[0].Trim());
        currentHp.Should().Be(0);

        var logText = await page.Locator("#battle-log").InnerTextAsync();
        logText.Should().Contain("あなた");
        logText.Should().Contain("敵");
        logText.Should().Contain("damage");
    }

    private static async Task<BrowserSession> OpenPageAsync()
    {
        var baseUrl = Environment.GetEnvironmentVariable("KOTOBA_COLISEUM_E2E_BASE_URL")
            ?? "http://127.0.0.1:5072";

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(baseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
        });

        return new BrowserSession(playwright, browser, page);
    }

    private static async Task ExpectTextAsync(IPage page, string selector, string expected)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var text = (await page.TextContentAsync(selector))?.Trim();
            if (text?.Contains(expected, StringComparison.Ordinal) == true)
            {
                return;
            }

            await page.WaitForTimeoutAsync(200);
        }

        var finalText = (await page.TextContentAsync(selector))?.Trim();
        finalText.Should().Contain(expected);
    }

    private sealed class BrowserSession : IAsyncDisposable
    {
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;

        public BrowserSession(IPlaywright playwright, IBrowser browser, IPage page)
        {
            _playwright = playwright;
            _browser = browser;
            Page = page;
        }

        public IPage Page { get; }

        public async ValueTask DisposeAsync()
        {
            await Page.CloseAsync();
            await _browser.DisposeAsync();
            _playwright.Dispose();
        }
    }
}
