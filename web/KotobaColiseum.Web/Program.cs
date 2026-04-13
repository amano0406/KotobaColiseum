using KotobaColiseum.Web.Infrastructure;
using KotobaColiseum.Web.Models;
using KotobaColiseum.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

ConfigureRuntimeDefaults(builder);
builder.Configuration.AddEnvironmentVariables();

var appPaths = AppPaths.Create(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(appPaths);
builder.Services.AddOptions<AppRuntimeOptions>()
    .Bind(builder.Configuration.GetSection(AppRuntimeOptions.SectionName));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(appPaths.DataProtectionRoot))
    .SetApplicationName("KotobaColiseum");
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<PlaceholderArtService>();
builder.Services.AddSingleton<BattleService>();
builder.Services.AddHttpClient<OpenAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { ok = true }));

var settingsGroup = app.MapGroup("/api/settings");
settingsGroup.MapGet(string.Empty, async (SettingsStore settingsStore, CancellationToken cancellationToken) =>
{
    var status = await settingsStore.GetStatusAsync(cancellationToken);
    return Results.Ok(status);
});

settingsGroup.MapPost("/openai", async (
    OpenAiSaveRequest request,
    SettingsStore settingsStore,
    CancellationToken cancellationToken) =>
{
    try
    {
        await settingsStore.SaveOpenAiKeyAsync(request.ApiKey, cancellationToken);
        return Results.Ok(await settingsStore.GetStatusAsync(cancellationToken));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
});

settingsGroup.MapDelete("/openai", async (SettingsStore settingsStore, CancellationToken cancellationToken) =>
{
    await settingsStore.DeleteOpenAiKeyAsync(cancellationToken);
    return Results.NoContent();
});

settingsGroup.MapPost("/battle-mode", async (
    BattleGenerationModeSaveRequest request,
    SettingsStore settingsStore,
    CancellationToken cancellationToken) =>
{
    try
    {
        await settingsStore.SaveBattleGenerationModeAsync(request.Mode, cancellationToken);
        return Results.Ok(await settingsStore.GetStatusAsync(cancellationToken));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
});

settingsGroup.MapPost("/openai/test", async (
    SettingsStore settingsStore,
    OpenAiService openAiService,
    CancellationToken cancellationToken) =>
{
    var apiKey = await settingsStore.GetOpenAiKeyAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return Results.BadRequest(new ErrorResponse("OpenAI API key is not configured."));
    }

    var result = await openAiService.TestApiKeyAsync(apiKey, cancellationToken);
    await settingsStore.RecordOpenAiTestAsync(result.Success, cancellationToken);
    return Results.Ok(new OpenAiTestResponse(result.Success, result.Message));
});

app.MapPost("/api/start-battle", async (
    BattleService battleService,
    CancellationToken cancellationToken) =>
{
    var response = await battleService.StartBattleAsync(cancellationToken);
    return Results.Ok(response);
});

app.MapPost("/api/attack", async (
    AttackRequest request,
    BattleService battleService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await battleService.ResolveAttackAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
});

app.MapGet("/api/runtime", (IOptions<AppRuntimeOptions> options) =>
{
    return Results.Ok(new
    {
        displayName = options.Value.DisplayName,
        allowMockWithoutKey = options.Value.Battle.AllowMockWithoutKey,
        forceMockMode = options.Value.OpenAi.ForceMockMode,
    });
});

app.MapFallbackToFile("index.html");

app.Run();

static void ConfigureRuntimeDefaults(WebApplicationBuilder builder)
{
    var explicitPath = Environment.GetEnvironmentVariable("KOTOBA_COLISEUM_RUNTIME_DEFAULTS");
    if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
    {
        builder.Configuration.AddJsonFile(explicitPath, optional: false, reloadOnChange: true);
        return;
    }

    var candidate = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "configs", "runtime.defaults.json"));
    if (File.Exists(candidate))
    {
        builder.Configuration.AddJsonFile(candidate, optional: false, reloadOnChange: true);
    }
}
