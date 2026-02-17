# Firehose Auth Server (Google Login)

This server provides OIDC-compatible endpoints for the Android app:

- `/.well-known/openid-configuration`
- `/connect/authorize`
- `/connect/token`
- `/connect/userinfo`

It uses Google as the upstream identity provider and returns app tokens to the mobile client.

## 1) Configure Google OAuth

In Google Cloud Console, create an **OAuth 2.0 Client ID** of type **Web application**.

Set Authorized redirect URI:

- `http://localhost:5076/signin-google`

Copy the Client ID and Client Secret.

## 2) Configure server settings

Edit `appsettings.Development.json`:

- `FirehoseAuth:GoogleClientId`
- `FirehoseAuth:GoogleClientSecret`
- `FirehoseAuth:JwtSigningKey` (32+ chars)

Or set environment variables instead of editing config:

```bash
export FIREHOSE_GOOGLE_CLIENT_ID="your-client-id.apps.googleusercontent.com"
export FIREHOSE_GOOGLE_CLIENT_SECRET="your-client-secret"
```

If Google credentials are missing or still placeholders, the server now exits at startup with a clear configuration error.

Preferred (no secrets in repo): use `dotnet user-secrets`:

```bash
dotnet user-secrets --project FirehoseAuthServer/FirehoseAuthServer.csproj set "FirehoseAuth:GoogleClientId" "your-client-id.apps.googleusercontent.com"
dotnet user-secrets --project FirehoseAuthServer/FirehoseAuthServer.csproj set "FirehoseAuth:GoogleClientSecret" "your-client-secret"
```

Defaults already match Android emulator access:

- `ListenUrl`: `http://0.0.0.0:5076`
- `PublicOrigin`: `http://10.0.2.2:5076`
- `ClientId`: `gym-app-mobile`
- Allowed redirect: `com.leiyu.GymJournal://oauth2redirect`

## 3) Run the server

```bash
dotnet run --project FirehoseAuthServer/FirehoseAuthServer.csproj
```

If port `5076` is already in use:

```bash
lsof -nP -iTCP:5076 -sTCP:LISTEN
kill <PID>
```

## 4) Android app values

`Gym_App/Gym_App/Resources/values/strings.xml` is set to:

- `oauth_client_id = gym-app-mobile`
- `oauth_authority_url = http://10.0.2.2:5076`
- `oauth_redirect_url = com.leiyu.GymJournal://oauth2redirect`

## Notes

- Current setup is intended for development/local testing.
- App OIDC policy currently allows insecure HTTP and unsigned identity-token acceptance for local flow compatibility.
- For production, switch to HTTPS and full signature validation.
