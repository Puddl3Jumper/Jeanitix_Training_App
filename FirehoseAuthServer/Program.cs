using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FirehoseAuthOptions>(builder.Configuration.GetSection(FirehoseAuthOptions.SectionName));
var authOptions = builder.Configuration.GetSection(FirehoseAuthOptions.SectionName).Get<FirehoseAuthOptions>() ?? new FirehoseAuthOptions();

ApplyCredentialOverrides(authOptions);
ValidateOptions(authOptions);

builder.WebHost.UseUrls(authOptions.ListenUrl);

builder.Services
	.AddAuthentication(options =>
	{
		options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
		options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
	})
	.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
	{
		options.ClientId = authOptions.GoogleClientId;
		options.ClientSecret = authOptions.GoogleClientSecret;
		options.CallbackPath = "/signin-google";
		options.SaveTokens = true;
		options.Scope.Add("openid");
		options.Scope.Add("profile");
		options.Scope.Add("email");
	});

builder.Services.AddAuthorization();
builder.Services.AddSingleton<AuthorizationCodeStore>();
builder.Services.AddSingleton<AccessTokenStore>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", (IOptions<FirehoseAuthOptions> options) =>
{
	var config = options.Value;
	return Results.Ok(new
	{
		name = "FirehoseAuthServer",
		issuer = config.PublicOrigin,
		authorize = $"{config.PublicOrigin}/connect/authorize",
		token = $"{config.PublicOrigin}/connect/token",
		userinfo = $"{config.PublicOrigin}/connect/userinfo"
	});
});

app.MapGet("/.well-known/openid-configuration", (IOptions<FirehoseAuthOptions> options) =>
{
	var config = options.Value;
	return Results.Ok(new
	{
		issuer = config.PublicOrigin,
		authorization_endpoint = $"{config.PublicOrigin}/connect/authorize",
		token_endpoint = $"{config.PublicOrigin}/connect/token",
		userinfo_endpoint = $"{config.PublicOrigin}/connect/userinfo",
		jwks_uri = $"{config.PublicOrigin}/.well-known/jwks.json",
		response_types_supported = new[] { "code" },
		subject_types_supported = new[] { "public" },
		id_token_signing_alg_values_supported = new[] { "HS256" },
		scopes_supported = new[] { "openid", "profile", "email" },
		token_endpoint_auth_methods_supported = new[] { "none" }
	});
});

app.MapGet("/.well-known/jwks.json", () => Results.Ok(new { keys = Array.Empty<object>() }));

app.MapGet("/connect/authorize", async (
	HttpContext context,
	IOptions<FirehoseAuthOptions> options,
	AuthorizationCodeStore authCodeStore) =>
{
	var config = options.Value;
	var query = context.Request.Query;

	var clientId = query["client_id"].ToString();
	var redirectUri = query["redirect_uri"].ToString();
	var state = query["state"].ToString();
	var scope = query["scope"].ToString();
	var responseType = query["response_type"].ToString();
	var codeChallenge = query["code_challenge"].ToString();
	var codeChallengeMethod = query["code_challenge_method"].ToString();

	if (!string.Equals(clientId, config.ClientId, StringComparison.Ordinal))
	{
		return Results.BadRequest(new { error = "invalid_client", error_description = "Unknown client_id." });
	}

	if (!IsAllowedRedirect(config, redirectUri))
	{
		return Results.BadRequest(new { error = "invalid_request", error_description = "redirect_uri is not allowed." });
	}

	if (!string.Equals(responseType, "code", StringComparison.OrdinalIgnoreCase))
	{
		return Results.BadRequest(new { error = "unsupported_response_type" });
	}

	if (string.IsNullOrWhiteSpace(scope) || !scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("openid", StringComparer.OrdinalIgnoreCase))
	{
		return Results.BadRequest(new { error = "invalid_scope", error_description = "openid scope is required." });
	}

	if (context.User?.Identity?.IsAuthenticated != true)
	{
		var properties = new AuthenticationProperties
		{
			RedirectUri = context.Request.Path + context.Request.QueryString
		};
		return Results.Challenge(properties, new[] { GoogleDefaults.AuthenticationScheme });
	}

	var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub") ?? Guid.NewGuid().ToString("N");
	var email = context.User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
	var name = context.User.FindFirstValue(ClaimTypes.Name) ?? email;

	var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
		.TrimEnd('=')
		.Replace('+', '-')
		.Replace('/', '_');

	authCodeStore.Save(code, new AuthorizationCodeEntry(
		clientId,
		redirectUri,
		scope,
		subject,
		email,
		name,
		codeChallenge,
		codeChallengeMethod,
		DateTimeOffset.UtcNow.AddMinutes(5)));

	var callbackUrl = QueryHelpers.AddQueryString(redirectUri, new Dictionary<string, string?>
	{
		["code"] = code,
		["state"] = state
	});

	return Results.Redirect(callbackUrl);
});

