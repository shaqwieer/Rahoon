using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record ConditionalState(
    string Capability, string State, string LabelAr, string Tone, bool OwnerVisible, bool LicensedPathAvailable,
    string ProviderLabel, string ManualAlternative, DateTimeOffset? LastUpdateAt, DateTimeOffset? LastAttemptAt);

/// <summary>
/// Conditional licensed integrations (X01 signing, X02 payment). The licensed path is offered only when (1) the
/// platform integration state is <c>enabled</c>, (2) the institution has an <c>enabled</c> licensed provider row and
/// (3) a real adapter replaced the Unavailable* registration of <see cref="ILicensedSigningProvider"/> /
/// <see cref="ILicensedPaymentProvider"/> in DI. Otherwise the manual path applies (consent record + OTP, bank
/// transfer with a manually matched reference). Simulated mode is never shown to the owner and has no effect.
/// To plug in a provider: implement the interface, register it in Program.cs, set the platform state to enabled
/// and the institution row to enabled — no endpoint changes are needed.
/// </summary>
public sealed class ConditionalIntegrations(RahoonDbContext db, IIntegrationRegistry registry, ILicensedSigningProvider signing, ILicensedPaymentProvider payment)
{
    public static readonly string[] Capabilities = [IntegrationKeys.LicensedSigning, IntegrationKeys.LicensedPayment];

    public bool AdapterRegistered(string capability) => capability == IntegrationKeys.LicensedSigning
        ? signing is not UnavailableSigningProvider
        : payment is not UnavailablePaymentProvider;

    public static (string Label, string Tone) Label(string state, string capability) => state switch
    {
        IntegrationState.Enabled => (capability == IntegrationKeys.LicensedSigning ? "مفعّل · مزود توقيع مرخّص" : "مفعّل · بوابة دفع مرخّصة", "ok"),
        IntegrationState.Simulated => ("محاكاة", "info"),
        IntegrationState.Pending => ("قيد المعالجة", "warn"),
        IntegrationState.Failed => ("فشل", "err"),
        _ => ("غير متاح مؤقتاً", "neutral"),
    };

    public static string Manual(string capability) => capability == IntegrationKeys.LicensedSigning
        ? "الموافقة داخل المنصة برمز تحقق (سجل موافقة وليس توقيعاً مرخّصاً)."
        : "التحويل البنكي إلى حساب الجهة الممولة، مع تسجيل المرجع ومطابقته يدوياً.";

    public async Task<ConditionalState> ResolveAsync(Guid institutionId, string capability, bool forOwner)
    {
        var platform = await registry.StateAsync(capability);
        var cfg = await db.Set<InstitutionIntegration>().AsNoTracking().FirstOrDefaultAsync(x => x.InstitutionOrganizationId == institutionId && x.Capability == capability);
        string state;
        if (cfg?.Mode == "simulated") state = forOwner ? IntegrationState.Unavailable : IntegrationState.Simulated;
        else if (platform == IntegrationState.Enabled && cfg?.Mode == "enabled" && AdapterRegistered(capability))
            state = cfg.Health switch { "available" => IntegrationState.Enabled, "pending" => IntegrationState.Pending, "failed" => IntegrationState.Failed, _ => IntegrationState.Unavailable };
        else state = IntegrationState.Unavailable;
        var last = await db.Set<IntegrationAttempt>().AsNoTracking().Where(a => a.InstitutionOrganizationId == institutionId && a.Capability == capability)
            .OrderByDescending(a => a.At).Select(a => (DateTimeOffset?)a.At).FirstOrDefaultAsync();
        var (label, tone) = Label(state, capability);
        return new ConditionalState(capability, state, label, tone, OwnerVisible: state != IntegrationState.Simulated, LicensedPathAvailable: state == IntegrationState.Enabled,
            cfg?.ProviderLabel ?? (capability == IntegrationKeys.LicensedSigning ? "مزود توقيع مرخّص (مكان محجوز)" : "بوابة دفع مرخّصة (مكان محجوز)"),
            Manual(capability), cfg?.LastUpdateAt, last);
    }
}

