using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var programName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

if (args.Contains("--version", StringComparer.OrdinalIgnoreCase))
{
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
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

if (args.Length < 3)
{
    Console.WriteLine($"Usage: {programName} <localUrl> <targetUrl> <certSubject> [--preserve-encoding] [--log-body=false]");
    return;
}

var localUrlRaw = args[0];
if (!Uri.TryCreate(localUrlRaw, UriKind.Absolute, out var localUri) || (localUri.Scheme != Uri.UriSchemeHttp && localUri.Scheme != Uri.UriSchemeHttps))
{
    Console.WriteLine("[ERROR] Invalid localUrl. It must be a valid HTTP or HTTPS URL.");
    return;
}

var localUrl = localUrlRaw.EndsWith('/') ? localUrlRaw : localUrlRaw + "/";

var targetUrlRaw = args[1];
if (!Uri.TryCreate(targetUrlRaw, UriKind.Absolute, out var targetUri) || (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
{
    Console.WriteLine("[ERROR] Invalid targetUrl. It must be a valid HTTP or HTTPS URL.");
    return;
}
var targetUrl = targetUrlRaw.EndsWith('/') ? targetUrlRaw : targetUrlRaw + "/";

var certSubject = args[2];

var preserveEncoding = args.Contains("--preserve-encoding", StringComparer.OrdinalIgnoreCase);
var logBody = !args.Contains("--log-body=false", StringComparer.OrdinalIgnoreCase);

var logPath = Path.Combine(
    OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share"),
    programName,
    "proxy.log"
);

// ReSharper disable once NullableWarningSuppressionIsUsed
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

var clientCert = FindCertificate(certSubject);

if (clientCert is null)
{
    Log("[ERROR] Certificate not found.");
    return;
}

Log($"Starting {programName} with Kestrel");
Log($"Listening on: {localUrl}");
Log($"Forwarding to: {targetUrl}");
Log($"Client Certificate: {clientCert.Subject}");
Log($"Preserve Encoding: {(preserveEncoding ? "Yes" : "No")}");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    var uri = new Uri(localUrl);

    options.ListenAnyIP(uri.Port, listenOptions =>
    {
        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            listenOptions.UseHttps(clientCert, httpsOptions =>
            {
                httpsOptions.SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12;
            });
        }
    });
});

builder.Logging.ClearProviders();

builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning); 
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Warning);

var app = builder.Build();

var cts = new CancellationTokenSource();
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
            var key = Console.ReadKey(true);
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
    try
    {
        var clientHandler = new HttpClientHandler
        {
            ClientCertificates = { clientCert },
            SslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var httpClient = new HttpClient(clientHandler);
        var forwardUri = new Uri(new Uri(targetUrl), context.Request.Path + context.Request.QueryString);

        var forwardRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), forwardUri);

        foreach (var header in context.Request.Headers)
            forwardRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());

        var clientEncoding = Encoding.UTF8;
        if (context.Request.ContentType != null && context.Request.ContentType.Contains("charset=", StringComparison.OrdinalIgnoreCase))
        {
            var charset = context.Request.ContentType.Split("charset=", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[1];
            clientEncoding = Encoding.GetEncoding(charset);
        }

        var forwardEncoding = preserveEncoding ? clientEncoding : Encoding.UTF8;

        Log($">>> {context.Request.Method} {context.Request.Path}");
        
        if (context.Request.Headers.Any())
            Log(">>> Headers:");
            
        foreach (var header in context.Request.Headers)
            Log($">>> {header.Key}: {string.Join(",", header.Value.AsEnumerable())}");

        if (context.Request.ContentLength > 0)
        {
            using var reader = new StreamReader(context.Request.Body, clientEncoding);
            var requestBody = await reader.ReadToEndAsync();
            if (logBody)
                Log($">>> Body:\n\n{requestBody}\n");

            var mediaType = context.Request.ContentType?.Split(';', 2, StringSplitOptions.TrimEntries)[0] ?? "application/octet-stream";
            var content = new StringContent(requestBody, forwardEncoding, mediaType);

            if (context.Request.ContentType?.Contains("charset=", StringComparison.OrdinalIgnoreCase) == true || preserveEncoding)
            {
                content.Headers.ContentType!.CharSet = forwardEncoding.WebName;
            }

            forwardRequest.Content = content;
        }

        var forwardResponse = await httpClient.SendAsync(forwardRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        context.Response.StatusCode = (int)forwardResponse.StatusCode;

        Log($"<<< {(int)forwardResponse.StatusCode} {forwardResponse.ReasonPhrase}");

        if (forwardResponse.Headers.Any())
            Log("<<< Headers:");

        foreach (var header in forwardResponse.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
            Log($"<<< {header.Key}: {string.Join(",", header.Value)}");
        }

        foreach (var header in forwardResponse.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
            Log($"<<< {header.Key}: {string.Join(",", header.Value.AsEnumerable())}");
        }

        var responseBody = await forwardResponse.Content.ReadAsStringAsync(cts.Token);
        if (logBody)
            Log($"<<< Body:\n\n{responseBody}\n");

        await context.Response.WriteAsync(responseBody, forwardEncoding, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Log("[INFO] Request canceled by shutdown signal.");
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    }
    catch (Exception ex)
    {
        Log($"[ERROR] Exception during forwarding: {ex.Message}");
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
    }
});


await app.RunAsync(cts.Token);

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
        store.Open(OpenFlags.ReadOnly);
        var certs = store.Certificates.Find(X509FindType.FindBySubjectName, subjectName, false);
        if (certs.Count > 0) return certs[0];
    }
    return null;
}
