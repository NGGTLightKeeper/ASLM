---
title: "AslmApiServer"
draft: false
---

## Class `AslmApiServer`

`ASLM/Services/AslmApiServer.cs` — **`public sealed`** — local HTTP reverse proxy (`HttpListener`) for module backends from **`ASLM_Ports.json`**.

**DI:** [AppDataStore](AppDataStore/), [PortRegistry](PortRegistry/), [ModuleInstaller](ModuleInstaller/). **`IDisposable`**. Constants: `BackendRedirectLimit`, `RouteHeaderName`.

---

### State and events

| Member | Description |
| --- | --- |
| `StateChanged` | Listener or enablement changed |
| `IsRunning` | `HttpListener` active |
| `IsEnabled` | `AppData.Api.ServerEnabled` |
| `Port` | Assigned official API port |
| `LastError` | Last startup failure message |

---

## Public methods

#### `public AslmApiServer( AppDataStore appData, PortRegistry ports, ModuleInstaller moduleInstaller, ILogger<AslmApiServer> logger)`

**Purpose:** Creates the ASLM API reverse proxy and resolves its runtime data path.

---

#### `public string BaseUrl`

**Purpose:** Gets the browser-friendly root URL for the reverse proxy.

---

#### `public async Task StartIfEnabledAsync()`

**Purpose:** Starts the reverse proxy when persisted settings say it should be enabled.

---

#### `public async Task SetEnabledAsync(bool enabled)`

**Purpose:** Persists the enabled state and starts or stops the proxy to match it.

---

#### `public Task StartAsync()`

**Purpose:** Starts the local listener and background request loop.

---

#### `public async Task StopAsync()`

**Purpose:** Stops the local listener and waits briefly for the request loop to exit.

---

#### `public IReadOnlyList<AslmApiHostInfo> GetHosts()`

**Purpose:** ---

#### `public async Task<IReadOnlyList<AslmApiHostInfo>> GetHostsWithAvailabilityAsync(CancellationToken ct = default)`

---

## Private methods

#### `private async Task RunListenerLoopAsync(HttpListener listener, CancellationToken ct)`

**Purpose:** Accepts incoming requests until the listener is stopped or cancelled.

---

#### `private async Task HandleContextAsync(HttpListenerContext context, CancellationToken ct)`

**Purpose:** Routes one incoming request to an index page, module host page, or proxied backend request.

---

#### `private static ProxyRoute ResolveRoute(HttpListenerRequest request, IReadOnlyDictionary<string, Dictionary<string, int>> portMap)`

**Purpose:** Resolves the incoming URL into a root page, listing page, or backend proxy route.

---

#### `private static bool TryResolveRefererProxyRoute( HttpListenerRequest request, IReadOnlyDictionary<string, Dictionary<string, int>> portMap, IReadOnlyList<string> requestSegments, out ProxyRoute route)`

**Purpose:** Resolves root-relative asset or API requests by using the mirror route from the referring page.

---

#### `private static bool TryResolveHostRoute( IReadOnlyDictionary<string, int> hosts, string routeHostKey, out string hostKey, out int port)`

**Purpose:** Resolves a public host route segment back to the underlying port-map host key.

---

#### `private static string BuildTargetPath(IEnumerable<string> segments, bool preserveTrailingSlash)`

**Purpose:** Builds a backend path from decoded path segments.

---

#### `private static string BuildTargetPath(IEnumerable<string> segments)`

**Purpose:** Builds a backend path from decoded path segments without forcing a trailing slash.

---

#### `private static List<string> GetPathSegments(string path)`

**Purpose:** Parses decoded path segments from an absolute URL path.

---

#### `private static async Task ProxyRequestAsync(HttpListenerContext context, ProxyRoute route, CancellationToken ct)`

**Purpose:** Proxies one resolved route to the selected localhost backend.

---

#### `private static async Task<HttpResponseMessage> SendHttpRequestWithInternalRedirectsAsync( HttpListenerRequest sourceRequest, ProxyRoute route, CancellationToken ct)`

**Purpose:** Sends the backend HTTP request and follows local backend redirects before responding to the browser.

