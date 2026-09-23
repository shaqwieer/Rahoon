using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Administration;

public sealed record IntegrationUpdateRequest(string State, string Note);

/// <summary>
/// Which adapters are actually wired. «مفعّل» requires a live adapter; «محاكاة» requires a sandbox adapter.
/// Everything else can only be pending / unavailable / failed until a contract and credentials exist.
/// </summary>
public static class IntegrationAdapters
{
    public static bool LiveAdapterConfigured(IServiceProvider sp, string key) => key switch
    {
        IntegrationKeys.Sms => sp.GetService<ISmsGateway>() is { } g && g is not SandboxSmsGateway,
        IntegrationKeys.NationalIdentity => sp.GetService<INationalIdentityProvider>() is { } p && p is not UnavailableIdentityProvider,
        IntegrationKeys.LicensedSigning => sp.GetService<ILicensedSigningProvider>() is { } s && s is not UnavailableSigningProvider,
        IntegrationKeys.LicensedPayment => sp.GetService<ILicensedPaymentProvider>() is { } pay && pay is not UnavailablePaymentProvider,
        IntegrationKeys.JudicialChannel => sp.GetService<IJudicialChannel>() is { } j && j is not UnavailableJudicialChannel,
        _ => false, // email, core banking, real-estate registry: no adapter exists in this build
    };

    public static bool SandboxAdapterConfigured(IServiceProvider sp, string key) => key switch
    {
        IntegrationKeys.Sms => sp.GetService<ISmsGateway>() is SandboxSmsGateway,
        IntegrationKeys.Email => true, // messages are recorded as simulated outbound rows only
        _ => false,
    };

    public static string Purpose(string key) => key switch
    {
        IntegrationKeys.JudicialChannel => "تصدير الحزم + الحالة الرسمية + طلب فك الرهن",
        IntegrationKeys.CoreBanking => "استيراد المديونية",
        IntegrationKeys.LicensedSigning => "اتفاقيات المالك",
        IntegrationKeys.LicensedPayment => "أقساط المالك",
        IntegrationKeys.NationalIdentity => "تحقق المالك",
        IntegrationKeys.Sms => "الإشعارات ورموز التحقق",
        IntegrationKeys.Email => "الإشعارات",
        IntegrationKeys.RealEstateRegistry => "مطابقة الصك",
        _ => "—",
    };

    public static string StateLabel(string state) => state switch
    {
        IntegrationState.Enabled => "مفعّل",
        IntegrationState.Simulated => "محاكاة",
        IntegrationState.Pending => "قيد الانتظار",
        IntegrationState.Failed => "فشل",
        _ => "غير متاح",
    };
}