public static class ConditionalIntegrationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/integrations/conditional", Get).RequireSession();
        var owner = app.MapGroup("/api/owner").RequireOwner();
        owner.MapPost("/agreement/signature-sessions", StartSigning).Idempotent();
        owner.MapPost("/installments/{no:int}/payment-attempts", StartPayment).Idempotent();
    }

    private static readonly object StateTable = new[]
    {
        new { state = IntegrationState.Enabled, label = "مفعّل", owner = "زر المسار المرخّص + بديل يدوي ظاهر", team = "الحالة من المزود مع وقت آخر تحديث", fallback = "المسار اليدوي دائماً" },
        new { state = IntegrationState.Simulated, label = "محاكاة", owner = "لا يظهر للمالك", team = "وسم «محاكاة» على كل سجل؛ لا أثر مالي أو قانوني", fallback = "—" },
        new { state = IntegrationState.Pending, label = "قيد المعالجة", owner = "«نتحقق من العملية» بلا تكرار الدفع", team = "انتظار رد المزود؛ لا تُعلَّم الدفعة مطابقة", fallback = "إشعار عند الاكتمال" },
        new { state = IntegrationState.Unavailable, label = "غير متاح", owner = "رسالة هادئة + بديل", team = "تنبيه تشغيلي", fallback = "المسار اليدوي" },
        new { state = IntegrationState.Failed, label = "فشل", owner = "«لم يُخصم أي مبلغ» عند التأكد فقط", team = "سجل الفشل ورمزه", fallback = "إعادة المحاولة أو يدوي" },
    };

    /// <summary>State per capability for the caller's institution (lender staff, or the owner's lender); platform staff pass ?institutionId.</summary>
    private static async Task<IResult> Get(Guid? institutionId, RahoonDbContext db, RequestContext rc, ConditionalIntegrations integrations)
    {
        Guid orgId;
        var forOwner = rc.IsOwner;
        if (rc.IsOwner || rc.IsLenderStaff) orgId = rc.OrganizationId!.Value;
        else if (rc.IsPlatform && rc.Has(P.PlatformIntegrations) && institutionId is { } i && await db.Organizations.AnyAsync(o => o.Id == i && o.Kind == OrganizationKind.Lender)) orgId = i;
        else throw new ForbiddenException();
        var states = new List<ConditionalState>();
        foreach (var cap in ConditionalIntegrations.Capabilities) states.Add(await integrations.ResolveAsync(orgId, cap, forOwner));
        return Results.Ok(new
        {
            intro = "تظهر فقط إذا فعّلت المنشأة مزوداً مرخّصاً معتمداً. في غير ذلك يبقى المسار اليدوي (سجل الموافقة، التحويل البنكي).",
            capabilities = states.Select(s => new
            {
                s.Capability, s.State, label = s.LabelAr, s.Tone, s.LicensedPathAvailable, s.ManualAlternative,
                provider = forOwner && !s.LicensedPathAvailable ? null : s.ProviderLabel,
                lastUpdateAt = forOwner ? null : s.LastUpdateAt, s.LastAttemptAt,
            }),
            stateTable = forOwner ? null : StateTable,
        });
    }

    private static IResult Unavailable(ConditionalState s, string title, string body, object? facts) => Results.Json(new
    {
        type = "https://rahoon.example/problems/integration_unavailable", title, status = 409, code = "integration_unavailable",
        capability = s.Capability, state = s.State, label = s.LabelAr, body, manualAlternative = s.ManualAlternative, lastAttemptAt = s.LastAttemptAt, facts,
    }, statusCode: StatusCodes.Status409Conflict, contentType: "application/problem+json");

    /// <summary>X01: licensed signing redirect, or a clear integration_unavailable state. Never creates a signature or consent record.</summary>
    private static async Task<IResult> StartSigning(RahoonDbContext db, RequestContext rc, IClock clock, ConditionalIntegrations integrations, ILicensedSigningProvider signing)
    {
        var orgId = rc.OrganizationId!.Value;
        var state = await integrations.ResolveAsync(orgId, IntegrationKeys.LicensedSigning, forOwner: true);
        var agreement = await db.Agreements.AsNoTracking().Where(a => a.CaseId == rc.OwnerCaseId).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
        if (!state.LicensedPathAvailable)
        {
            db.Set<IntegrationAttempt>().Add(new IntegrationAttempt { InstitutionOrganizationId = orgId, CaseId = rc.OwnerCaseId, Capability = state.Capability, State = state.State, SubjectReference = agreement?.Number, At = clock.UtcNow });
            await db.SaveChangesAsync();
            var offer = agreement?.OfferId is { } oid ? await db.Offers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == oid) : null;
            return Unavailable(state with { LastAttemptAt = clock.UtcNow }, "خدمة التوقيع غير متاحة الآن",
                "لم يتغير شيء في عرضك. يمكنك الموافقة داخل المنصة برمز تحقق، أو المحاولة لاحقاً.",
                new { offerValidUntil = offer?.ValidUntil, lastAttemptAt = clock.UtcNow });
        }
        if (agreement is null) throw new NotFoundException();
        var redirect = await signing.StartSigningAsync(agreement.Id);
        return Results.Ok(new { state = IntegrationState.Pending, redirectUrl = redirect, provider = state.ProviderLabel, manualAlternative = state.ManualAlternative });
    }

    /// <summary>X02: licensed payment link, or a clear integration_unavailable state. Never records a payment or marks an installment.</summary>
    private static async Task<IResult> StartPayment(int no, RahoonDbContext db, RequestContext rc, IClock clock, ConditionalIntegrations integrations, ILicensedPaymentProvider payment)
    {
        var orgId = rc.OrganizationId!.Value;
        var state = await integrations.ResolveAsync(orgId, IntegrationKeys.LicensedPayment, forOwner: true);
        var installment = await db.Installments.AsNoTracking().FirstOrDefaultAsync(i => i.CaseId == rc.OwnerCaseId && i.No == no);
        if (!state.LicensedPathAvailable)
        {
            db.Set<IntegrationAttempt>().Add(new IntegrationAttempt { InstitutionOrganizationId = orgId, CaseId = rc.OwnerCaseId, Capability = state.Capability, State = state.State, SubjectReference = $"installment:{no}", At = clock.UtcNow });
            await db.SaveChangesAsync();
            return Unavailable(state with { LastAttemptAt = clock.UtcNow }, "الدفع الإلكتروني غير متاح الآن",
                "لم يُخصم أي مبلغ. يمكنك السداد بالتحويل البنكي إلى حساب الجهة الممولة ثم إرسال إشعار التحويل.",
                installment is null ? null : new { amount = installment.Amount, dueDate = installment.DueDate });
        }
        if (installment is null || installment.Status is InstallmentStatus.Matched or InstallmentStatus.Waived) throw new NotFoundException();
        var link = await payment.CreatePaymentLinkAsync(installment.Id);
        return Results.Ok(new { state = IntegrationState.Pending, redirectUrl = link, amount = installment.Amount, provider = state.ProviderLabel, manualAlternative = state.ManualAlternative });
    }
}
