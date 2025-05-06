using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;

// ==== Logging target ====
var logPath = Path.Combine(
    OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.local/share",
    "certfwd",
    "proxy.log"
);

Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

// ==== Argument handling ====
if (args.Length < 4)
{
    Console.WriteLine("Usage: certfwd <localUrl> <targetUrl> <certSubject> [--preserve-encoding] [--log-body=false]");
    return;
}

var localUrl = args[1];
var targetUrl = args[2];
var certSubject = args[3];
var preserveEncoding = args.Contains("--preserve-encoding", StringComparer.OrdinalIgnoreCase);
var logBody = !args.Contains("--log-body=false", StringComparer.OrdinalIgnoreCase);

Log($"Listen on: {localUrl}");
Log($"Forward to: {targetUrl}");
Log($"Find certificate: {certSubject}");
Log($"Preserve encoding forward: {(preserveEncoding ? "YES" : "NO (UTF-8 used)")}");
Log($"Log to: {logPath}");

var clientCert = FindCertificate(certSubject);
if (clientCert == null)
{
    Log("[ERROR] Certificate not found.");
    return;
}
Log($"[OK] Certifikat hittat: {clientCert.Subject}");

// ==== HTTP och proxy ====
var listener = new HttpListener();
listener.Prefixes.Add(localUrl);
listener.Start();
Log($"[OK] HttpListener started on {localUrl}");

var handler = new HttpClientHandler();
handler.ClientCertificates.Add(clientCert);
handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

var httpClient = new HttpClient(handler);

while (true)
{
    var context = await listener.GetContextAsync();
    _ = Task.Run(async () =>
    {
        var request = context.Request;
        var clientEncoding = request.ContentEncoding;
        var forwardEncoding = preserveEncoding ? clientEncoding : Encoding.UTF8;

        Log($">>> {request.HttpMethod} {request.Url}");
        Log($">>> Client encoding:  {clientEncoding.WebName}");
        Log($">>> Forward encoding: {forwardEncoding.WebName}");

        using var reader = new StreamReader(request.InputStream, clientEncoding);
        var requestBody = await reader.ReadToEndAsync();

        Log($">>> Headers:");
        foreach (string header in request.Headers)
            Log($">>> {header}: {request.Headers[header]}");

        Log($">>> Body: {(logBody ? requestBody : "[hidden]")}");

        var forwardUriBase = new Uri(targetUrl);
        var finalTarget = new Uri(forwardUriBase, request.RawUrl);
        var forwardRequest = new HttpRequestMessage(new HttpMethod(request.HttpMethod), finalTarget);
        Log($">>> Forward target: {finalTarget}");

        foreach (string header in request.Headers)
        {
            if (!WebHeaderCollection.IsRestricted(header) &&
                !string.Equals(header, "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(header, "Host", StringComparison.OrdinalIgnoreCase))
            {
                forwardRequest.Headers.TryAddWithoutValidation(header, request.Headers[header]);
            }
        }
        var forwardBytes = forwardEncoding.GetBytes(requestBody);
        forwardRequest.Content = new ByteArrayContent(forwardBytes);
        forwardRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml")
        {
            CharSet = forwardEncoding.WebName
        };

        var response = await httpClient.SendAsync(forwardRequest);
        var responseBody = await response.Content.ReadAsStringAsync();

        Log($"<<< {((int)response.StatusCode)} {response.ReasonPhrase}");
        Log("<<< Headers:");
        foreach (var header in response.Headers)
            Log($"<<< {header.Key}: {string.Join(", ", header.Value)}");

        Log($"<<< Body: {(logBody ? responseBody : "[hidden]")}");

        var responseBytes = clientEncoding.GetBytes(responseBody);
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = "text/xml; charset=" + clientEncoding.WebName;
        context.Response.ContentLength64 = responseBytes.Length;
        await context.Response.OutputStream.WriteAsync(responseBytes);
        context.Response.Close();
    });
}


// ==== Logging ====
void Log(string line)
{
    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
    var formatted = $"[{timestamp}] {line}";
    Console.WriteLine(formatted);
    File.AppendAllText(logPath, formatted + Environment.NewLine);
}


// ==== Certificate ====
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
            // Ignore error
        }
    }
    return null;
}
