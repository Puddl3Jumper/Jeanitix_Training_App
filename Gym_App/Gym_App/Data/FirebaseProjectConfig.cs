using Android.Content;
using System.Text.Json;

namespace Gym_App.Data;

public sealed record FirebaseProjectConfig(
    string ProjectId,
    string AndroidPackageName,
    string WebApiKey,
    string? GoogleWebClientId)
{
    public static FirebaseProjectConfig LoadFromGoogleServicesJson(Context context)
    {
        using var stream = context.Assets?.Open("google-services.json")
            ?? throw new InvalidOperationException("google-services.json was not found in Android assets. Ensure the .csproj includes <AndroidAsset Include=\"google-services.json\" />.");

        using var doc = JsonDocument.Parse(stream);

        var root = doc.RootElement;

        var projectId = root.GetProperty("project_info").GetProperty("project_id").GetString();
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("Firebase project_id is missing in google-services.json.");

        var packageName = context.PackageName;

        string? apiKey = null;
        string? googleWebClientId = null;
        string? matchedPackageName = null;

        if (root.TryGetProperty("client", out var clients) && clients.ValueKind == JsonValueKind.Array)
        {
            foreach (var client in clients.EnumerateArray())
            {
                var clientInfo = client.GetProperty("client_info");
                var androidInfo = clientInfo.GetProperty("android_client_info");
                var clientPackage = androidInfo.GetProperty("package_name").GetString();

                if (!string.Equals(clientPackage, packageName, StringComparison.Ordinal))
                    continue;

                matchedPackageName = clientPackage;

                if (client.TryGetProperty("api_key", out var apiKeys) && apiKeys.ValueKind == JsonValueKind.Array)
                {
                    foreach (var key in apiKeys.EnumerateArray())
                    {
                        apiKey = key.GetProperty("current_key").GetString();
                        if (!string.IsNullOrWhiteSpace(apiKey))
                            break;
                    }
                }

                if (client.TryGetProperty("oauth_client", out var oauthClients) && oauthClients.ValueKind == JsonValueKind.Array)
                {
                    foreach (var oc in oauthClients.EnumerateArray())
                    {
                        var clientType = oc.TryGetProperty("client_type", out var typeEl) ? typeEl.GetInt32() : -1;
                        // client_type=3 is typically the Web client id used to request an ID token.
                        if (clientType == 3)
                        {
                            googleWebClientId = oc.GetProperty("client_id").GetString();
                            if (!string.IsNullOrWhiteSpace(googleWebClientId))
                                break;
                        }
                    }
                }

                break;
            }
        }

        if (string.IsNullOrWhiteSpace(matchedPackageName))
            throw new InvalidOperationException($"google-services.json does not contain a client entry for package '{packageName}'.");

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Firebase Web API key (api_key/current_key) is missing in google-services.json.");

        return new FirebaseProjectConfig(
            ProjectId: projectId,
            AndroidPackageName: matchedPackageName,
            WebApiKey: apiKey,
            GoogleWebClientId: googleWebClientId);
    }
}
