using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;

internal static class PlatformClaimTypes
{
    public const string SessionId = "scp_session_id";
}

internal sealed class PlatformCookieEvents(
    PlatformDbContext database,
    TimeProvider timeProvider) : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        string? sessionValue = context.Principal?.FindFirstValue(PlatformClaimTypes.SessionId);
        string? userValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sessionValue, out Guid sessionId) ||
            !Guid.TryParse(userValue, out Guid userId))
        {
            context.RejectPrincipal();
            return;
        }

        var state = await database.AuthenticationSessions
            .Where(value => value.Id == sessionId && value.UserId == userId)
            .Join(database.UserAccounts,
                session => session.UserId,
                user => user.Id,
                (session, user) => new { Session = session, User = user })
            .Join(database.PasswordCredentials,
                value => value.User.Id,
                credential => credential.UserId,
                (value, credential) => new { value.Session, value.User, Credential = credential })
            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (state is null || state.User.State != UserAccountState.Active ||
            !state.Session.IsValid(now, state.Credential.SecurityVersion))
        {
            context.RejectPrincipal();
            return;
        }

        if (state.Session.Touch(now))
            await database.SaveChangesAsync(context.HttpContext.RequestAborted);
    }
}
