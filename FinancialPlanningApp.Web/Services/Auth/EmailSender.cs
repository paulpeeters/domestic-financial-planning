using FinancialPlanningApp.Web.Data.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace FinancialPlanningApp.Web.Services.Auth;

public sealed record EmailSendRequest(string ToEmail, string Subject, string TextBody, string? HtmlBody = null);

public interface IEmailSender
{
    Task<(bool Success, string? Error)> SendAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken = default);
}

public sealed class EmailSender(HttpClient httpClient) : IEmailSender
{
    public async Task<(bool Success, string? Error)> SendAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken = default)
    {
        if (!settings.IsEnabled || string.Equals(settings.Provider, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Mailverzending is uitgeschakeld.");
        }

        if (string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            return (false, "Afzender e-mail is niet geconfigureerd.");
        }

        return settings.Provider switch
        {
            "Brevo" => await SendBrevoAsync(settings, request, cancellationToken),
            "Resend" => await SendResendAsync(settings, request, cancellationToken),
            "Postmark" => await SendPostmarkAsync(settings, request, cancellationToken),
            "SendGrid" => await SendSendGridAsync(settings, request, cancellationToken),
            "Mailgun" => await SendMailgunAsync(settings, request, cancellationToken),
            "CustomSmtp" => await SendSmtpAsync(settings, request, cancellationToken),
            _ => (false, "Unsupported mail provider.")
        };
    }

    private async Task<(bool Success, string? Error)> SendBrevoAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken)
    {
        var payload = new
        {
            sender = Sender(settings),
            to = new[] { Recipient(request.ToEmail) },
            subject = request.Subject,
            textContent = request.TextBody,
            htmlContent = request.HtmlBody
        };

        using var httpRequest = JsonRequest(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email", payload);
        httpRequest.Headers.Add("api-key", settings.ApiKey);
        return await SendHttpAsync(httpRequest, cancellationToken);
    }

    private async Task<(bool Success, string? Error)> SendResendAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken)
    {
        var from = string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromEmail : $"{settings.FromName} <{settings.FromEmail}>";
        var payload = new
        {
            from,
            to = new[] { request.ToEmail },
            subject = request.Subject,
            text = request.TextBody,
            html = request.HtmlBody
        };

        using var httpRequest = JsonRequest(HttpMethod.Post, "https://api.resend.com/emails", payload);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        return await SendHttpAsync(httpRequest, cancellationToken);
    }

    private async Task<(bool Success, string? Error)> SendPostmarkAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken)
    {
        var payload = new
        {
            From = settings.FromEmail,
            To = request.ToEmail,
            Subject = request.Subject,
            TextBody = request.TextBody,
            HtmlBody = request.HtmlBody,
            MessageStream = "outbound"
        };

        using var httpRequest = JsonRequest(HttpMethod.Post, "https://api.postmarkapp.com/email", payload);
        httpRequest.Headers.Add("X-Postmark-Server-Token", settings.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await SendHttpAsync(httpRequest, cancellationToken);
    }

    private async Task<(bool Success, string? Error)> SendSendGridAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken)
    {
        var content = request.HtmlBody is null
            ? new[] { new { type = "text/plain", value = request.TextBody } }
            : new[] { new { type = "text/html", value = request.HtmlBody } };
        var payload = new
        {
            personalizations = new[] { new { to = new[] { Recipient(request.ToEmail) } } },
            from = Sender(settings),
            subject = request.Subject,
            content
        };

        using var httpRequest = JsonRequest(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send", payload);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        return await SendHttpAsync(httpRequest, cancellationToken);
    }

    private async Task<(bool Success, string? Error)> SendMailgunAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken)
    {
        var domain = new MailAddress(settings.FromEmail!).Host;
        using var content = new MultipartFormDataContent
        {
            { new StringContent(string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromEmail! : $"{settings.FromName} <{settings.FromEmail}>"), "from" },
            { new StringContent(request.ToEmail), "to" },
            { new StringContent(request.Subject), "subject" },
            { new StringContent(request.TextBody), "text" }
        };
        if (!string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            content.Add(new StringContent(request.HtmlBody), "html");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"https://api.mailgun.net/v3/{domain}/messages")
        {
            Content = content
        };
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{settings.ApiKey}"));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        return await SendHttpAsync(httpRequest, cancellationToken);
    }

    private static async Task<(bool Success, string? Error)> SendSmtpAsync(MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) || settings.SmtpPort is null)
        {
            return (false, "SMTP-host en poort zijn verplicht.");
        }

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(settings.SmtpHost, settings.SmtpPort.Value, cancellationToken);

        Stream stream = tcpClient.GetStream();
        var ownsStream = false;
        if (settings.SmtpUseSsl && settings.SmtpPort == 465)
        {
            var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = settings.SmtpHost }, cancellationToken);
            stream = sslStream;
            ownsStream = true;
        }

        try
        {
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            await ExpectAsync(reader, 220, cancellationToken);
            await WriteAsync(writer, $"EHLO {Dns.GetHostName()}", cancellationToken);
            await ExpectAsync(reader, 250, cancellationToken);

            if (settings.SmtpUseSsl && settings.SmtpPort != 465)
            {
                await WriteAsync(writer, "STARTTLS", cancellationToken);
                await ExpectAsync(reader, 220, cancellationToken);

                var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
                await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = settings.SmtpHost }, cancellationToken);
                ownsStream = true;
                stream = sslStream;

                using var secureReader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                await using var secureWriter = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
                {
                    NewLine = "\r\n",
                    AutoFlush = true
                };

                await WriteAsync(secureWriter, $"EHLO {Dns.GetHostName()}", cancellationToken);
                await ExpectAsync(secureReader, 250, cancellationToken);
                await AuthenticateAndSendAsync(secureReader, secureWriter, settings, request, cancellationToken);
                return (true, null);
            }

            await AuthenticateAndSendAsync(reader, writer, settings, request, cancellationToken);
            return (true, null);
        }
        finally
        {
            if (ownsStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private static async Task AuthenticateAndSendAsync(StreamReader reader, StreamWriter writer, MailSettings settings, EmailSendRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
        {
            await WriteAsync(writer, "AUTH LOGIN", cancellationToken);
            await ExpectAsync(reader, 334, cancellationToken);
            await WriteAsync(writer, Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.SmtpUsername)), cancellationToken);
            await ExpectAsync(reader, 334, cancellationToken);
            await WriteAsync(writer, Convert.ToBase64String(Encoding.UTF8.GetBytes(settings.SmtpPassword ?? string.Empty)), cancellationToken);
            await ExpectAsync(reader, 235, cancellationToken);
        }

        await WriteAsync(writer, $"MAIL FROM:<{settings.FromEmail}>", cancellationToken);
        await ExpectAsync(reader, 250, cancellationToken);
        await WriteAsync(writer, $"RCPT TO:<{request.ToEmail}>", cancellationToken);
        await ExpectAsync(reader, 250, cancellationToken);
        await WriteAsync(writer, "DATA", cancellationToken);
        await ExpectAsync(reader, 354, cancellationToken);
        await WriteAsync(writer, BuildSmtpMessage(settings, request), cancellationToken);
        await WriteAsync(writer, ".", cancellationToken);
        await ExpectAsync(reader, 250, cancellationToken);
        await WriteAsync(writer, "QUIT", cancellationToken);
    }

    private static string BuildSmtpMessage(MailSettings settings, EmailSendRequest request)
    {
        var from = string.IsNullOrWhiteSpace(settings.FromName)
            ? settings.FromEmail!
            : $"{settings.FromName} <{settings.FromEmail}>";
        var body = request.HtmlBody ?? WebUtility.HtmlEncode(request.TextBody).Replace("\n", "<br>", StringComparison.Ordinal);
        var lines = new[]
        {
            $"From: {from}",
            $"To: {request.ToEmail}",
            $"Subject: {request.Subject}",
            "MIME-Version: 1.0",
            "Content-Type: text/html; charset=utf-8",
            "Content-Transfer-Encoding: 8bit",
            string.Empty,
            body
        };

        return string.Join("\r\n", lines.Select(DotStuff));
    }

    private static string DotStuff(string line)
        => line.StartsWith(".", StringComparison.Ordinal) ? $".{line}" : line;

    private static async Task WriteAsync(StreamWriter writer, string line, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
    }

    private static async Task ExpectAsync(StreamReader reader, int expectedCode, CancellationToken cancellationToken)
    {
        var response = await ReadResponseAsync(reader, cancellationToken);
        if (response.Code != expectedCode)
        {
            throw new InvalidOperationException($"SMTP returned {response.Code}: {response.Message}");
        }
    }

    private static async Task<(int Code, string Message)> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? line;
        var message = new StringBuilder();
        int code = 0;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw new InvalidOperationException("SMTP-verbinding werd onverwacht afgesloten.");
            }

            message.AppendLine(line);
            if (line.Length >= 3 && int.TryParse(line[..3], out var parsedCode))
            {
                code = parsedCode;
            }
        }
        while (line.Length > 3 && line[3] == '-');

        return (code, message.ToString().Trim());
    }

    private async Task<(bool Success, string? Error)> SendHttpAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (body.Length > 600)
        {
            body = body[..600];
        }

        return (false, $"Provider gaf {(int)response.StatusCode} {response.ReasonPhrase} terug: {body}");
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string url, object payload)
        => new(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

    private static object Sender(MailSettings settings)
        => new { email = settings.FromEmail, name = settings.FromName };

    private static object Recipient(string email)
        => new { email };
}
