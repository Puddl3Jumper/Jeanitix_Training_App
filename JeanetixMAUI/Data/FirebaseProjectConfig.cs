using System.Text.Json;

namespace JeanetixMAUI.Data;

public sealed record FirebaseProjectConfig(
    string ProjectId,
    string WebApiKey,
    string? GoogleWebClientId)
{
    private static FirebaseProjectConfig? _cached;

    /// <summary>
    /// Loads config from google-services.json embedded in the app bundle (Raw resource).
    /// Place google-services.json in Resources/Raw/ in the MAUI project.
    /// </summary>
    public static async Task<FirebaseProjectConfig> LoadAsync()
    {
        if (_cached != null) return _cached;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("google-services.json");
            using var doc = JsonDocument.Parse(stream);
            _cached = ParseConfig(doc);
            return _cached;
        }
        catch
        {
            // Return a stub config when google-services.json is not present.
            return new FirebaseProjectConfig(
                ProjectId: "jeanetix-app",
                WebApiKey: string.Empty,
                GoogleWebClientId: null);
        }
    }

    private static FirebaseProjectConfig ParseConfig(JsonDocument doc)
    {
        var root = doc.RootElement;
        var projectId = root.GetProperty("project_info").GetProperty("project_id").GetString() ?? string.Empty;

        string? apiKey = null;
        string? googleWebClientId = null;

        if (root.TryGetProperty("client", out var clients) && clients.ValueKind == JsonValueKind.Array)
        {
            foreach (var client in clients.EnumerateArray())
            {
                if (client.TryGetProperty("api_key", out var apiKeys) && apiKeys.ValueKind == JsonValueKind.Array)
                {
                    foreach (var key in apiKeys.EnumerateArray())
                    {
                        apiKey = key.GetProperty("current_key").GetString();
                        if (!string.IsNullOrWhiteSpace(apiKey)) break;
                    }
                }

                if (client.TryGetProperty("oauth_client", out var oauthClients) && oauthClients.ValueKind == JsonValueKind.Array)
                {
                    foreach (var oc in oauthClients.EnumerateArray())
                    {
                        var clientType = oc.TryGetProperty("client_type", out var typeEl) ? typeEl.GetInt32() : -1;
                        if (clientType == 3)
                        {
                            googleWebClientId = oc.GetProperty("client_id").GetString();
                            if (!string.IsNullOrWhiteSpace(googleWebClientId)) break;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(apiKey)) break;
            }
        }

        return new FirebaseProjectConfig(
            ProjectId: projectId,
            WebApiKey: apiKey ?? string.Empty,
            GoogleWebClientId: googleWebClientId);
    }
}
