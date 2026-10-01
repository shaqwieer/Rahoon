using System.Text.RegularExpressions;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Infrastructure.Integrations;

/// <summary>Every integration is exactly one of these states; the UI never presents a simulated one as real.</summary>
public static class IntegrationState
{
    public const string Enabled = "enabled";
    public const string Simulated = "simulated";
    public const string Unavailable = "unavailable";
    public const string Failed = "failed";
}

public sealed record SmsResult(bool Delivered, string Provider, string State);

/// <summary>SMS channel. No live gateway is contracted yet: the sandbox adapter records the message and sends nothing.</summary>
public interface ISmsGateway
{
    Task<SmsResult> SendAsync(string destination, string body);
}

/// <remarks>The record keeps the masked number and the text with one-time codes blanked: codes are stored only hashed.</remarks>
public sealed partial class SandboxSmsGateway(RahoonDbContext db, IClock clock, ILogger<SandboxSmsGateway> log) : ISmsGateway
{
    [GeneratedRegex(@"\d{4,}")]
    private static partial Regex Codes();

    public Task<SmsResult> SendAsync(string destination, string body)
    {
        var masked = Mask.Phone(destination);
        var safeBody = Codes().Replace(body, m => new string('•', m.Length));
        db.OutboundSms.Add(new OutboundSms { Destination = masked, Body = safeBody, Provider = "sandbox", Result = "simulated", CreatedAt = clock.UtcNow });
        log.LogInformation("[SANDBOX SMS — not delivered] to {Destination}: {Body}", masked, safeBody);
        return Task.FromResult(new SmsResult(false, "sandbox", IntegrationState.Simulated));
    }
}
