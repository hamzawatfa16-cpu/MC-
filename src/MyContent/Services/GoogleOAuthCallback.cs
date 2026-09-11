using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MyContent.Services;

internal sealed class GoogleOAuthCallback : IDisposable
{
    private const int CallbackPort = 43817;
    private readonly TcpListener _listener;

    public GoogleOAuthCallback()
    {
        _listener = new TcpListener(IPAddress.Loopback, CallbackPort);
        _listener.Start();
        RedirectUri = $"http://127.0.0.1:{CallbackPort}/auth/callback";
    }

    public string RedirectUri { get; }

    public async Task<Uri> WaitForCallbackAsync(CancellationToken cancellationToken = default)
    {
        var callbackPath = new Uri(RedirectUri).AbsolutePath;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

            var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                continue;
            }

            while (true)
            {
                var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(headerLine))
                {
                    break;
                }
            }

            var requestParts = requestLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (requestParts.Length < 2 || !requestParts[0].Equals("GET", StringComparison.Ordinal))
            {
                await WriteResponseAsync(stream, HttpStatusCode.BadRequest, "Invalid callback.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!Uri.TryCreate(new Uri(RedirectUri), requestParts[1], out var callbackUri))
            {
                await WriteResponseAsync(stream, HttpStatusCode.BadRequest, "Invalid callback URL.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!string.Equals(callbackUri.AbsolutePath, callbackPath, StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, HttpStatusCode.NotFound, "Not found.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            await WriteResponseAsync(
                stream,
                HttpStatusCode.OK,
                "<!doctype html><html><head><meta charset=\"utf-8\"><title>My Content</title></head><body><p>Google sign-in completed. You can close this window and return to My Content.</p></body></html>",
                cancellationToken).ConfigureAwait(false);

            return callbackUri;
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Dispose();
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        HttpStatusCode statusCode,
        string body,
        CancellationToken cancellationToken)
    {
        var statusText = statusCode == HttpStatusCode.OK ? "OK" : statusCode.ToString();
        var response =
            $"HTTP/1.1 {(int)statusCode} {statusText}\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\n\r\n{body}";
        var bytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
