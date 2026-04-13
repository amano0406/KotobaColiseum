using System.Text.Json;
using KotobaColiseum.Web.Infrastructure;
using KotobaColiseum.Web.Models;

namespace KotobaColiseum.Web.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AppPaths _appPaths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsStore(AppPaths appPaths)
    {
        _appPaths = appPaths;
    }

    public async Task<SettingsStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var metadata = await LoadMetadataCoreAsync(cancellationToken);
            return new SettingsStatusResponse(
                await HasSavedKeyCoreAsync(cancellationToken),
                NormalizeMode(metadata.BattleGenerationMode));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetOpenAiKeyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_appPaths.OpenAiKeyPath))
            {
                return null;
            }

            var key = await File.ReadAllTextAsync(_appPaths.OpenAiKeyPath, cancellationToken);
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveOpenAiKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        apiKey = apiKey.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("API key is required.");
        }

        if (!apiKey.StartsWith("sk-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("OpenAI API key must start with 'sk-'.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.WriteAllTextAsync(_appPaths.OpenAiKeyPath, apiKey, cancellationToken);
            var metadata = await LoadMetadataCoreAsync(cancellationToken);
            metadata = metadata with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            await SaveMetadataCoreAsync(metadata, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetBattleGenerationModeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var metadata = await LoadMetadataCoreAsync(cancellationToken);
            return NormalizeMode(metadata.BattleGenerationMode);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveBattleGenerationModeAsync(string mode, CancellationToken cancellationToken)
    {
        if (!BattleGenerationModes.IsSupported(mode))
        {
            throw new InvalidOperationException("Battle generation mode must be 'fixed' or 'dynamic'.");
        }

        mode = NormalizeMode(mode);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var metadata = await LoadMetadataCoreAsync(cancellationToken);
            metadata = metadata with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                BattleGenerationMode = mode,
            };
            await SaveMetadataCoreAsync(metadata, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteOpenAiKeyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_appPaths.OpenAiKeyPath))
            {
                File.Delete(_appPaths.OpenAiKeyPath);
            }

            var metadata = await LoadMetadataCoreAsync(cancellationToken);
            metadata = metadata with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            await SaveMetadataCoreAsync(metadata, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordOpenAiTestAsync(bool success, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var metadata = await LoadMetadataCoreAsync(cancellationToken);
            metadata = success
                ? metadata with { LastSuccessfulOpenAiTestAtUtc = DateTimeOffset.UtcNow }
                : metadata with { LastFailedOpenAiTestAtUtc = DateTimeOffset.UtcNow };
            await SaveMetadataCoreAsync(metadata, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<LocalSettingsMetadata> LoadMetadataCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_appPaths.SettingsFilePath))
        {
            return new LocalSettingsMetadata();
        }

        await using var stream = File.OpenRead(_appPaths.SettingsFilePath);
        return await JsonSerializer.DeserializeAsync<LocalSettingsMetadata>(stream, JsonOptions, cancellationToken)
            ?? new LocalSettingsMetadata();
    }

    private async Task SaveMetadataCoreAsync(LocalSettingsMetadata metadata, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_appPaths.SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, metadata, JsonOptions, cancellationToken);
    }

    private Task<bool> HasSavedKeyCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_appPaths.OpenAiKeyPath))
        {
            return Task.FromResult(false);
        }

        var key = File.ReadAllText(_appPaths.OpenAiKeyPath).Trim();
        return Task.FromResult(!string.IsNullOrWhiteSpace(key));
    }

    private sealed record LocalSettingsMetadata(
        DateTimeOffset? UpdatedAtUtc = null,
        DateTimeOffset? LastSuccessfulOpenAiTestAtUtc = null,
        DateTimeOffset? LastFailedOpenAiTestAtUtc = null,
        string BattleGenerationMode = BattleGenerationModes.Dynamic);

    private static string NormalizeMode(string? mode)
    {
        return BattleGenerationModes.Normalize(mode);
    }
}