---

#### `private static HttpRequestMessage CreateHttpRequest( HttpListenerRequest sourceRequest, ProxyRoute route, string query, bool includeBody)`

**Purpose:** Builds one backend HTTP request for the resolved proxy route.

---

#### `private static void CopyRequestHeaders(NameValueCollection sourceHeaders, HttpRequestMessage targetRequest)`

**Purpose:** Copies browser request headers that are safe to forward to the backend.

---

#### `private static void AddForwardedHeaders(HttpListenerRequest sourceRequest, HttpRequestMessage targetRequest, ProxyRoute route)`

**Purpose:** Adds reverse-proxy context headers for frameworks that support mounted path prefixes.

---

#### `private static string RewriteMirrorUriToBackendUri(Uri mirrorUri, ProxyRoute route, string backendOrigin)`

**Purpose:** Converts a mirror URL seen by the browser into the equivalent backend URL for server-side checks.

---

#### `private static bool TryResolveInternalRedirect( HttpListenerRequest sourceRequest, HttpResponseMessage responseMessage, ProxyRoute currentRoute, out ProxyRoute nextRoute, out string nextQuery)`

**Purpose:** ---

#### `private static bool IsRedirectStatusCode(HttpStatusCode statusCode)`

---

#### `private static bool IsBodylessRedirectMethod(string method)`

**Purpose:** ---

#### `private static async Task ProxyWebSocketAsync(HttpListenerContext context, ProxyRoute route, CancellationToken ct)`

Proxies one WebSocket upgrade to the selected localhost backend.

---

#### `private static Uri CreateWebSocketTargetUri(HttpListenerRequest request, ProxyRoute route)`

**Purpose:** Builds a backend WebSocket URI for the resolved proxy route.

---

#### `private static async Task PumpWebSocketAsync(WebSocket source, WebSocket target, CancellationToken ct)`

**Purpose:** Relays WebSocket frames until either side closes or errors.

---

#### `private static async Task CloseWebSocketSafeAsync( WebSocket socket, WebSocketCloseStatus? closeStatus, string? closeDescription, CancellationToken ct)`

**Purpose:** Closes a WebSocket while tolerating peers that have already disconnected.

---

#### `private static string? ResolveWebSocketSubProtocol(HttpListenerRequest request)`

**Purpose:** Chooses the first requested WebSocket subprotocol when the browser supplied any.

---

#### `private static void CopyResponseHeaders(HttpResponseMessage sourceResponse, HttpListenerResponse targetResponse, ProxyRoute route)`

**Purpose:** Copies backend response headers to the browser response with reverse-proxy rewrites applied.

---

#### `private static void TrySetCookieHeader(HttpListenerResponse response, string cookie)`

**Purpose:** Adds one Set-Cookie header while tolerating platform restrictions in HttpListener.

---

#### `private static IEnumerable<string> ExtractSetCookieHeaders(HttpResponseMessage response)`

**Purpose:** Extracts all Set-Cookie values from response headers without merging them.

---

#### `private static string RewriteSetCookie(string cookie, ProxyRoute route)`

**Purpose:** Rewrites backend cookies so their path is scoped to the mounted mirror route.

---

#### `private static string RewriteLocationHeader(string location, ProxyRoute route)`

**Purpose:** Rewrites one Location header so browser-visible redirects remain inside the mirror route.

---

#### `private static void TrySetResponseHeader(HttpListenerResponse response, string headerName, string headerValue)`

**Purpose:** Adds a response header when HttpListener allows application code to set it.

---

#### `private static bool ShouldSkipRequestHeader(string headerName)`

**Purpose:** ---

#### `private static bool ShouldSkipResponseHeader(string headerName)`

---

#### `private static bool ShouldRewriteContent(string? mediaType)`

**Purpose:** ---

#### `private static string RewriteTextContent(string content, string? mediaType, ProxyRoute route)`

Rewrites HTML backend content so document links stay inside the mirror route.

---

#### `private static string RewriteLoopbackBackendUrls(string content, ProxyRoute route)`

**Purpose:** Rewrites absolute localhost URLs that point at the backend target into mirror-root URLs.

