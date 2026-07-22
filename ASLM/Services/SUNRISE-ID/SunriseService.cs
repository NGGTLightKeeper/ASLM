// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services.Sunrise
{
    /// <summary>
    /// Provides SUNRISE ID storage, endpoint resolution, authentication, and web API requests.
    /// </summary>
    public sealed class SunriseService : IDisposable
    {
        private const int CurrentFileVersion = 1;

        private const string DomainsFileName = "SUNRISE_Domains.json";
        private const string UrlsFileName = "SUNRISE_URLs.json";
        private const string TokensFileName = "SUNRISE_Tokens.json";
        private const string UserDataFileName = "SUNRISE_UserData.json";

        private const string DefaultDomainType = "NGGT_OSS";

        public const string SignupEndpoint = "API_Singup";
        public const string AuthenticationEndpoint = "JWT_Auth";
        public const string RefreshEndpoint = "JWT_Refresh";
        public const string VerifyEndpoint = "JWT_Verify";
        public const string PasswordRecoveryEndpoint = "WEB_PasswordRecovery";
        public const string AslmGetUserDataEndpoint = "API_AslmGetUserData";
        public const string AslmCreateProfileEndpoint = "API_AslmCreateProfile";

        public const string ErrorUserData = "ErrorUserData";
        public const string ErrorUserIsActive = "ErrorUserIsActive";
        public const string ErrorProfileAslm = "ErrorProfileASLM";
        public const string ErrorAslmIsActive = "ErrorAslmIsActive";
        public const string ErrorAslmIsBanned = "ErrorAslmIsBanned";

        private readonly ILogger<SunriseService> _logger;
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _dataGate = new(1, 1);

        private readonly string _domainsFilePath;
        private readonly string _urlsFilePath;
        private readonly string _tokensFilePath;
        private readonly string _userDataFilePath;

        private readonly JsonSerializerOptions _storageJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly JsonSerializerOptions _apiJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private SunriseDomainsData _domainsData = CreateDefaultDomainsData();
        private SunriseUrlsData _urlsData = CreateDefaultUrlsData();
        private SunriseTokensData _tokensData = CreateDefaultTokensData();
        private SunriseUserDataDocument _userDataDocument = CreateDefaultUserDataDocument();

        private bool _initialized;
        private bool _disposed;


        // State access

        /// <summary>
        /// Gets the currently loaded SUNRISE domain catalog.
        /// </summary>
        public IReadOnlyList<SunriseDomain> Domains => _domainsData.Domains;

        /// <summary>
        /// Gets the currently loaded SUNRISE endpoint catalog.
        /// </summary>
        public IReadOnlyList<SunriseUrl> Urls => _urlsData.Urls;

        /// <summary>
        /// Gets the current in-memory JWT values.
        /// </summary>
        public SunriseJwtTokens Tokens => _tokensData.Jwt;

        /// <summary>
        /// Gets the current persisted SUNRISE user data.
        /// </summary>
        public SunriseUserData UserData => _userDataDocument.UserData;


        // Construction

        /// <summary>
        /// Creates the service and resolves all persisted data paths below <c>Data/App</c>.
        /// </summary>
        public SunriseService(ILogger<SunriseService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();

            var dataDirectory = Path.Combine(AppRoot.Directory, "Data", "App");
            _domainsFilePath = Path.Combine(dataDirectory, DomainsFileName);
            _urlsFilePath = Path.Combine(dataDirectory, UrlsFileName);
            _tokensFilePath = Path.Combine(dataDirectory, TokensFileName);
            _userDataFilePath = Path.Combine(dataDirectory, UserDataFileName);
        }


        // Initialization

        /// <summary>
        /// Loads all SUNRISE configuration and account data once for the process lifetime.
        /// </summary>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            await _dataGate.WaitAsync(ct);
            try
            {
                if (_initialized)
                {
                    return;
                }

                _domainsData = await LoadDocumentAsync(
                    _domainsFilePath,
                    CreateDefaultDomainsData,
                    data => data.Normalize(),
                    ct);
                _urlsData = await LoadDocumentAsync(
                    _urlsFilePath,
                    CreateDefaultUrlsData,
                    data => data.Normalize(),
                    ct);
                _tokensData = await LoadDocumentAsync(
                    _tokensFilePath,
                    CreateDefaultTokensData,
                    data => data.Normalize(),
                    ct);
                _userDataDocument = await LoadDocumentAsync(
                    _userDataFilePath,
                    CreateDefaultUserDataDocument,
                    data => data.Normalize(),
                    ct);

                var domainsChanged = EnsureDefaultDomain();
                var urlsChanged = EnsureDefaultUrls();

                if (domainsChanged)
                {
                    await SaveDocumentCoreAsync(_domainsFilePath, _domainsData, ct);
                }

                if (urlsChanged)
                {
                    await SaveDocumentCoreAsync(_urlsFilePath, _urlsData, ct);
                }

                _initialized = true;
            }
            finally
            {
                _dataGate.Release();
            }
        }


        // URL resolution

        /// <summary>
        /// Resolves one configured endpoint name into its absolute SUNRISE URI.
        /// </summary>
        public Uri CreateFullUri(string endpointName)
        {
            ThrowIfDisposed();
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(endpointName))
            {
                throw new ArgumentException("SUNRISE endpoint name is required.", nameof(endpointName));
            }

            var endpoint = _urlsData.Urls.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, endpointName, StringComparison.Ordinal));
            if (endpoint == null)
            {
                throw new KeyNotFoundException($"SUNRISE endpoint '{endpointName}' is not configured.");
            }

            var domain = _domainsData.Domains.FirstOrDefault(candidate =>
                string.Equals(candidate.DomainType, endpoint.DomainType, StringComparison.Ordinal));
            if (domain == null)
            {
                throw new InvalidOperationException(
                    $"SUNRISE domain type '{endpoint.DomainType}' required by endpoint '{endpoint.Name}' is not configured.");
            }

            if (string.IsNullOrWhiteSpace(domain.Protocol) || string.IsNullOrWhiteSpace(domain.Domain))
            {
                throw new InvalidOperationException(
                    $"SUNRISE domain type '{domain.DomainType}' has an incomplete protocol or domain value.");
            }

            var baseUrl = $"{domain.Protocol.TrimEnd(':', '/', '\\')}://{domain.Domain.TrimEnd('/')}/";
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException(
                    $"SUNRISE domain type '{domain.DomainType}' does not form a valid absolute URI.");
            }

            var relativeUrl = endpoint.Url.TrimStart('/');
            if (!Uri.TryCreate(baseUri, relativeUrl, out var fullUri))
            {
                throw new InvalidOperationException(
                    $"SUNRISE endpoint '{endpoint.Name}' does not form a valid URI.");
            }

            return fullUri;
        }

        /// <summary>
        /// Returns the configured password-recovery web page URI.
        /// </summary>
        public Uri GetPasswordRecoveryUri() => CreateFullUri(PasswordRecoveryEndpoint);


        // Generic web request

        /// <summary>
        /// Sends one request to a named SUNRISE endpoint with an arbitrary array of headers.
        /// </summary>
        /// <remarks>
        /// Supported verbs are GET, POST, PUT, and DELETE. Unknown verbs fall back to GET,
        /// matching the original SUNRISE request behavior. A body is attached only to POST and PUT.
        /// The returned response belongs to the caller and must be disposed.
        /// </remarks>
        public async Task<HttpResponseMessage> SendAsync(
            string endpointName,
            string httpRequestVerb,
            SunriseHeader[]? headers = null,
            string? body = null,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await InitializeAsync(ct);

            var method = NormalizeHttpMethod(httpRequestVerb);
            var uri = CreateFullUri(endpointName);

            using var request = new HttpRequestMessage(method, uri);
            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                request.Content = CreateBodyContent(body ?? string.Empty);
            }

            ApplyHeaders(request, headers);

            try
            {
                return await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseContentRead,
                    ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Never include request bodies or header values here: they may contain passwords or JWTs.
                _logger.LogWarning(
                    ex,
                    "SUNRISE {Method} request to endpoint {EndpointName} failed.",
                    method.Method,
                    endpointName);
                throw;
            }
        }

        // Authentication API

        /// <summary>
        /// Authenticates a SUNRISE account with a username and password.
        /// </summary>
        public Task<HttpResponseMessage> AuthenticateAsync(
            string username,
            string password,
            CancellationToken ct = default)
        {
            var body = SerializeApiPayload(new SunriseAuthRequest
            {
                Username = username,
                Password = password
            });

            return SendJsonAsync(AuthenticationEndpoint, body, ct);
        }

        /// <summary>
        /// Requests a new SUNRISE token pair using a refresh token.
        /// </summary>
        public Task<HttpResponseMessage> RefreshTokenAsync(
            string refreshToken,
            CancellationToken ct = default)
        {
            var body = SerializeApiPayload(new SunriseRefreshRequest
            {
                Refresh = refreshToken
            });

            return SendJsonAsync(RefreshEndpoint, body, ct);
        }

        /// <summary>
        /// Verifies one SUNRISE refresh or access token.
        /// </summary>
        public Task<HttpResponseMessage> VerifyTokenAsync(
            string refreshOrAccessToken,
            CancellationToken ct = default)
        {
            var body = SerializeApiPayload(new SunriseVerifyRequest
            {
                Token = refreshOrAccessToken
            });

            return SendJsonAsync(VerifyEndpoint, body, ct);
        }

        /// <summary>
        /// Creates a SUNRISE account for the ASLM application.
        /// </summary>
        public Task<HttpResponseMessage> SignUpAsync(
            string email,
            string username,
            string password1,
            string password2,
            CancellationToken ct = default)
        {
            var body = SerializeApiPayload(new SunriseSignupRequest
            {
                Email = email,
                Username = username,
                Password1 = password1,
                Password2 = password2,
                SignupApp = "ASLM"
            });

            return SendJsonAsync(SignupEndpoint, body, ct);
        }

        /// <summary>
        /// Sends an application/json POST request to one named endpoint.
        /// </summary>
        private Task<HttpResponseMessage> SendJsonAsync(
            string endpointName,
            string body,
            CancellationToken ct) =>
            SendAsync(
                endpointName,
                "POST",
                [new SunriseHeader("Content-Type", "application/json")],
                body,
                ct);


        // ASLM API

        /// <summary>
        /// Gets the current account and ASLM profile data using the stored access token.
        /// </summary>
        public Task<HttpResponseMessage> GetAslmUserDataAsync(CancellationToken ct = default) =>
            SendAslmAuthorizedAsync(AslmGetUserDataEndpoint, ct);

        /// <summary>
        /// Creates an ASLM profile for the current account using the stored access token.
        /// </summary>
        public Task<HttpResponseMessage> CreateAslmProfileAsync(CancellationToken ct = default) =>
            SendAslmAuthorizedAsync(AslmCreateProfileEndpoint, ct);

        /// <summary>
        /// Sends one JWT-authorized ASLM request.
        /// </summary>
        private async Task<HttpResponseMessage> SendAslmAuthorizedAsync(
            string endpointName,
            CancellationToken ct)
        {
            // Load persisted tokens before constructing the Authorization header. This also
            // keeps direct service use correct when startup initialization has not run yet.
            await InitializeAsync(ct);

            return await SendAsync(
                endpointName,
                "POST",
                [new SunriseHeader("Authorization", $"JWT {_tokensData.Jwt.TokenAccess}")],
                string.Empty,
                ct);
        }


        // Token persistence

        /// <summary>
        /// Returns the stored refresh token when SUNRISE credentials are available.
        /// </summary>
        public bool TryGetRefreshToken(out string refreshToken)
        {
            ThrowIfDisposed();

            refreshToken = _tokensData.Jwt.TokenRefresh;
            return _initialized && !string.IsNullOrEmpty(refreshToken);
        }

        /// <summary>
        /// Parses a SUNRISE token response and persists both refresh and access tokens.
        /// </summary>
        public async Task<bool> UpdateTokensAsync(string tokensJson, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await InitializeAsync(ct);

            SunriseTokenResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<SunriseTokenResponse>(tokensJson, _apiJsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "SUNRISE token response could not be parsed.");
                return false;
            }

            if (response?.Refresh == null || response.Access == null)
            {
                return false;
            }

            await _dataGate.WaitAsync(ct);
            try
            {
                _tokensData.Jwt.TokenRefresh = response.Refresh;
                _tokensData.Jwt.TokenAccess = response.Access;
                _tokensData.Normalize();
                await SaveDocumentCoreAsync(_tokensFilePath, _tokensData, ct);
                return true;
            }
            finally
            {
                _dataGate.Release();
            }
        }

        /// <summary>
        /// Clears all stored SUNRISE tokens.
        /// </summary>
        public async Task ClearTokensAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await InitializeAsync(ct);

            await _dataGate.WaitAsync(ct);
            try
            {
                _tokensData.Jwt.TokenRefresh = string.Empty;
                _tokensData.Jwt.TokenAccess = string.Empty;
                await SaveDocumentCoreAsync(_tokensFilePath, _tokensData, ct);
            }
            finally
            {
                _dataGate.Release();
            }
        }


        // User data persistence and validation

        /// <summary>
        /// Validates the account and ASLM state returned in a <c>user_data</c> API response.
        /// </summary>
        public bool CheckUserData(string json, out string errorCode)
        {
            errorCode = string.Empty;

            SunriseUserDataResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<SunriseUserDataResponse>(json, _apiJsonOptions);
            }
            catch (JsonException)
            {
                errorCode = ErrorUserData;
                return false;
            }

            var account = response?.UserData;
            if (account == null)
            {
                errorCode = ErrorUserData;
                return false;
            }

            if (!account.IsActive)
            {
                errorCode = ErrorUserIsActive;
                return false;
            }

            if (account.Aslm == null)
            {
                errorCode = ErrorProfileAslm;
                return false;
            }

            if (!account.Aslm.IsActive)
            {
                errorCode = ErrorAslmIsActive;
                return false;
            }

            if (account.Aslm.IsBanned)
            {
                errorCode = ErrorAslmIsBanned;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses and persists the account contained in a <c>user_data</c> API response.
        /// </summary>
        public async Task<bool> UpdateUserDataAsync(string json, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await InitializeAsync(ct);

            SunriseUserDataResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<SunriseUserDataResponse>(json, _apiJsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "SUNRISE user-data response could not be parsed.");
                return false;
            }

            if (response?.UserData == null)
            {
                return false;
            }

            response.UserData.Normalize();

            await _dataGate.WaitAsync(ct);
            try
            {
                _userDataDocument.UserData.Account = response.UserData;
                _userDataDocument.Normalize();
                await SaveDocumentCoreAsync(_userDataFilePath, _userDataDocument, ct);
                return true;
            }
            finally
            {
                _dataGate.Release();
            }
        }

        /// <summary>
        /// Persists the current user-data model after caller changes.
        /// </summary>
        public async Task SaveUserDataAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();
            await InitializeAsync(ct);

            await _dataGate.WaitAsync(ct);
            try
            {
                _userDataDocument.Normalize();
                await SaveDocumentCoreAsync(_userDataFilePath, _userDataDocument, ct);
            }
            finally
            {
                _dataGate.Release();
            }
        }


        // Header and payload helpers

        /// <summary>
        /// Applies every supplied request or content header without imposing a fixed count.
        /// </summary>
        private static void ApplyHeaders(HttpRequestMessage request, SunriseHeader[]? headers)
        {
            if (headers == null || headers.Length == 0)
            {
                return;
            }

            foreach (var header in headers)
            {
                if (header == null || string.IsNullOrWhiteSpace(header.Name))
                {
                    throw new ArgumentException("SUNRISE request headers must have a non-empty name.", nameof(headers));
                }

                var headerName = header.Name.Trim();
                var headerValue = header.Value ?? string.Empty;

                if (request.Headers.TryAddWithoutValidation(headerName, headerValue))
                {
                    continue;
                }

                // HttpClient separates entity headers such as Content-Type from request headers.
                // An empty content container lets a bodyless request still carry such a header.
                request.Content ??= new ByteArrayContent([]);
                if (!request.Content.Headers.TryAddWithoutValidation(headerName, headerValue))
                {
                    throw new ArgumentException(
                        $"'{headerName}' is not a valid SUNRISE request or content header.",
                        nameof(headers));
                }
            }
        }

        /// <summary>
        /// Creates UTF-8 request content without adding an implicit media type.
        /// </summary>
        private static HttpContent CreateBodyContent(string body) =>
            new ByteArrayContent(Encoding.UTF8.GetBytes(body));

        /// <summary>
        /// Normalizes supported request verbs and falls back to GET.
        /// </summary>
        private static HttpMethod NormalizeHttpMethod(string? httpRequestVerb) =>
            httpRequestVerb?.Trim().ToUpperInvariant() switch
            {
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                _ => HttpMethod.Get
            };

        /// <summary>
        /// Serializes one strongly typed SUNRISE API payload.
        /// </summary>
        private string SerializeApiPayload<T>(T payload) =>
            JsonSerializer.Serialize(payload, _apiJsonOptions);


        // Storage helpers

        /// <summary>
        /// Loads and normalizes one typed JSON document or creates its defaults when unavailable.
        /// </summary>
        private async Task<T> LoadDocumentAsync<T>(
            string filePath,
            Func<T> createDefault,
            Action<T> normalize,
            CancellationToken ct)
            where T : class
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath, ct);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var loaded = JsonSerializer.Deserialize<T>(json, _storageJsonOptions);
                        if (loaded != null)
                        {
                            normalize(loaded);
                            return loaded;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                _logger.LogError(
                    ex,
                    "Failed to load SUNRISE data from {FilePath}. Falling back to defaults.",
                    filePath);
            }

            var defaults = createDefault();
            normalize(defaults);
            await SaveDocumentCoreAsync(filePath, defaults, ct);
            return defaults;
        }

        /// <summary>
        /// Persists one typed SUNRISE document below the shared application data root.
        /// </summary>
        private async Task SaveDocumentCoreAsync<T>(string filePath, T data, CancellationToken ct)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(data, _storageJsonOptions);
            await File.WriteAllTextAsync(filePath, json, ct);
        }


        // Configuration defaults and migrations

        /// <summary>
        /// Ensures the default NGGT OSS domain exists without replacing user-configured values.
        /// </summary>
        private bool EnsureDefaultDomain()
        {
            _domainsData.Normalize();
            var removed = _domainsData.Domains.RemoveAll(domain =>
                string.IsNullOrWhiteSpace(domain.Protocol) ||
                string.IsNullOrWhiteSpace(domain.Domain) ||
                string.IsNullOrWhiteSpace(domain.DomainType));
            var changed = removed > 0;

            if (_domainsData.Domains.Any(domain =>
                    string.Equals(domain.DomainType, DefaultDomainType, StringComparison.Ordinal)))
            {
                return changed;
            }

            _domainsData.Domains.Add(CreateDefaultDomain());
            return true;
        }

        /// <summary>
        /// Adds current endpoints that are absent from the persisted catalog.
        /// </summary>
        private bool EnsureDefaultUrls()
        {
            _urlsData.Normalize();
            var changed = false;

            var removed = _urlsData.Urls.RemoveAll(url =>
                string.IsNullOrWhiteSpace(url.Name) ||
                string.IsNullOrWhiteSpace(url.Url) ||
                string.IsNullOrWhiteSpace(url.DomainType));
            changed |= removed > 0;

            foreach (var defaultUrl in CreateDefaultUrls())
            {
                if (_urlsData.Urls.Any(url =>
                        string.Equals(url.Name, defaultUrl.Name, StringComparison.Ordinal)))
                {
                    continue;
                }

                _urlsData.Urls.Add(defaultUrl);
                changed = true;
            }

            if (_urlsData.FileVersion < CurrentFileVersion)
            {
                _urlsData.FileVersion = CurrentFileVersion;
                changed = true;
            }

            return changed;
        }

        private static SunriseDomainsData CreateDefaultDomainsData() => new()
        {
            FileVersion = CurrentFileVersion,
            Domains = [CreateDefaultDomain()]
        };

        private static SunriseDomain CreateDefaultDomain() => new()
        {
            Protocol = "http",
            Domain = "127.0.0.1:8000",
            DomainType = DefaultDomainType
        };

        private static SunriseUrlsData CreateDefaultUrlsData() => new()
        {
            FileVersion = CurrentFileVersion,
            Urls = CreateDefaultUrls()
        };

        private static List<SunriseUrl> CreateDefaultUrls() =>
        [
            CreateUrl(SignupEndpoint, "api/id/signup/"),
            CreateUrl(AuthenticationEndpoint, "api/id/token/"),
            CreateUrl(RefreshEndpoint, "api/id/token/refresh/"),
            CreateUrl(VerifyEndpoint, "api/id/token/verify/"),
            CreateUrl(PasswordRecoveryEndpoint, "id/"),
            CreateUrl(AslmGetUserDataEndpoint, "api/aslm/getuserdata/"),
            CreateUrl(AslmCreateProfileEndpoint, "api/aslm/createprofile/")
        ];

        private static SunriseUrl CreateUrl(string name, string url) => new()
        {
            Name = name,
            Url = url,
            DomainType = DefaultDomainType
        };

        private static SunriseTokensData CreateDefaultTokensData() => new()
        {
            FileVersion = CurrentFileVersion,
            Jwt = new SunriseJwtTokens()
        };

        private static SunriseUserDataDocument CreateDefaultUserDataDocument() => new()
        {
            FileVersion = CurrentFileVersion,
            UserData = new SunriseUserData
            {
                Account = new SunriseUserAccount
                {
                    Aslm = new SunriseAslmProfile()
                },
                SavedAccounts = [new SunriseSavedAccount()]
            }
        };


        // Lifecycle helpers

        /// <summary>
        /// Ensures a synchronous operation only uses initialized service state.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("SunriseService must be initialized before this operation.");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        /// <summary>
        /// Releases the process-wide HTTP client and synchronization primitive.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _httpClient.Dispose();
            _dataGate.Dispose();
        }
    }
}
