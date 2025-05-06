/*
MIT License

Copyright (c) 2025 Martin Fredriksson

Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;

// Not available in args[0], due to top-level statements...
var programName = Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);

// ==== Logging target ====
var logPath = Path.Combine(
    OperatingSystem.IsWindows()
        ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.local/share",
    programName,
    "proxy.log"
);

Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

// ==== Argument handling ====

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

// ==== Cancellation Token Setup ====

var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;

// Handle Ctrl+C or SIGTERM
Console.CancelKeyPress += (_, e) =>
{
    Log("[INFO] Shutdown signal received. Stopping...");
    cts.Cancel();
    e.Cancel = true; // Prevent immediate termination
};

// ==== HTTP och proxy ====

var listener = new HttpListener();
listener.Prefixes.Add(localUrl);
listener.Start();
Log($"[OK] HttpListener started on {localUrl}");

var handler = new HttpClientHandler();
handler.ClientCertificates.Add(clientCert);
handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

var httpClient = new HttpClient(handler);

try
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var context = await listener.GetContextAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    var request = context.Request;
                    var clientEncoding = request.ContentEncoding;
                    var forwardEncoding = preserveEncoding ? clientEncoding : Encoding.UTF8;

                    Log($">>> {request.HttpMethod} {request.Url}");
                    Log($">>> Client encoding:  {clientEncoding.WebName}");
                    Log($">>> Forward encoding: {forwardEncoding.WebName}");

                    Log(">>> Headers:");
                    foreach (string header in request.Headers)
                        Log($">>> {header}: {request.Headers[header]}");

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

                    if (logBody)
                    {
                        using var reader = new StreamReader(request.InputStream, clientEncoding);
                        var requestBody = await reader.ReadToEndAsync();
                        Log($">>> Body: {requestBody}");

                        var forwardBytes = forwardEncoding.GetBytes(requestBody);
                        forwardRequest.Content = new ByteArrayContent(forwardBytes);
                        forwardRequest.Content.Headers.ContentType =
                            MediaTypeHeaderValue.Parse(request.ContentType ?? "application/octet-stream");
                        forwardRequest.Content.Headers.ContentType.CharSet = forwardEncoding.WebName;
                    }
                    else
                    {
                        forwardRequest.Content = new StreamContent(request.InputStream);
                        forwardRequest.Content.Headers.ContentType =
                            MediaTypeHeaderValue.Parse(request.ContentType ?? "application/octet-stream");
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
                        context.Response.Close();
                        return;
                    }

                    Log($"<<< {((int)response.StatusCode)} {response.ReasonPhrase}");
                    Log("<<< Headers:");
                    foreach (var header in response.Headers)
                        Log($"<<< {header.Key}: {string.Join(", ", header.Value)}");

                    var isCompressed = response.Content.Headers.ContentEncoding.Contains("gzip", StringComparer.OrdinalIgnoreCase);

                    if (logBody)
                    {
                        if (isCompressed)
                        {
                            await using var rawStream = await response.Content.ReadAsStreamAsync();
                            await using var gzipStream = new GZipStream(rawStream, CompressionMode.Decompress);
                            using var reader = new StreamReader(gzipStream, clientEncoding);
                            var decompressed = await reader.ReadToEndAsync();
                            Log($"<<< Body (decompressed): {decompressed}");
                        }
                        else
                        {
                            var responseBody = await response.Content.ReadAsStringAsync();
                            Log($"<<< Body: {responseBody}");
                        }
                    }

                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

                    if (response.Content.Headers.ContentLength.HasValue)
                        context.Response.ContentLength64 = response.Content.Headers.ContentLength.Value;

                    foreach (var header in response.Content.Headers)
                    {
                        foreach (var value in header.Value)
                        {
                            context.Response.Headers[header.Key] = value;
                        }
                    }

                    await using var forwardStream = await response.Content.ReadAsStreamAsync();
                    await forwardStream.CopyToAsync(context.Response.OutputStream);
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] An error occurred while processing the request: {ex.Message}");
                    try
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        context.Response.Close();
                    }
                    catch
                    {
                        // Ignore errors during response cleanup
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log($"[ERROR] An error occurred while accepting a connection: {ex.Message}");
        }
    }
}
finally
{
    listener.Stop();
    Log("[INFO] HttpListener stopped.");
}

return;

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
