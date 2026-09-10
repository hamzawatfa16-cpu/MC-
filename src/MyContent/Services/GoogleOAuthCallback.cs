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
        using var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            throw new InvalidOperationException("Google sign-in returned an empty callback.");
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
            throw new InvalidOperationException("Google sign-in returned an invalid callback.");
        }

        if (!Uri.TryCreate(new Uri(RedirectUri), requestParts[1], out var callbackUri))
        {
            throw new InvalidOperationException("Google sign-in returned an invalid callback URL.");
        }

        const string response = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\n\r\n<!doctype html><html><head><meta charset=\"utf-8\"><title>My Content</title></head><body><p>Google sign-in completed. You can close this window and return to My Content.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        return callbackUri;
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Dispose();
    }
}