---

#### `private static string RewriteRootRelativeUrls(string content, ProxyRoute route)`

**Purpose:** Rewrites common root-relative URLs in HTML, CSS, JavaScript, and JSON payloads.

---

#### `private static string RewriteAttributeUrl(Match match, ProxyRoute route)`

**Purpose:** Rewrites one HTML attribute URL match.

---

#### `private static string RewriteCssUrl(Match match, ProxyRoute route)`

**Purpose:** Rewrites one CSS url(...) match.

---

#### `private static string RewriteSrcSet(Match match, ProxyRoute route)`

**Purpose:** Rewrites one srcset attribute while preserving image descriptors.

---

#### `private static string RewriteOneRootRelativeUrl(string url, ProxyRoute route)`

**Purpose:** Rewrites one root-relative backend URL to the mounted mirror route when needed.

---

#### `private static string InjectHtmlMirrorSupport(string html, ProxyRoute route)`

**Purpose:** Injects base and browser-side request rewriting support into HTML documents.

---

#### `private static string CreateRuntimeShim(ProxyRoute route)`

**Purpose:** Creates a browser-side shim for runtime APIs that construct root-relative URLs after page load.

---

#### `private static Encoding ResolveContentEncoding(MediaTypeHeaderValue? contentType)`

**Purpose:** Resolves the response text encoding, falling back to UTF-8.

---

#### `private async Task WriteRootPageAsync( HttpListenerContext context, IReadOnlyDictionary<string, Dictionary<string, int>> portMap, int statusCode, string? message, CancellationToken ct)`

**Purpose:** Writes the root index that lists every module and host path declared by the port map.

---

#### `private async Task WriteModulePageAsync( HttpListenerContext context, string moduleId, IReadOnlyDictionary<string, int> hosts, int statusCode, string? message, CancellationToken ct)`

**Purpose:** Writes a module page that lists the hosts available under that module route.

---

#### `private static async Task WriteErrorPageAsync(HttpListenerContext context, Exception ex, CancellationToken ct)`

**Purpose:** Writes an error page when request routing or proxying fails unexpectedly.

---

#### `private static async Task WriteHtmlAsync(HttpListenerContext context, int statusCode, string title, string body, CancellationToken ct)`

**Purpose:** Writes a complete HTML document with the shared minimal ASLM API style.

---

#### `private static async Task WritePlainTextAsync(HttpListenerContext context, int statusCode, string text, CancellationToken ct)`

**Purpose:** Writes a plain text response for low-level proxy validation failures.

---

#### `private static void AppendMessage(StringBuilder body, string? message)`

**Purpose:** Appends a warning message to a generated index page.

---

#### `private void AppendHostLink(StringBuilder body, string moduleId, string hostKey, int port)`

**Purpose:** Appends one routed host link to a generated index page.

---

#### `private static void AppendModuleHeading( StringBuilder body, string moduleId, IReadOnlyDictionary<string, bool> moduleStates)`

**Purpose:** Appends a module heading and disabled status marker for the generated index page.

---

#### `private static void AppendDisabledBadge( StringBuilder body, string moduleId, IReadOnlyDictionary<string, bool> moduleStates)`

**Purpose:** Appends a compact disabled badge when the module manifest says the module is off.

---

#### `private async Task<IReadOnlyDictionary<string, bool>> LoadModuleDisplayStatesAsync()`

**Purpose:** Loads enabled states from installed module manifests for the generated API index.

---

#### `private Dictionary<string, Dictionary<string, int>> LoadPortMap()`

**Purpose:** Loads the current port map from disk and supports the older flat module-to-port shape.

---

#### `private static Dictionary<string, Dictionary<string, int>> NormalizePortMap(Dictionary<string, Dictionary<string, int>> source)`

**Purpose:** Removes invalid module and host entries from a deserialized port map.

---

#### `private static bool IsPublicModulePortOwner(string ownerId)`

**Purpose:** ---

#### `private AslmApiHostInfo CreateHostInfo(string moduleId, string hostKey, int port, bool? isOnline)`

Creates one host info object for the ASLM UI and web index.

