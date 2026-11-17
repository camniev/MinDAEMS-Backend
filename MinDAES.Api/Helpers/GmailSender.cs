using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using MimeKit;
using System.IO;
using System.Text;

public class GmailSender
{
    public static void SendEmail(string recipientEmail, string subject, string bodyHtml, IWebHostEnvironment webHostEnvironment)
    {
        var service = GoogleOAuthHelper.GetGmailService(webHostEnvironment);

        var emailMessage = new MimeMessage();
        emailMessage.From.Add(new MailboxAddress("MinDAEMS-NoReply", "no-reply@minda.gov.ph"));
        emailMessage.To.Add(new MailboxAddress("", recipientEmail));
        emailMessage.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = bodyHtml };
        emailMessage.Body = bodyBuilder.ToMessageBody();

        using (var memoryStream = new MemoryStream())
        {
            emailMessage.WriteTo(memoryStream);
            var rawMessage = Convert.ToBase64String(memoryStream.ToArray()).Replace('+', '-').Replace('/', '_').Replace("=", "");

            var message = new Message { Raw = rawMessage };

            service.Users.Messages.Send(message, "me").Execute();
        }
    }
}
