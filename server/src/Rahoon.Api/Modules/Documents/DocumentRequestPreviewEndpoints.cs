using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Documents;

public sealed record DocumentRequestPreviewRequest(string? DocumentTypeKey, DateOnly? DueOn, string? Uploader, List<string>? Channels);

public sealed record RenderedTemplate(string Text, string Code, string Title, int VersionNo);

/// <summary>Owner-facing document request text from the published template TPL-DOCREQ-01 (L10 request drawer).</summary>
public static class DocumentRequestTemplates
{
    public const string Code = "TPL-DOCREQ-01";

    public static async Task<RenderedTemplate> RenderAsync(RahoonDbContext db, Guid orgId, string documentName, DateOnly dueOn)
    {
        // Templates are not tenant-filtered: prefer the institution's published copy, then the platform base template.
        var t = await db.Templates.AsNoTracking()
            .Where(x => x.Code == Code && x.Status == TemplateStatus.Published && (x.OrganizationId == null || x.OrganizationId == orgId))
            .OrderByDescending(x => x.OrganizationId != null).ThenByDescending(x => x.VersionNo).FirstOrDefaultAsync();
        var body = t?.BodyAr ?? "نحتاج منك {المستند} لمتابعة حالتك. يمكنك رفعه من صفحة المستندات حتى {المهلة}، وإن واجهت صعوبة فاكتب لنا.";
        var text = body.Replace("{المستند}", documentName).Replace("{المهلة}", dueOn.ToString("yyyy-MM-dd"));
        return new RenderedTemplate(text, t?.Code ?? Code, t?.Title ?? "طلب مستند", t?.VersionNo ?? 0);
    }
}

public static class DocumentRequestPreviewEndpoints
{
    private static readonly Dictionary<string, string> ChannelLabels = new() { ["portal"] = "داخل البوابة", ["sms"] = "إشعار نصي" };

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/cases/{reference}/documents/requests/preview", Preview).RequirePermission(P.CaseView, P.DocumentRequest);

    /// <summary>Renders what the owner will read, with the institution rule for the type — nothing is saved or sent.</summary>
    private static async Task<IResult> Preview(string reference, DocumentRequestPreviewRequest req, CaseAccess access, RahoonDbContext db, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var uploader = (req.Uploader ?? "owner").ToLowerInvariant();
        new Validator()
            .Require(!string.IsNullOrWhiteSpace(req.DocumentTypeKey), "documentTypeKey", "اختر نوع المستند.")
            .Require(req.DueOn is { } d && d > today, "dueOn", "المهلة يجب أن تكون في المستقبل.")
            .Require(uploader is "owner" or "internal", "uploader", "اختر من يرفع المستند.")
            .Require(req.Channels is null || req.Channels.All(ChannelLabels.ContainsKey), "channels", "قناة غير معروفة.")
            .ThrowIfInvalid();
        var c = await access.GetAsync(reference, track: false);
        var type = await db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Key == req.DocumentTypeKey)
                   ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["documentTypeKey"] = ["نوع المستند غير معروف."] });
        var rule = await db.DocumentRules.AsNoTracking().FirstOrDefaultAsync(r => r.OrganizationId == c.OrganizationId && r.DocumentTypeKey == type.Key);
        var due = req.DueOn!.Value;
        var channels = req.Channels is { Count: > 0 } ch ? ch : ["portal", "sms"];
        var rendered = uploader == "owner" ? await DocumentRequestTemplates.RenderAsync(db, c.OrganizationId, type.NameAr, due) : null;
        var validity = rule?.ValidityDays ?? type.ValidityDays;
        return Results.Ok(new
        {
            ownerMessage = rendered?.Text,
            template = rendered is null ? null : new { rendered.Code, rendered.Title, rendered.VersionNo },
            caption = rendered is null ? "طلب داخلي — لا يُرسل شيء للمالك." : $"من قالب «{rendered.Title}» · يُرسل " + string.Join(" + ", channels.Select(x => ChannelLabels[x])),
            channels,
            dueOn = due, dueOnHijri = Hijri.Format(due), daysFromToday = due.DayNumber - today.DayNumber,
            dueLabel = $"{Hijri.Format(due)} · {CaseDisplay.Days(due.DayNumber - today.DayNumber)}",
            rule = new
            {
                formats = type.AllowedFormats, validityDays = validity, visibleTo = rule?.VisibleTo ?? ["case_team"], uploader = rule?.Uploader,
                helper = $"قاعدة المنشأة: {string.Join("/", type.AllowedFormats.Select(f => f.ToUpperInvariant()))}"
                         + (validity is { } vd ? $" · صلاحية {vd} يوماً" : "")
                         + ((rule?.VisibleTo ?? ["case_team"]).SequenceEqual(["case_team"]) ? " · يراه فريق الحالة فقط" : ""),
            },
            saved = false,
        });
    }
}
