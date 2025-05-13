using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var programName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
{
    var version = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    Console.WriteLine($"{programName} version {version}");
    return;
}

if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h"))
{
    Console.WriteLine($"\nUsage: {programName} <localUrl> <targetUrl> <certSubject> [options]\n");
    Console.WriteLine("Options:");
    Console.WriteLine("  --preserve-encoding     Preserve incoming encoding when forwarding (default: UTF-8)");
    Console.WriteLine("  --log-body=false        Disable body logging");
    Console.WriteLine("  --version               Print version info");
    Console.WriteLine("  --help, -h              Show this help message\n");
    Console.WriteLine("Ctrl+L to clear the console.\n");
    Console.WriteLine("Ctrl+C to stop the proxy.\n");
    return;
}

var logPath = Path.Combine(
    OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.local/share",
    programName,
    "proxy.log"
);

Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

if (args.Length < 3)
{
    Console.WriteLine($"Usage: {programName} <localUrl> <targetUrl> <certSubject> [--preserve-encoding] [--log-body=false]");
    return;
}

var localUrl = args[0];
var targetUrl = args[1];
var certSubject = args[2];

if (!Uri.TryCreate(localUrl, UriKind.Absolute, out var localUri) || (localUri.Scheme != Uri.UriSchemeHttp && localUri.Scheme != Uri.UriSchemeHttps))
{
    Console.WriteLine("[ERROR] Invalid localUrl. It must be a valid HTTP or HTTPS URL.");
    return;
}

if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri) || (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
{
    Console.WriteLine("[ERROR] Invalid targetUrl. It must be a valid HTTP or HTTPS URL.");
    return;
}

if (string.IsNullOrWhiteSpace(certSubject))
{
    Console.WriteLine("[ERROR] Invalid certSubject. It cannot be null or empty.");
    return;
}

var preserveEncoding = args.Contains("--preserve-encoding", StringComparer.OrdinalIgnoreCase);
var logBody = !args.Contains("--log-body=false", StringComparer.OrdinalIgnoreCase);

Log($"Listen on: {localUrl}");
Log($"Forward to: {targetUrl}");
Log($"Client certificate: {certSubject}");
Log($"Preserve encoding forward: {(preserveEncoding ? "YES" : "NO (UTF-8 used)")}");
Log($"Log to: {logPath}");

var clientCert = FindCertificate(certSubject);

if (clientCert == null)
{
    Log("[ERROR] Certificate not found.");
    return;
}
Log($"[OK] Client certificate found: {clientCert.Subject}");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

var app = builder.Build();

var handler = new HttpClientHandler();
handler.ClientCertificates.Add(clientCert);
handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
var httpClient = new HttpClient(handler);

var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;

Console.CancelKeyPress += (_, e) =>
{
    Log("[INFO] Shutdown signal received. Stopping...");
    cts.Cancel();
    e.Cancel = true;
};

_ = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.L)
            {
                Console.Clear();
                Log("[INFO] Console cleared via Ctrl+L");
            }
        }
        await Task.Delay(100, cts.Token);
    }
});

app.Run(async context =>
{
    var request = context.Request;
    var forwardRequest = new HttpRequestMessage(new HttpMethod(request.Method), $"{targetUrl.TrimEnd('/')}{request.Path}{request.QueryString}");
    var clientEncoding = Encoding.GetEncoding(request.ContentType?.Split("charset=").LastOrDefault()?.Trim() ?? "utf-8");
    var forwardEncoding = preserveEncoding ? clientEncoding : Encoding.UTF8;
    
    Log($">>> Client request: {request.Method} {request.Scheme}://{request.Host}{request.Path}{request.QueryString}");
    Log($">>> Client encoding:  {clientEncoding.WebName}");
    Log($">>> Forward request {forwardRequest.Method} {forwardRequest.RequestUri!.AbsoluteUri}");
    Log($">>> Forward encoding: {forwardEncoding.WebName}");

    Log(">>> Headers:");
    foreach (var header in request.Headers)
        Log($">>> {header.Key}: {header.Value}");

    foreach (var header in request.Headers)
    {
        if (!string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
        {
            forwardRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    if (logBody)
    {
        using var reader = new StreamReader(request.Body, clientEncoding);
        var requestBody = await reader.ReadToEndAsync();
        Log($">>> Body:\n\n{requestBody}\n");

        var forwardBytes = forwardEncoding.GetBytes(requestBody);
        forwardRequest.Content = new ByteArrayContent(forwardBytes);
        forwardRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType ?? "application/octet-stream");
        forwardRequest.Content.Headers.ContentType.CharSet = forwardEncoding.WebName;
    }
    else
    {
        forwardRequest.Content = new StreamContent(request.Body);
        forwardRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType ?? "application/octet-stream");
    }

    HttpResponseMessage response;
    
    try
    {
        response = await httpClient.SendAsync(forwardRequest, HttpCompletionOption.ResponseHeadersRead);
    }
    catch (HttpRequestException ex)
    {
        Log($"[ERROR] Failed to forward request: {ex.Message}");
        context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        return;
    }

    Log($"<<< {(int)response.StatusCode} {response.ReasonPhrase}");
    Log("<<< Headers:");
    foreach (var header in response.Headers)
        Log($"<<< {header.Key}: {string.Join(", ", header.Value)}");

    if (logBody)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        Log($"<<< Body:\n\n{responseBody}\n");
    }

    context.Response.StatusCode = (int)response.StatusCode;
    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
    if (response.Content.Headers.ContentLength.HasValue)
        context.Response.ContentLength = response.Content.Headers.ContentLength.Value;

    foreach (var header in response.Content.Headers)
    {
        foreach (var value in header.Value)
        {
            context.Response.Headers[header.Key] = value;
        }
    }

    await using var forwardStream = await response.Content.ReadAsStreamAsync();
    await forwardStream.CopyToAsync(context.Response.Body);
});

Log($"[OK] Kestrel started on {localUrl}");

await app.RunAsync(localUrl);

return;

void Log(string line)
{
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    var formatted = $"[{timestamp}] {line}";
    Console.WriteLine(formatted);
    File.AppendAllText(logPath, formatted + Environment.NewLine);
}

X509Certificate2? FindCertificate(string subjectName)
{
    foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
    {
        using var store = new X509Store(StoreName.My, location);
        try
        {
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectName, false);
            if (certs.Count > 0)
                return certs[0];
        }
        catch
        {
            // Ignore
        }
    }
    return null;
}
