using System.Net;

// The login-details email. Replaces GenerateEmailBodyForPasswordUpdate.
// Changes from the old template:
//  * username and code are HTML-encoded (they come from imported data);
//  * the link comes from configuration, not a hard-coded localhost address;
//  * styles are inline and the layout is a table (Outlook ignores <style> blocks and flexbox);
//  * no "change your password" line: applicant accounts have no password change and are
//    removed when the exam closes.
//
// ---- appsettings.json ----
//   "App":   { "BaseUrl": "https://<where the web app is hosted>" },
//   "Email": { "HeaderImageUrl": "https://<an image on your own domain>/email-header.png" }
// (Drive "uc?export=view" links are often blocked or rate-limited, so host the header
//  image yourself. Leave HeaderImageUrl empty to send the email without it.)
public static class ApplicantEmailTemplate
{
    public const string Subject = "Your MinDA Examination System login details";

    // examTitle: since every exam now has its own applicant code, the email says which exam it is for.
    public static string Credentials(string username, string code, string loginUrl, string headerImageUrl, bool isResend, string? examTitle = null)
    {
        const string label = "width:35%;padding:6px 8px;text-align:right;font-weight:bold;color:#7d7d7d;vertical-align:top;";
        const string value = "padding:6px 8px;font-weight:bold;color:#8950FC;font-family:Consolas,'Courier New',monospace;word-break:break-all;";

        var examRow = string.IsNullOrWhiteSpace(examTitle)
            ? string.Empty
            : $"<tr><td style=\"{label}\">Examination:</td><td style=\"padding:6px 8px;font-weight:bold;color:#212631;\">{WebUtility.HtmlEncode(examTitle)}</td></tr>";

        var heading = isResend
            ? "Here are your login details for the <span style=\"font-weight:bold;color:#8950FC;\">MinDA Examination System</span> again."
            : "Welcome! Below are your account details to log in to the <span style=\"font-weight:bold;color:#8950FC;\">MinDA Examination System</span>.";

        var image = string.IsNullOrWhiteSpace(headerImageUrl)
            ? string.Empty
            : $"<tr><td><img src=\"{WebUtility.HtmlEncode(headerImageUrl)}\" alt=\"MinDAEMS\" width=\"600\" style=\"display:block;width:100%;border:0;\" /></td></tr>";

        return $@"<!DOCTYPE html>
            <html>
            <body style=""margin:0;padding:0;background-color:#e7e8f0;"">
            <table width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" bgcolor=""#e7e8f0"">
            <tr><td align=""center"" style=""padding:20px 0;"">
                <table width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width:600px;width:100%;background-color:#f9f9f9;font-family:Arial,sans-serif;"">
                {image}
                <tr><td style=""padding:24px;"">
                    <h3 style=""margin:0 0 12px 0;text-align:center;font-size:18px;"">{heading}</h3>
                    <table width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""margin-top:8px;"">
                    {examRow}
                    <tr><td style=""{label}"">Username:</td><td style=""{value}"">{WebUtility.HtmlEncode(username)}</td></tr>
                    <tr><td style=""{label}"">Password / Applicant Code:</td><td style=""{value}"">{WebUtility.HtmlEncode(code)}</td></tr>
                    </table>
                    <p style=""margin:20px 0 0 0;text-align:center;font-size:13px;color:#7d7d7d;"">
                    Keep your applicant code private. Your account is only valid until your exam schedule closes.
                    </p>
                    <table cellspacing=""0"" cellpadding=""0"" border=""0"" align=""center"" style=""margin-top:28px;"">
                    <tr><td align=""center"" bgcolor=""#83c1ff"" style=""border:3px solid #212631;border-bottom-width:6px;border-radius:10px;"">
                        <a href=""{WebUtility.HtmlEncode(loginUrl)}"" style=""display:inline-block;padding:14px 48px;color:#212631;font-weight:600;text-decoration:none;"">Open the App</a>
                    </td></tr>
                    </table>
                    <p style=""margin:32px 0 0 0;text-align:center;font-size:12px;color:#777;"">This is an automated message. Please do not reply.</p>
                </td></tr>
                </table>
            </td></tr>
            </table>
            </body>
            </html>";
    }
}