app.MapPost("/connect/token", async (
	HttpContext context,
	IOptions<FirehoseAuthOptions> options,
	AuthorizationCodeStore authCodeStore,
	AccessTokenStore accessTokenStore) =>
{
	var config = options.Value;
	var form = await context.Request.ReadFormAsync();

	var grantType = form["grant_type"].ToString();
	var code = form["code"].ToString();
	var clientId = form["client_id"].ToString();
	var redirectUri = form["redirect_uri"].ToString();
	var codeVerifier = form["code_verifier"].ToString();

	if (!string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
	{
		return Results.BadRequest(new { error = "unsupported_grant_type" });
	}

	if (!string.Equals(clientId, config.ClientId, StringComparison.Ordinal))
	{
		return Results.BadRequest(new { error = "invalid_client" });
	}

	if (!authCodeStore.TryRedeem(code, out var entry) || entry == null)
	{
		return Results.BadRequest(new { error = "invalid_grant" });
	}

	if (entry.ExpiresAt < DateTimeOffset.UtcNow)
	{
		return Results.BadRequest(new { error = "invalid_grant", error_description = "Authorization code expired." });
	}

	if (!string.Equals(entry.RedirectUri, redirectUri, StringComparison.Ordinal) ||
		!string.Equals(entry.ClientId, clientId, StringComparison.Ordinal))
	{
		return Results.BadRequest(new { error = "invalid_grant" });
	}

	if (!ValidatePkce(entry.CodeChallenge, entry.CodeChallengeMethod, codeVerifier))
	{
		return Results.BadRequest(new { error = "invalid_grant", error_description = "PKCE verification failed." });
	}

	var accessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
		.TrimEnd('=')
		.Replace('+', '-')
		.Replace('/', '_');

	accessTokenStore.Save(accessToken, new AccessTokenEntry(
		entry.Subject,
		entry.Email,
		entry.Name,
		entry.Scope,
		DateTimeOffset.UtcNow.AddHours(1)));

	var idToken = CreateIdToken(config, entry.Subject, entry.Email, entry.Name);

	return Results.Ok(new
	{
		token_type = "Bearer",
		access_token = accessToken,
		expires_in = 3600,
		scope = entry.Scope,
		id_token = idToken
	});
});

app.MapGet("/connect/userinfo", (
	HttpContext context,
	AccessTokenStore accessTokenStore) =>
{
	var authHeader = context.Request.Headers.Authorization.ToString();
	if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
	{
		return Results.Unauthorized();
	}

	var token = authHeader["Bearer ".Length..].Trim();
	if (!accessTokenStore.TryGet(token, out var entry) || entry == null || entry.ExpiresAt < DateTimeOffset.UtcNow)
	{
		return Results.Unauthorized();
	}

	return Results.Ok(new
	{
		sub = entry.Subject,
		email = entry.Email,
		email_verified = !string.IsNullOrWhiteSpace(entry.Email),
		name = entry.Name
	});
});

app.Run();

static bool IsAllowedRedirect(FirehoseAuthOptions options, string redirectUri)
{
	if (string.IsNullOrWhiteSpace(redirectUri))
	{
		return false;
	}

	return options.AllowedRedirectUris.Any(uri => string.Equals(uri, redirectUri, StringComparison.Ordinal));
}

static bool ValidatePkce(string? codeChallenge, string? codeChallengeMethod, string codeVerifier)
{
	if (string.IsNullOrWhiteSpace(codeChallenge))
	{
		return true;
	}

	if (string.IsNullOrWhiteSpace(codeVerifier))
	{
		return false;
	}

	if (string.Equals(codeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase))
	{
		var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
		var calculated = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		return string.Equals(calculated, codeChallenge, StringComparison.Ordinal);
	}

	if (string.IsNullOrWhiteSpace(codeChallengeMethod) || string.Equals(codeChallengeMethod, "plain", StringComparison.OrdinalIgnoreCase))
	{
		return string.Equals(codeVerifier, codeChallenge, StringComparison.Ordinal);
	}

	return false;
}

static string CreateIdToken(FirehoseAuthOptions options, string subject, string email, string name)
{
	var signingKeyBytes = Encoding.UTF8.GetBytes(options.JwtSigningKey.PadRight(32, '0'));
	var key = new SymmetricSecurityKey(signingKeyBytes);
	var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
	var now = DateTime.UtcNow;

	var claims = new List<Claim>
	{
		new(JwtRegisteredClaimNames.Sub, subject),
		new(JwtRegisteredClaimNames.Email, email),
		new(JwtRegisteredClaimNames.Name, name)
	};

	var jwt = new JwtSecurityToken(
		issuer: options.PublicOrigin,
		audience: options.ClientId,
		claims: claims,
		notBefore: now,
		expires: now.AddHours(1),
		signingCredentials: creds);

	return new JwtSecurityTokenHandler().WriteToken(jwt);
}

static void ApplyCredentialOverrides(FirehoseAuthOptions options)
{
	var googleClientId = Environment.GetEnvironmentVariable("FIREHOSE_GOOGLE_CLIENT_ID");
	var googleClientSecret = Environment.GetEnvironmentVariable("FIREHOSE_GOOGLE_CLIENT_SECRET");

	if (!string.IsNullOrWhiteSpace(googleClientId))
	{
		options.GoogleClientId = googleClientId;
	}

	if (!string.IsNullOrWhiteSpace(googleClientSecret))
	{
		options.GoogleClientSecret = googleClientSecret;
	}
}

static void ValidateOptions(FirehoseAuthOptions options)
{
	if (IsPlaceholderGoogleClientId(options.GoogleClientId) || IsPlaceholderGoogleClientSecret(options.GoogleClientSecret))
	{
		throw new InvalidOperationException(
			"Google OAuth is not configured. Set FirehoseAuth:GoogleClientId and FirehoseAuth:GoogleClientSecret in appsettings.Development.json or set FIREHOSE_GOOGLE_CLIENT_ID and FIREHOSE_GOOGLE_CLIENT_SECRET environment variables.");
	}
}

static bool IsPlaceholderGoogleClientId(string? value)
{
	if (string.IsNullOrWhiteSpace(value))
	{
		return true;
	}

	return value.Contains("YOUR_GOOGLE_CLIENT_ID", StringComparison.OrdinalIgnoreCase);
}

static bool IsPlaceholderGoogleClientSecret(string? value)
{
	if (string.IsNullOrWhiteSpace(value))
	{
		return true;
	}

	return value.Contains("YOUR_GOOGLE_CLIENT_SECRET", StringComparison.OrdinalIgnoreCase);
}

sealed record AuthorizationCodeEntry(
	string ClientId,
	string RedirectUri,
	string Scope,
	string Subject,
	string Email,
	string Name,
	string? CodeChallenge,
	string? CodeChallengeMethod,
	DateTimeOffset ExpiresAt);

sealed class AuthorizationCodeStore
{
	private readonly ConcurrentDictionary<string, AuthorizationCodeEntry> _codes = new();

	public void Save(string code, AuthorizationCodeEntry entry) => _codes[code] = entry;

	public bool TryRedeem(string code, out AuthorizationCodeEntry? entry)
	{
		if (_codes.TryRemove(code, out var found))
		{
			entry = found;
			return true;
		}

		entry = null;
		return false;
	}
}

sealed record AccessTokenEntry(
	string Subject,
	string Email,
	string Name,
	string Scope,
	DateTimeOffset ExpiresAt);

sealed class AccessTokenStore
{
	private readonly ConcurrentDictionary<string, AccessTokenEntry> _tokens = new();

	public void Save(string accessToken, AccessTokenEntry entry) => _tokens[accessToken] = entry;

	public bool TryGet(string accessToken, out AccessTokenEntry? entry)
	{
		if (_tokens.TryGetValue(accessToken, out var found))
		{
			entry = found;
			return true;
		}

		entry = null;
		return false;
	}
}

sealed class FirehoseAuthOptions
{
	public const string SectionName = "FirehoseAuth";

	public string ListenUrl { get; set; } = "http://0.0.0.0:5076";
	public string PublicOrigin { get; set; } = "http://10.0.2.2:5076";
	public string ClientId { get; set; } = "gym-app-mobile";
	public string JwtSigningKey { get; set; } = "replace-with-long-random-signing-key";
	public string GoogleClientId { get; set; } = "";
	public string GoogleClientSecret { get; set; } = "";
	public string[] AllowedRedirectUris { get; set; } = new[] { "com.leiyu.GymJournal://oauth2redirect" };
}
