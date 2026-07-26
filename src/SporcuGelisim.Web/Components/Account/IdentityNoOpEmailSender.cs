using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using SporcuGelisim.Infrastructure.Identity;

namespace SporcuGelisim.Web.Components.Account;

public interface IAccountEmailSender
{
    Task SendEmailConfirmationCodeAsync(ApplicationUser user, string email, string code);
    Task SendEmailChangeCodeAsync(ApplicationUser user, string email, string code);
}

public sealed class AccountEmailSendException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

internal sealed class AccountEmailSender(IConfiguration configuration, ILogger<AccountEmailSender> logger)
    : IEmailSender<ApplicationUser>, IAccountEmailSender
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendEmailAsync(email, "E-posta onayı", $"Hesabınızı onaylamak için bağlantıyı açın: {confirmationLink}");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendEmailAsync(email, "Şifre sıfırlama", $"Şifrenizi sıfırlamak için bağlantıyı açın: {resetLink}");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendEmailAsync(email, "Şifre sıfırlama kodu", $"Şifre sıfırlama kodunuz: {resetCode}");

    public Task SendEmailConfirmationCodeAsync(ApplicationUser user, string email, string code) =>
        SendEmailAsync(
            email,
            "Sporcu Gelişim Platformu e-posta onay kodu",
            $"""
            Merhaba {user.FullName},

            Sporcu Gelişim Platformu hesabınızı onaylamak için kodunuz: {code}

            Bu kod 30 dakika geçerlidir.
            """);

    public Task SendEmailChangeCodeAsync(ApplicationUser user, string email, string code) =>
        SendEmailAsync(
            email,
            "Sporcu Gelişim Platformu e-posta değişiklik kodu",
            $"""
            Merhaba {user.FullName},

            E-posta adresinizi bu adrese taşımak için kodunuz: {code}

            Bu kod 30 dakika geçerlidir.
            """);

    private async Task SendEmailAsync(string to, string subject, string body)
    {
        var host = configuration["Email:Smtp:Host"];
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        var fromAddress = configuration["Email:FromAddress"];
        var fromName = configuration["Email:FromName"] ?? "Sporcu Gelişim Platformu";

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new AccountEmailSendException("SMTP sunucusu tanımlı değil. Email:Smtp:Host ayarını user-secrets veya ortam değişkeni ile girin.");
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            fromAddress = username;
        }

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new AccountEmailSendException("Gönderen e-posta adresi tanımlı değil. Email:FromAddress veya Email:Smtp:Username ayarını girin.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = body
        };
        message.To.Add(to);

        using var client = new SmtpClient(host, configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = configuration.GetValue("Email:Smtp:EnableSsl", true)
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        try
        {
            await client.SendMailAsync(message);
            logger.LogInformation("E-posta gönderildi. Alıcı: {Email}, Konu: {Subject}", to, subject);
        }
        catch (SmtpException exception)
        {
            logger.LogError(exception, "E-posta gönderilemedi. Alıcı: {Email}, Konu: {Subject}", to, subject);
            throw new AccountEmailSendException("E-posta gönderilemedi. SMTP sunucusu, kullanıcı adı, uygulama şifresi ve gönderen adresi ayarlarını kontrol edin.", exception);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "E-posta ayarları geçersiz. Alıcı: {Email}, Konu: {Subject}", to, subject);
            throw new AccountEmailSendException("E-posta ayarları geçersiz. SMTP ayarlarını kontrol edin.", exception);
        }
    }
}
