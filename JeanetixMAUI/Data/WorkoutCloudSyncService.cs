using System.Text;
using System.Text.Json;
using JeanetixMAUI.Models;

namespace JeanetixMAUI.Data;

internal static class WorkoutCloudSyncService
{
    internal sealed class WorkoutSyncPayload
    {
        public long UpdatedAtUnixSeconds { get; set; }
        public List<WorkoutSession> WorkoutSessions { get; set; } = new();
        public int NextWorkoutSessionId { get; set; }
        public int NextWorkoutExerciseId { get; set; }
        public int NextWorkoutSetId { get; set; }
    }

    private const string KeyLastAppliedUpdatedAt = "cloud_sync_last_at";
    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
    private static readonly SemaphoreSlim PullLock = new(1, 1);
    private static readonly SemaphoreSlim PushLock = new(1, 1);
    private static CancellationTokenSource? _pushDebounceCts;

    internal static void NotifyLocalWorkoutsChanged(GymDatabase database)
    {
        _pushDebounceCts?.Cancel();
        _pushDebounceCts = new CancellationTokenSource();
        var token = _pushDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                await TryPushAsync(database, token).ConfigureAwait(false);
            }
            catch { }
        }, token);
    }

    internal static async Task TryPullAndApplyAsync(GymDatabase database, CancellationToken cancellationToken)
    {
        if (!await PullLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false))
            return;
        try
        {
            if (!TryGetSyncIdentifiers(out var localId, out var projectId)) return;
            var (_, idToken, expiresAt) = AuthSessionStore.ReadSessionTokens();
            if (string.IsNullOrWhiteSpace(idToken) || expiresAt?.ToUniversalTime() <= DateTimeOffset.UtcNow.AddMinutes(1))
            {
                var refreshToken = AuthSessionStore.ReadSessionTokens().RefreshToken;
                if (string.IsNullOrWhiteSpace(refreshToken)) return;
                var config = await FirebaseProjectConfig.LoadAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(config.WebApiKey)) return;
                var auth = new FirebaseAuthService(config);
                var refreshed = await auth.RefreshIdTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                var email = AuthSessionStore.ReadEmail() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(refreshed.Email) && !string.IsNullOrWhiteSpace(email))
                    refreshed = refreshed with { Email = email };
                AuthSessionStore.Save(refreshed);
                idToken = refreshed.IdToken;
            }
            if (string.IsNullOrWhiteSpace(idToken)) return;

            var url = $"https://{projectId}-default-rtdb.firebaseio.com/users/{Uri.EscapeDataString(localId)}/workouts.json?auth={Uri.EscapeDataString(idToken)}";
            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return;
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null") return;

            var payload = JsonSerializer.Deserialize<WorkoutSyncPayload>(json, JsonOptions);
            if (payload == null) return;

            var lastApplied = Preferences.Get(KeyLastAppliedUpdatedAt, 0L);
            if (payload.UpdatedAtUnixSeconds <= lastApplied) return;

            if (database.TryApplyRemoteWorkoutSync(payload))
                Preferences.Set(KeyLastAppliedUpdatedAt, payload.UpdatedAtUnixSeconds);
        }
        catch { }
        finally { PullLock.Release(); }
    }

    private static async Task TryPushAsync(GymDatabase database, CancellationToken cancellationToken)
    {
        if (!await PushLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false))
            return;
        try
        {
            if (!TryGetSyncIdentifiers(out var localId, out var projectId)) return;
            var (_, idToken, _) = AuthSessionStore.ReadSessionTokens();
            if (string.IsNullOrWhiteSpace(idToken)) return;

            var payload = database.ExportWorkoutSyncPayload();
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var url = $"https://{projectId}-default-rtdb.firebaseio.com/users/{Uri.EscapeDataString(localId)}/workouts.json?auth={Uri.EscapeDataString(idToken)}";
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PutAsync(url, content, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                Preferences.Set(KeyLastAppliedUpdatedAt, payload.UpdatedAtUnixSeconds);
        }
        catch { }
        finally { PushLock.Release(); }
    }

    private static bool TryGetSyncIdentifiers(out string localId, out string projectId)
    {
        localId = AuthSessionStore.ReadLocalId() ?? string.Empty;
        projectId = string.Empty;
        if (string.IsNullOrWhiteSpace(localId)) return false;
        // Load projectId synchronously from cached config (best effort).
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JeanetixApp", "firebase_project_id.txt");
            if (File.Exists(path)) projectId = File.ReadAllText(path).Trim();
        }
        catch { }
        return !string.IsNullOrWhiteSpace(projectId);
    }

    internal static async Task InitializeProjectIdCacheAsync()
    {
        try
        {
            var config = await FirebaseProjectConfig.LoadAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(config.ProjectId)) return;
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JeanetixApp", "firebase_project_id.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, config.ProjectId);
        }
        catch { }
    }
}
