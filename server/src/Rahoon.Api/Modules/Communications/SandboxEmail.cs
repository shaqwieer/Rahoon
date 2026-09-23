using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Communications;

/// <summary>
/// E-mail channel. No provider is contracted (integration state «simulated»): the message is recorded
/// and never sent. Bodies must not contain secrets — callers redact tokens before recording.
/// </summary>
public static class SandboxEmail
{
    public static void Record(RahoonDbContext db, IClock clock, Guid? orgId, string to, string body, string? templateCode = null) =>
        db.OutboundMessages.Add(new OutboundMessage
        {
            OrganizationId = orgId, Channel = MessageChannel.Email, Destination = to, Body = body, TemplateCode = templateCode,
            Provider = "sandbox", Status = OutboundStatus.Simulated, CreatedAt = clock.UtcNow,
        });
}