/// <summary>PA18 integrations & states (platform) and the per-institution read-only view.</summary>
public static class IntegrationEndpoints
{
    private static readonly string[] States = [IntegrationState.Enabled, IntegrationState.Simulated, IntegrationState.Pending, IntegrationState.Unavailable, IntegrationState.Failed];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/platform/integrations").RequireOrg(OrganizationKind.Platform);
        g.MapGet("", PlatformList).RequireAnyPermission(P.PlatformOps, P.PlatformIntegrations);
        g.MapPut("/{key}", Update).RequirePermission(P.PlatformIntegrations).Idempotent();
        app.MapGet("/api/settings/integrations", InstitutionList).RequireOrg(OrganizationKind.Lender).RequireAnyPermission(P.OrgSettings, P.CaseView);
    }

    private static object Legend() => States.Select(s => new
    {
        key = s, label = IntegrationAdapters.StateLabel(s),
        description = s switch
        {
            IntegrationState.Enabled => "مزامنة حية من قناة معتمدة",
            IntegrationState.Simulated => "بيانات اختبار؛ لا أثر",
            IntegrationState.Pending => "أُرسل ولم يصل رد",
            IntegrationState.Failed => "رفض أو خطأ؛ استثناء مفتوح",
            _ => "القناة متوقفة؛ يدوي",
        },
    });

    private static async Task<IResult> PlatformList(RahoonDbContext db, HttpContext http)
    {
        var sp = http.RequestServices;
        var rows = await db.IntegrationSettings.AsNoTracking().OrderBy(i => i.NameAr).ToListAsync();
        var users = await db.Users.AsNoTracking().Where(u => rows.Select(r => r.UpdatedByUserId).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return Results.Ok(new
        {
            items = rows.Select(i => new
            {
                i.Key, name = i.NameAr, purpose = IntegrationAdapters.Purpose(i.Key), state = i.State, stateLabel = IntegrationAdapters.StateLabel(i.State), i.Note,
                lastChange = new { at = i.UpdatedAt, by = i.UpdatedByUserId is { } u ? users.GetValueOrDefault(u) : "الإعداد الأولي" },
                liveAdapter = IntegrationAdapters.LiveAdapterConfigured(sp, i.Key), sandboxAdapter = IntegrationAdapters.SandboxAdapterConfigured(sp, i.Key),
                lastSuccessAt = (DateTimeOffset?)null, failureRate7d = (decimal?)null,
                metricsNote = "لا توجد مزامنة حية؛ لا تُعرض مؤشرات نجاح أو فشل مختلقة.",
            }),
            legend = Legend(),
            footnote = "«محاكاة» تُستخدم للاختبار فقط ولا تنتج أي أثر قانوني أو مالي، وتوسم بها كل السجلات المرتبطة.",
        });
    }

    private static async Task<IResult> InstitutionList(RahoonDbContext db)
    {
        var rows = await db.IntegrationSettings.AsNoTracking().OrderBy(i => i.NameAr).ToListAsync();
        return Results.Ok(new
        {
            items = rows.Select(i => new
            {
                i.Key, name = i.NameAr, purpose = IntegrationAdapters.Purpose(i.Key), state = i.State, stateLabel = IntegrationAdapters.StateLabel(i.State), i.Note,
                lastChange = i.UpdatedAt, manualPath = i.State != IntegrationState.Enabled,
            }),
            legend = Legend(),
        });
    }

    private static async Task<IResult> Update(string key, IntegrationUpdateRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, HttpContext http)
    {
        new Validator().Require(States.Contains(req.State), "state", "الحالة: مفعّل أو محاكاة أو قيد الانتظار أو غير متاح أو فشل.")
            .Require(!string.IsNullOrWhiteSpace(req.Note) && req.Note.Trim().Length is >= 5 and <= 500, "note", "اكتب ملاحظة توضح الوضع (5 أحرف على الأقل).").ThrowIfInvalid();
        var sp = http.RequestServices;
        await using var tx = await db.Database.BeginTransactionAsync();
        var i = await db.IntegrationSettings.FirstOrDefaultAsync(x => x.Key == key) ?? throw new NotFoundException();
        if (req.State == IntegrationState.Enabled && !IntegrationAdapters.LiveAdapterConfigured(sp, key))
        {
            await audit.RecordBlockedAsync(new AuditEntry("integration.state_blocked", $"محاولة تفعيل «{i.NameAr}» دون موصل", Detail: "المانع: لا يوجد موصل حي مهيأ لهذا التكامل.", Blocked: true));
            throw new DomainException("adapter_not_configured",
                $"لا يمكن تفعيل «{i.NameAr}»: لا يوجد موصل حي مهيأ لهذا التكامل (عقد واعتمادات). اختر «قيد الانتظار» أو «غير متاح» حتى يُركَّب الموصل.", StatusCodes.Status422UnprocessableEntity);
        }
        if (req.State == IntegrationState.Simulated && !IntegrationAdapters.SandboxAdapterConfigured(sp, key))
            throw new DomainException("sandbox_not_available", $"لا توجد بيئة محاكاة لـ«{i.NameAr}»؛ المحاكاة متاحة فقط حيث يوجد موصل تجريبي.", StatusCodes.Status422UnprocessableEntity);
        var from = i.State;
        i.State = req.State;
        i.Note = req.Note.Trim();
        i.UpdatedAt = clock.UtcNow;
        i.UpdatedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("integration.state_changed", $"تغيير حالة التكامل «{i.NameAr}»", FromState: from, ToState: req.State, Reason: i.Note,
            Detail: $"{IntegrationAdapters.StateLabel(from)} ← {IntegrationAdapters.StateLabel(req.State)}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { i.Key, state = i.State, stateLabel = IntegrationAdapters.StateLabel(i.State), i.Note, lastChange = i.UpdatedAt });
    }
}
