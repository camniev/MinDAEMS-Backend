using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MinDAES.Api.Helpers
{
    // Replaces GmailOAuthHelper.cs and GmailSender.cs.
    //
    // HOW IT SENDS AS no-reply@minda.gov.ph WITHOUT ANYONE LOGGING IN
    // A Google Workspace "service account" with domain-wide delegation impersonates the
    // no-reply mailbox. There is no browser, no consent screen and no token.json; the Gmail
    // API call is made AS that user, so "From" really is no-reply@minda.gov.ph.
    //
    // ONE-TIME SETUP (a Workspace super admin must do the last step):
    //  1. Google Cloud console -> the project you already use -> enable the Gmail API.
    //  2. IAM & Admin -> Service Accounts -> create one (e.g. "minda-mailer") -> Keys -> Add key
    //     -> JSON. Keep that file OUT of wwwroot and out of git (see appsettings below).
    //  3. On the service account, copy its numeric "Client ID" (Unique ID).
    //  4. Admin console (admin.google.com) -> Security -> Access and data control -> API controls
    //     -> Manage domain-wide delegation -> Add new:
    //         Client ID: <the number from step 3>
    //         OAuth scope: https://www.googleapis.com/auth/gmail.send
    //  5. no-reply@minda.gov.ph must be a real Workspace user with a Gmail license, or an alias
    //     of one (impersonate the user that owns the alias).

    // ---- appsettings.json ----
    //   "Gmail": {
    //     "ServiceAccountKeyPath": "App_Data/google/minda-mailer.json",
    //     "SenderEmail": "no-reply@minda.gov.ph",
    //     "SenderName": "MinDAEMS-NoReply"
    //   }
    //
    // ---- Program.cs ----
    //   builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection("Gmail"));
    //   builder.Services.AddSingleton<IEmailSender, GmailEmailSender>();

    public class GmailOptions
    {
        public string ServiceAccountKeyPath { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = "no-reply@minda.gov.ph";
        public string SenderName { get; set; } = "MinDAEMS-NoReply";
    }

    public interface IEmailSender
    {
        Task SendAsync(string recipientEmail, string subject, string bodyHtml, CancellationToken ct = default);
    }

    public class GmailEmailSender : IEmailSender
    {
        private readonly GmailOptions _options;
        private readonly Lazy<GmailService> _service;

        public GmailEmailSender(IOptions<GmailOptions> options, IWebHostEnvironment env)
        {
            _options = options.Value;
            // Built once and reused. The old code rebuilt the credential for every email.
            _service = new Lazy<GmailService>(() => CreateService(env));
        }

        private GmailService CreateService(IWebHostEnvironment env)
        {
            var keyPath = Path.IsPathRooted(_options.ServiceAccountKeyPath)
                ? _options.ServiceAccountKeyPath
                : Path.Combine(env.ContentRootPath, _options.ServiceAccountKeyPath);

            // Newer Google.Apis.Auth versions mark FromFile as obsolete (a warning only). The
            // replacement is CredentialFactory.FromFile<ServiceAccountCredential>(keyPath).
            var credential = GoogleCredential
                .FromFile(keyPath)
                .CreateScoped(GmailService.Scope.GmailSend)
                .CreateWithUser(_options.SenderEmail); // <- this is what makes the mail come from no-reply

            return new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "MinDA Examination Management System",
            });
        }

        public async Task SendAsync(string recipientEmail, string subject, string bodyHtml, CancellationToken ct = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
            message.To.Add(MailboxAddress.Parse(recipientEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = bodyHtml }.ToMessageBody();

            using var ms = new MemoryStream();
            await message.WriteToAsync(ms, ct);
            var raw = Convert.ToBase64String(ms.ToArray()).Replace('+', '-').Replace('/', '_').TrimEnd('=');

            // "me" = the impersonated user, i.e. no-reply@minda.gov.ph.
            await _service.Value.Users.Messages
                .Send(new Message { Raw = raw }, "me")
                .ExecuteAsync(ct);
        }
    }

    // ---- Your controller: inject IEmailSender instead of calling GmailSender statically ----
    //
    //   private readonly IEmailSender _emailSender;      // add to the constructor
    //
    //   // TODO: remove this endpoint after testing, or at least [Authorize(Roles = "...")] it.
    //   // As written it is open to anyone, who could use it to send mail from your domain.
    //   [HttpPost("send-test-email")]
    //   public async Task<IActionResult> SendTestEmail()
    //   {
    //       var email = "cameron.nieve@minda.gov.ph";
    //       try
    //       {
    //           await _emailSender.SendAsync(email, "Test Email from MinDAEMS",
    //               GenerateEmailBodyForPasswordUpdate(email, "DMO_I_KMD_0001"));
    //           return Ok(new { message = "Test email sent successfully." });
    //       }
    //       catch (Exception ex)
    //       {
    //           return StatusCode(500, new { message = "Error sending test email", error = ex.Message });
    //       }
    //   }
}
