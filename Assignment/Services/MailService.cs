using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Assignment.Services
{
    public static class MailService
    {
        private static readonly string GoogleMailerClientId = "134136435658-h2v9469tijnbgjrsqijpbs2p3tmttt8h.apps.googleusercontent.com";
        private static readonly string GoogleMailerClientSecret = "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";
        private static readonly string GoogleMailerRefreshToken = "1//04By0Ljpi4-IeCgYIARAAGAQSNwF-L9IrD7NT7OGcVnx1rmuHmV4WJdU2OF8CNse6hHDQhloBxqh9mmsh_56X4BrbAUDNA-MHlks";
        private static readonly string GoogleMailerEmailPrimaryAddress = "zombiegenzzz@gmail.com";
        private static readonly string GoogleMailerEmailSendAddress = "zombiegenzzz@gmail.com";

        public static async Task<bool> SendMailAsync(string to, string subject, string html, string fromEmail = null)
        {
            try
            {
                if (string.IsNullOrEmpty(fromEmail))
                {
                    fromEmail = GoogleMailerEmailSendAddress;
                }

                var credential = new UserCredential(new GoogleAuthorizationCodeFlow(
                    new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = new ClientSecrets
                        {
                            ClientId = GoogleMailerClientId,
                            ClientSecret = GoogleMailerClientSecret
                        },
                        Scopes = new[] { "https://mail.google.com/" },
                        DataStore = new FileDataStore("TokenStore")
                    }),
                    GoogleMailerEmailPrimaryAddress,
                    new TokenResponse { RefreshToken = GoogleMailerRefreshToken }
                );

                await credential.RefreshTokenAsync(CancellationToken.None);
                var accessToken = credential.Token.AccessToken;

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("", fromEmail));
                message.To.Add(new MailboxAddress("", to));
                message.Subject = subject;

                message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
                {
                    Text = html
                };

                using (var client = new SmtpClient())
                {
                    client.Connect("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect);

                    var oauth2 = new SaslMechanismOAuth2(GoogleMailerEmailPrimaryAddress, accessToken);
                    await client.AuthenticateAsync(oauth2);

                    await client.SendAsync(message);
                    client.Disconnect(true);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi gửi email: {ex.Message}");
                return false;
            }
        }
    }
}