---

#### `private static void RedirectToRouteRoot(HttpListenerContext context, string moduleId, string routeHostKey)`

**Purpose:** Redirects a module host route to its slash-terminated root.

---

#### `private static string BuildHostRouteKey(string hostKey)`

**Purpose:** Builds the public route key used for one port-map host.

---

#### `private static bool IsMirrorPath(string path, string routePrefix)`

**Purpose:** ---

#### `private static string StripMirrorPrefix(string path, string routePrefix)`

Converts a mirror-prefixed path back into the backend root-relative path.

---

#### `private static string NormalizeBackendPath(string path)`

**Purpose:** Normalizes a backend path to a root-relative path without query or fragment.

---

#### `private static string HtmlEncode(string? value)`

**Purpose:** HTML-encodes text for generated index pages.

---

#### `private static async Task<bool> IsTcpPortOpenAsync(int port, CancellationToken ct)`

**Purpose:** Checks whether a localhost TCP port is currently accepting connections.

---

#### `private static bool IsValidPort(int port)`

**Purpose:** Checks whether a TCP port value can be used in a localhost URL.

---

#### `private static HttpClient CreateProxyClient()`

**Purpose:** Creates the shared HTTP client used by proxied backend requests.

---

#### `private void RaiseStateChanged()`

**Purpose:** Raises the state changed event on the current thread.

---

#### `private void CleanupListener()`

**Purpose:** Clears listener references after stop or failed start.

---

#### `private int GetAssignedPort()`

**Purpose:** Gets or reserves the ASLM API port from the shared official module pool.

---

#### `private static string BuildBaseUrl(int port)`

**Purpose:** Builds the localhost base URL for the requested reverse proxy port.

---

#### `private static string GetRootDirectory()`

**Purpose:** ---

## Public methods

#### `public void Dispose()`

**Purpose:** ---

## Related types and nested members

#### `private sealed record ProxyRoute( ProxyRouteKind Kind, string ModuleId, string HostKey, string RouteHostKey, int Port, string TargetPath, IReadOnlyDictionary<string, int> Hosts)`

**Purpose:** Describes the resolved action for one incoming ASLM API request.

---

## Public methods

#### `public string RoutePrefix`

**Purpose:** Gets the mounted mirror route prefix without a trailing slash.

---

#### `public string RouteRoot`

**Purpose:** Mounted mirror route root with trailing slash (`RoutePrefix + "/"`).

---

#### `public static ProxyRoute RootIndex()`

**Purpose:** Creates the root-index route.

---

#### `public static ProxyRoute ModuleIndex(string moduleId, IReadOnlyDictionary<string, int> hosts)`

**Purpose:** Creates the module-index route.

---

#### `public static ProxyRoute ModuleNotFound(string moduleId)`

**Purpose:** Creates the module-not-found route.

---

#### `public static ProxyRoute HostNotFound(string moduleId, string hostKey, IReadOnlyDictionary<string, int> hosts)`

**Purpose:** Creates the host-not-found route.

---

#### `public static ProxyRoute RedirectToSlash(string moduleId, string hostKey, string routeHostKey)`

**Purpose:** Creates the slash-normalization redirect route.

---

#### `public static ProxyRoute Proxy(string moduleId, string hostKey, string routeHostKey, int port, string targetPath, IReadOnlyDictionary<string, int> hosts)`

**Purpose:** Creates a backend proxy route.

---

#### `public static string BuildRouteRoot(string moduleId, string hostKey)`

**Purpose:** Builds the slash-terminated route root for a module host.

---

#### `public string ToMirrorPath(string backendPath, string query, string fragment)`

**Purpose:** Converts one backend path into a browser-visible mirror path.

---

## Related types and nested members

#### `public record AslmApiHostInfo( string ModuleId, string HostKey, int Port, string MirrorUrl, string TargetUrl, bool? IsOnline)`

**Purpose:** Describes one ASLM API mirror host displayed in the UI.

---

## Related

- [PortRegistry](PortRegistry/)
- [AslmApiView](../Pages/AslmApiView/)
- [LoadingPage](../Pages/LoadingPage/)
