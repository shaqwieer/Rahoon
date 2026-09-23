using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Communications;

namespace Rahoon.Api.Infrastructure.Integrations;

/// <summary>Handoff: every integration is exactly one of these states, shown as text in the UI.</summary>
public static class IntegrationState
{
    public const string Enabled = "enabled";
    public const string Simulated = "simulated";
    public const string Pending = "pending";
    public const string Unavailable = "unavailable";
    public const string Failed = "failed";
}

public static class IntegrationKeys
{
    public const string Sms = "sms_gateway";
    public const string Email = "email";
    public const string NationalIdentity = "national_identity";
    public const string LicensedSigning = "licensed_signing";
    public const string LicensedPayment = "licensed_payment";
    public const string CoreBanking = "core_banking";
    public const string JudicialChannel = "judicial_channel";
    public const string RealEstateRegistry = "real_estate_registry";
}

public sealed class IntegrationUnavailableException(string key)
    : Http.DomainException("integration_unavailable", "هذا التكامل غير متاح حالياً؛ استخدم المسار اليدوي الموثق.", StatusCodes.Status409Conflict)
{
    public string Key { get; } = key;
}

public interface IIntegrationRegistry
{
    Task<string> StateAsync(string key);
}

public sealed class IntegrationRegistry(RahoonDbContext db) : IIntegrationRegistry
{
    public async Task<string> StateAsync(string key) =>
        await db.IntegrationSettings.Where(i => i.Key == key).Select(i => i.State).FirstOrDefaultAsync() ?? IntegrationState.Unavailable;
}

public sealed record SmsResult(bool Delivered, string Provider, string State);

/// <summary>SMS channel. No live gateway is contracted: the sandbox adapter records the message and sends nothing.</summary>
public interface ISmsGateway
{
    Task<SmsResult> SendAsync(string destination, string body, Guid? orgId = null, Guid? caseId = null, string? templateCode = null);
}

public sealed class SandboxSmsGateway(RahoonDbContext db, IClock clock, ILogger<SandboxSmsGateway> log) : ISmsGateway
{
    public Task<SmsResult> SendAsync(string destination, string body, Guid? orgId = null, Guid? caseId = null, string? templateCode = null)
    {
        db.OutboundMessages.Add(new OutboundMessage
        {
            OrganizationId = orgId, CaseId = caseId, Channel = MessageChannel.Sms, Destination = destination, Body = body,
            TemplateCode = templateCode, Provider = "sandbox", Status = OutboundStatus.Simulated, CreatedAt = clock.UtcNow,
        });
        log.LogInformation("[SANDBOX SMS — not delivered] to {Destination}: {Body}", destination, body);
        return Task.FromResult(new SmsResult(false, "sandbox", IntegrationState.Simulated));
    }
}

/// <summary>
/// Licensed e-signature, payment, national identity and official judicial channels are not
/// contracted. These adapters refuse with a clear "unavailable" state; the product uses the
/// documented manual paths instead. Replace per provider once contracts and credentials exist.
/// </summary>
public interface ILicensedSigningProvider { Task<string> StartSigningAsync(Guid agreementId); }
public interface ILicensedPaymentProvider { Task<string> CreatePaymentLinkAsync(Guid installmentId); }
public interface INationalIdentityProvider { Task<bool> VerifyAsync(string nationalId); }
public interface IJudicialChannel { Task<string> SubmitAsync(Guid referralId); }

public sealed class UnavailableSigningProvider : ILicensedSigningProvider
{
    public Task<string> StartSigningAsync(Guid agreementId) => throw new IntegrationUnavailableException(IntegrationKeys.LicensedSigning);
}

public sealed class UnavailablePaymentProvider : ILicensedPaymentProvider
{
    public Task<string> CreatePaymentLinkAsync(Guid installmentId) => throw new IntegrationUnavailableException(IntegrationKeys.LicensedPayment);
}

public sealed class UnavailableIdentityProvider : INationalIdentityProvider
{
    public Task<bool> VerifyAsync(string nationalId) => throw new IntegrationUnavailableException(IntegrationKeys.NationalIdentity);
}

public sealed class UnavailableJudicialChannel : IJudicialChannel
{
    public Task<string> SubmitAsync(Guid referralId) => throw new IntegrationUnavailableException(IntegrationKeys.JudicialChannel);
}
