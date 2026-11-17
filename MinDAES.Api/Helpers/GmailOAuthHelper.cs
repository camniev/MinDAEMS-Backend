using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;
using System.Threading;

public class GoogleOAuthHelper
{
    public static GmailService GetGmailService(IWebHostEnvironment webHostEnvironment)
    {
        string[] Scopes = { GmailService.Scope.GmailSend };
        string ApplicationName = "MinDA Examination Management System";
        UserCredential credential;

        var wwwrootFolder = Path.Combine(webHostEnvironment.ContentRootPath, "wwwroot");
        var uploadsFolder = Path.Combine(wwwrootFolder, "uploads");
        var googleFolder = Path.Combine(uploadsFolder, "google");
        var tokenFolder = Path.Combine(googleFolder, "token");
        var fullFilePath = Path.Combine(googleFolder, "client_secret_1017921278385-hcbkhhoh2eivcn7valscob88sv4d5r0d.apps.googleusercontent.com.json");

        using (var stream = new FileStream(fullFilePath, FileMode.Open, FileAccess.Read))
        {
            string credPath = "token.json";
            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
        }

        return new GmailService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });
    }
}
