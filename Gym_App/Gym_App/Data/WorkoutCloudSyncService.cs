using System.Net;
using System.Text;
using System.Text.Json;
#if ANDROID
using Android.App;
using Android.Content;
#endif
using Gym_App.Models;

namespace Gym_App.Data;

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

#if ANDROID
    private const string PrefsName = "workout_cloud_sync";
    private const string KeyLastAppliedUpdatedAt = "last_applied_updated_at";

    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static readonly SemaphoreSlim PullLock = new(1, 1);
    private static readonly SemaphoreSlim PushLock = new(1, 1);

    private static readonly object InitialPullGate = new();
    private static string? _initialPullLocalId;
    private static CancellationTokenSource? _pushDebounceCts;

    internal static void TryScheduleInitialPull(GymDatabase database)
    {
        if (database == null)
            return;

        var context = Application.Context;
        if (context == null)
            return;

        if (database.IsGuestUser)
            return;

        if (!TryGetSyncIdentifiers(context, out var localId, out _))
            return;

        lock (InitialPullGate)
        {
            if (string.Equals(_initialPullLocalId, localId, StringComparison.Ordinal))
                return;

            _initialPullLocalId = localId;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await TryPullAndApplyAsync(database, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort only.
            }
        });
    }

    internal static void NotifyLocalWorkoutsChanged(GymDatabase database)
    {
        if (database == null)
            return;

        var context = Application.Context;
        if (context == null)
            return;

        if (!TryGetSyncIdentifiers(context, out _, out _))
            return;

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
            catch
            {
                // Best-effort only.
            }
        }, token);
    }

    private static bool TryGetSyncIdentifiers(Context context, out string localId, out string projectId)
    {
        localId = string.Empty;
        projectId = string.Empty;

        if (context == null)
            return false;

        localId = AuthSessionStore.ReadLocalId(context) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(localId))
            return false;

        try
        {
            var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(context);
            projectId = config.ProjectId;
            return !string.IsNullOrWhiteSpace(projectId);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildDatabaseBaseUrl(string projectId)
    {
        // Default RTDB instance host for new Firebase projects.
        return $"https://{projectId}-default-rtdb.firebaseio.com";
    }

    private static async Task<string?> GetValidIdTokenAsync(Context context, CancellationToken cancellationToken)
    {
        var (refreshToken, idToken, expiresAtUtc) = AuthSessionStore.ReadSessionTokens(context);
        if (!string.IsNullOrWhiteSpace(idToken) && expiresAtUtc.HasValue)
        {
            if (expiresAtUtc.Value > DateTimeOffset.UtcNow.AddMinutes(1))
                return idToken;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var config = FirebaseProjectConfig.LoadFromGoogleServicesJson(context);
        var auth = new FirebaseAuthService(config);
        var refreshed = await auth.RefreshIdTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);

        var email = AuthSessionStore.ReadEmail(context) ?? string.Empty;
        var stitched = refreshed with { Email = email };
        AuthSessionStore.Save(context, stitched);
        return stitched.IdToken;
    }

    internal static async Task TryPullAndApplyAsync(GymDatabase database, CancellationToken cancellationToken)
    {
        if (database == null)
            return;

        var context = Application.Context;
        if (context == null)
            return;

        if (database.IsGuestUser)
            return;

        if (!TryGetSyncIdentifiers(context, out var localId, out var projectId))
            return;

        var idToken = await GetValidIdTokenAsync(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(idToken))
            return;

        await PullLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            var lastAppliedText = prefs?.GetString(KeyLastAppliedUpdatedAt, null);
            _ = long.TryParse(lastAppliedText, out var lastApplied);

            var baseUrl = BuildDatabaseBaseUrl(projectId);
            var url = $"{baseUrl}/users/{Uri.EscapeDataString(localId)}/workouts.json?auth={Uri.EscapeDataString(idToken)}";

            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return;

            if (string.IsNullOrWhiteSpace(body) || body.Trim() == "null")
                return;

            WorkoutSyncPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<WorkoutSyncPayload>(body, JsonOptions);
            }
            catch
            {
                return;
            }

            if (payload == null)
                return;

            if (payload.UpdatedAtUnixSeconds <= lastApplied)
                return;

            var applied = database.TryApplyRemoteWorkoutSync(payload);
            if (!applied)
                return;

            prefs?.Edit()?.PutString(KeyLastAppliedUpdatedAt, payload.UpdatedAtUnixSeconds.ToString())?.Apply();
        }
        finally
        {
            PullLock.Release();
        }
    }

    internal static async Task TryPushAsync(GymDatabase database, CancellationToken cancellationToken)
    {
        if (database == null)
            return;

        var context = Application.Context;
        if (context == null)
            return;

        if (database.IsGuestUser)
            return;

        if (!TryGetSyncIdentifiers(context, out var localId, out var projectId))
            return;

        var idToken = await GetValidIdTokenAsync(context, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(idToken))
            return;

        await PushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = database.ExportWorkoutSyncPayload();
            var json = JsonSerializer.Serialize(payload, JsonOptions);

            var baseUrl = BuildDatabaseBaseUrl(projectId);
            var url = $"{baseUrl}/users/{Uri.EscapeDataString(localId)}/workouts.json?auth={Uri.EscapeDataString(idToken)}";

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http.PutAsync(url, content, cancellationToken).ConfigureAwait(false);
            // Best effort: ignore errors.
        }
        finally
        {
            PushLock.Release();
        }
    }

#else
    internal static void TryScheduleInitialPull(GymDatabase database) { }
    internal static void NotifyLocalWorkoutsChanged(GymDatabase database) { }
#endif
}
