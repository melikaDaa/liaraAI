namespace LiaraAI.Api.Configuration;

/// <summary>
/// Loads KEY=VALUE pairs from a .env file into the ASP.NET Core configuration system.
/// This allows local development to use a .env file without requiring manual
/// environment variable setup in every terminal session.
///
/// Rules:
///   - Lines starting with # are comments.
///   - Empty lines are ignored.
///   - Whitespace around keys/values is trimmed.
///   - Values may be quoted with single or double quotes (quotes are stripped).
///   - Existing environment variables take precedence over .env values.
/// </summary>
public static class EnvFileConfigurationExtensions
{
    public static IDictionary<string, string?> LoadEnvFile(
        this IDictionary<string, string?> configuration,
        string envFilePath)
    {
        if (!File.Exists(envFilePath))
        {
            return configuration;
        }

        foreach (var rawLine in File.ReadLines(envFilePath))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // ASP.NET Core configuration uses ":" as the hierarchy separator.
            // Environment variables (and .env files) conventionally use "__".
            // Convert to match the standard configuration key format.
            key = key.Replace("__", ":");

            // Strip surrounding quotes if present.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Environment variables take precedence over .env values.
            if (!configuration.ContainsKey(key) || configuration[key] is null)
            {
                configuration[key] = value;
            }
        }

        return configuration;
    }
}
