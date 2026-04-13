namespace KotobaColiseum.Web.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string appDataRoot)
    {
        AppDataRoot = appDataRoot;
        SecretsRoot = Path.Combine(AppDataRoot, "secrets");
        SettingsFilePath = Path.Combine(AppDataRoot, "settings.json");
        OpenAiKeyPath = Path.Combine(SecretsRoot, "openai.key");
        DataProtectionRoot = Path.Combine(AppDataRoot, "data-protection");

        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(SecretsRoot);
        Directory.CreateDirectory(DataProtectionRoot);
    }

    public string AppDataRoot { get; }

    public string SecretsRoot { get; }

    public string SettingsFilePath { get; }

    public string OpenAiKeyPath { get; }

    public string DataProtectionRoot { get; }

    public static AppPaths Create(IConfiguration configuration, string contentRootPath)
    {
        var configuredPath = configuration["KOTOBA_COLISEUM_APPDATA_ROOT"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return new AppPaths(configuredPath);
        }

        foreach (var candidateRoot in GetRootCandidates(contentRootPath))
        {
            if (Directory.Exists(Path.Combine(candidateRoot, "configs")))
            {
                return new AppPaths(Path.Combine(candidateRoot, "app-data"));
            }
        }

        return new AppPaths(Path.Combine(contentRootPath, "app-data"));
    }

    private static IEnumerable<string> GetRootCandidates(string contentRootPath)
    {
        yield return Path.GetFullPath(Path.Combine(contentRootPath, "..", ".."));
        yield return Path.GetFullPath(Path.Combine(contentRootPath, ".."));
        yield return contentRootPath;
    }
}
