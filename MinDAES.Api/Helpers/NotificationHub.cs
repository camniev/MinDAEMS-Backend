using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

// Replaces your NotificationHub.cs. Three problems in the first version:
//  1. No [Authorize]: anyone could connect, with or without a token.
//  2. NotifyStatusUpdate was callable BY CLIENTS and sent to Clients.All, so any connected
//     browser could broadcast a fake notification to everybody. Notifications must only ever
//     originate on the server (IHubContext), so there are no client-callable methods at all.
//  3. AddToGroupAsync wasn't awaited, so the group join could still be running (or fail
//     silently) when the connection was already considered open.
[Authorize]
public class NotificationHub : Hub
{
    // Every staff (non-applicant) connection joins this group; exam alerts go to it.
    public const string ProctorsGroup = "proctors";

    // Matches the role claim AuthController issues and ApplicantController checks.
    private const string ApplicantRole = "Applicant";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Per-user group, so something can be sent to one specific person
        // (_hubContext.Clients.Group(userId).SendAsync(...), as in your earlier app).
        if (userId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);

        // Applicants never need the hub: they report exam events through a normal API call.
        if (Context.User?.IsInRole(ApplicantRole) != true)
            await Groups.AddToGroupAsync(Context.ConnectionId, ProctorsGroup);

        await base.OnConnectedAsync();
    }
}

// ---- Program.cs checklist (you already have most of this) ----
//  * AddSignalR(), AddJwtBearer with the OnMessageReceived access_token handler,
//    app.MapHub<NotificationHub>("/notificationHub")  -> all as you wrote them.
//  * Order: app.UseCors(...); app.UseAuthentication(); app.UseAuthorization(); app.MapHub(...)
//  * CORS: SignalR from the browser sends credentials, which a wildcard origin can't allow:
//        policy.WithOrigins("https://<your web app>", "http://localhost:3000")
//              .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
//  * Hosting on IIS: the WebSocket Protocol feature must be installed, otherwise SignalR
//    quietly falls back to long polling (it still works, just less efficiently).