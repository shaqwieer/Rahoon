using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Referral;

public sealed record ReadinessItem(string Key, string Label, bool Met, string Detail, string? EvidenceLink, string? EvidenceDate);

public sealed record FieldMapping(string Field, string PlatformValue, string ExpectedValue, string Result, bool Blocking);

/// <summary>
/// L25/J01 readiness (computed on the server from case data), blockers for the referral decision,
/// and the J02 evidence pack. Nothing here changes the case state.
/// </summary>
public sealed class ReferralService(RahoonDbContext db, IClock clock)
{
    public static readonly string[] ItemKeys =
    [
        "amicable_exhausted", "voluntary_sale_offered", "no_open_complaints", "core_docs_valid",
        "lien_review", "debt_reconciled", "owner_notified", "objection_period_elapsed",
    ];

    /// <summary>Items whose evidence legal records manually when the platform has no record of it.</summary>
    public static readonly string[] AttestableKeys = ["voluntary_sale_offered", "amicable_exhausted"];

    public Task<JudicialReferral?> CurrentAsync(Guid caseId, bool track = true)
    {
        var q = db.Referrals.Where(r => r.CaseId == caseId && r.Status != ReferralStatus.Rejected && r.Status != ReferralStatus.Withdrawn);
        if (!track) q = q.AsNoTracking();
        return q.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
    }

    public async Task<List<ReadinessItem>> ReadinessAsync(Case c, JudicialReferral? r)
    {
        var today = clock.TodayRiyadh;
        var items = new List<ReadinessItem>();
        var evidence = await db.Set<ReferralChecklistEvidence>().AsNoTracking().Where(e => e.CaseId == c.Id).OrderByDescending(e => e.RecordedAt).ToListAsync();
        string Link(string tail) => $"/cases/{c.Reference}/{tail}";

        // 1. Amicable solutions exhausted.
        var offers = await db.Offers.AsNoTracking().Where(o => o.CaseId == c.Id).OrderBy(o => o.SentAt).ToListAsync();
        var live = offers.Any(o => o.Status is OfferStatus.Sent or OfferStatus.Countered && o.ValidUntil >= today);
        var activeAgreement = await db.Agreements.AnyAsync(a => a.CaseId == c.Id && (a.Status == AgreementStatus.Active || a.Status == AgreementStatus.PendingActivation));
        var amicableEvidence = evidence.FirstOrDefault(e => e.Key == "amicable_exhausted");
        if (offers.Count > 0)
        {
            var declined = offers.Count(o => o.Status == OfferStatus.Declined);
            var lapsed = offers.Count(o => o.Status == OfferStatus.Expired || (o.Status is OfferStatus.Sent or OfferStatus.Countered && o.ValidUntil < today));
            var detail = $"{offers.Count} {(offers.Count == 1 ? "عرض" : offers.Count == 2 ? "عرضان" : "عروض")} بين {offers[0].SentAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM} و{offers[^1].SentAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM}" +
                         $" · رُفض {declined}، ولم يُرد على {lapsed}";
            if (live) detail = "يوجد عرض قائم للمالك لم تنتهِ صلاحيته.";
            else if (activeAgreement) detail = "يوجد اتفاق نشط أو بانتظار التفعيل.";
            items.Add(new("amicable_exhausted", "استنفاد الحلول الودية", !live && !activeAgreement, detail, Link("negotiation"), offers[^1].SentAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd")));
        }
        else if (amicableEvidence is not null && !activeAgreement)
            items.Add(new("amicable_exhausted", "استنفاد الحلول الودية", true, amicableEvidence.Note, Link("referral"), amicableEvidence.RecordedAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd")));
        else
            items.Add(new("amicable_exhausted", "استنفاد الحلول الودية", false, "لم يُعرض على المالك أي حل ودي بعد.", Link("solutions"), null));

        // 2. Voluntary sale offered to the owner.
        var saleOffered = await db.Solutions.AnyAsync(s => s.CaseId == c.Id && s.Kind == SolutionKind.VoluntarySale && db.Offers.Any(o => o.SolutionVersionId == s.Id))
                          || await db.AuditEvents.AnyAsync(e => e.CaseId == c.Id && e.Type == "case.transition" && e.ToState == "voluntary_sale" && !e.Blocked);
        var saleEvidence = evidence.FirstOrDefault(e => e.Key == "voluntary_sale_offered");
        items.Add(saleOffered
            ? new("voluntary_sale_offered", "عرض البيع الطوعي على المالك", true, "عُرض مسار البيع الطوعي على المالك داخل المنصة.", Link("negotiation"), null)
            : saleEvidence is not null
                ? new("voluntary_sale_offered", "عرض البيع الطوعي على المالك", true, saleEvidence.Note, saleEvidence.DocumentVersionId is null ? Link("referral") : Link("documents"), saleEvidence.RecordedAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"))
                : new("voluntary_sale_offered", "عرض البيع الطوعي على المالك", false, "لا يوجد ما يثبت عرض البيع الطوعي على المالك.", Link("referral"), null));

        // 3. No open complaints or objections.
        var complaints = await db.Complaints.AsNoTracking().Where(x => x.CaseId == c.Id).ToListAsync();
        var open = complaints.Where(x => x.Status != ComplaintStatus.Resolved && x.Status != ComplaintStatus.Closed).ToList();
        var lastClosed = complaints.Where(x => x.RespondedAt != null).OrderByDescending(x => x.RespondedAt).FirstOrDefault();
        items.Add(new("no_open_complaints", "لا شكاوى أو اعتراضات مفتوحة", open.Count == 0,
            open.Count > 0 ? $"مفتوح: {string.Join("، ", open.Select(x => x.Reference))}" : lastClosed is null ? "لا توجد شكاوى على الحالة" : $"آخر شكوى أُغلقت {lastClosed.RespondedAt!.Value.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
            "/complaints", null));

        // 4. Core documents valid (contract, deed/mortgage, current debt statement).
        string[] core = ["financing_contract", "title_deed"];
        var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && core.Contains(d.DocumentTypeKey)).ToListAsync();
        var snapshot = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync();
        var docProblems = new List<string>();
        foreach (var key in core)
        {
            var d = docs.FirstOrDefault(x => x.DocumentTypeKey == key);
            var name = key == "title_deed" ? "الصك" : "العقد";
            if (d is null || d.Status != DocumentStatus.Verified) docProblems.Add($"{name} غير متحقق منه");
            else if (d.ValidUntil is { } v && v < today) docProblems.Add($"{name} منتهي الصلاحية");
        }
        if (snapshot is null) docProblems.Add("لا يوجد كشف مديونية حالي");
        items.Add(new("core_docs_valid", "المستندات الأساسية صالحة", docProblems.Count == 0,
            docProblems.Count == 0 ? $"العقد، الصك، الرهن، كشف المديونية ({snapshot!.AsOf.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd})" : string.Join("، ", docProblems),
            Link("documents"), null));

        // 5. Lien and encumbrance review by legal.
        var mortgage = await db.Mortgages.AsNoTracking().FirstOrDefaultAsync(m => m.CaseId == c.Id);
        var reviewer = mortgage?.LegalReviewedByUserId is { } rid ? await db.Users.Where(u => u.Id == rid).Select(u => u.FullName).FirstOrDefaultAsync() : null;
        items.Add(mortgage is { LegalReviewStatus: LegalReviewStatus.Complete }
            ? new("lien_review", "مراجعة الرهن والقيود", true, $"{reviewer ?? "القانونية"} · {mortgage.LegalReviewedAt?.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}", Link("property"), mortgage.LegalReviewedAt?.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"))
            : new("lien_review", "مراجعة الرهن والقيود", false, "مراجعة القانونية للرهن لم تكتمل.", Link("property"), null));

        // 6. Debt reconciled with the financing system (current snapshot = case outstanding).
        var diff = snapshot is null || c.OutstandingAmount is null ? (decimal?)null : c.OutstandingAmount.Value - snapshot.Total;
        items.Add(new("debt_reconciled", "مطابقة المديونية مع نظام التمويل", diff == 0m,
            diff is null ? "لا توجد مديونية مسجلة للمطابقة." : $"الفرق {diff:N2} ر.س · {snapshot!.Source} {snapshot.AsOf.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}",
            Link("finance"), snapshot?.AsOf.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd")));

        // 7. Prior notice to the owner with their rights.
        var days = r?.ObjectionPeriodDays ?? await Analytics.OperationalSettings.DaysAsync(db, c.OrganizationId, Analytics.OperationalSettings.ObjectionDays, 15);
        items.Add(r?.NoticeSentAt is { } sent
            ? new("owner_notified", "إشعار المالك المسبق بالقرار وحقوقه", true, $"أُرسل {sent.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd} · رسالة نصية (تجريبية) + البوابة", Link("comms"), sent.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"))
            : new("owner_notified", "إشعار المالك المسبق بالقرار وحقوقه", false, "لم يُرسل · قالب «إشعار قبل الإحالة» جاهز", null, null));

        // 8. Objection period elapsed (no open objection is covered by item 3).
        if (r?.ObjectionEndsOn is { } ends)
        {
            var elapsed = today > ends;
            items.Add(new("objection_period_elapsed", "انقضاء مهلة الاعتراض", elapsed,
                elapsed ? $"انقضت {ends:yyyy-MM-dd} ({CaseDisplay.Days(r.ObjectionPeriodDays)})" : $"تنتهي {ends:yyyy-MM-dd} · متبقٍ {CaseDisplay.Days(ends.DayNumber - today.DayNumber + 1)}",
                null, ends.ToString("yyyy-MM-dd")));
        }
        else
            items.Add(new("objection_period_elapsed", "انقضاء مهلة الاعتراض", false, $"تبدأ بعد الإشعار · {CaseDisplay.Days(days)} (افتراض)", null, null));

        return items;
    }

    /// <summary>Everything that blocks the referral decision: unmet readiness items plus a live offer.</summary>
    public async Task<List<string>> BlockersAsync(Case c, JudicialReferral? r)
    {
        var blockers = (await ReadinessAsync(c, r)).Where(i => !i.Met).Select(i => $"{i.Label}: {i.Detail}").ToList();
        var today = clock.TodayRiyadh;
        if (await db.Offers.AnyAsync(o => o.CaseId == c.Id && (o.Status == OfferStatus.Sent || o.Status == OfferStatus.Countered) && o.ValidUntil >= today))
            blockers.Add("يوجد عرض قائم للمالك لم تنتهِ صلاحيته.");
        return blockers.Distinct().ToList();
    }

    /// <summary>
    /// Builds the numbered J02 pack: each item hashed (document versions use their stored SHA-256; generated
    /// records hash their canonical JSON), then the manifest hash over the items.
    /// </summary>
    public async Task<(List<EvidencePackItem> Items, List<FieldMapping> Mappings, string ManifestHash)> BuildPackAsync(Case c, JudicialReferral r, Guid packId)
    {
        var items = new List<EvidencePackItem>();
        var seq = 0;
        void Add(string title, string sourceType, string sourceRef, string version, string sha, bool verified) =>
            items.Add(new EvidencePackItem
            {
                OrganizationId = c.OrganizationId, PackId = packId, CaseId = c.Id, Seq = ++seq, Title = title, SourceType = sourceType,
                SourceRef = sourceRef, VersionLabel = version, Sha256 = sha, Verified = verified,
            });

        var transitions = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id && e.Type == "case.transition" && !e.Blocked).OrderBy(e => e.Seq)
            .Select(e => new { e.FromState, e.ToState, e.OccurredAt, e.Reason }).ToListAsync();
        var summary = new { c.Reference, status = CaseStatusInfo.Key(c.Status), c.OpenedOn, c.OutstandingAmount, c.ArrearsAmount, transitions };
        Add("ملخص الحالة ومسارها الزمني", "generated", $"case:{c.Reference}", "مولَّد", Hash(summary), true);

        foreach (var (key, title) in new[] { ("financing_contract", "عقد التمويل وملاحقه"), ("title_deed", "صك الملكية وشهادة الرهن") })
        {
            var doc = await db.Documents.AsNoTracking().Include(d => d.Versions).FirstOrDefaultAsync(d => d.CaseId == c.Id && d.DocumentTypeKey == key);
            var v = doc?.Versions.OrderByDescending(x => x.VersionNo).FirstOrDefault();
            Add(title, "document", v is null ? $"document:{key}" : $"document_version:{v.Id}", v is null ? "—" : $"v{v.VersionNo}",
                v is null ? "—" : $"sha256:{v.Sha256}", doc?.Status == DocumentStatus.Verified && v is not null);
        }

        var snapshot = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync();
        Add($"كشف المديونية المعتمد {snapshot?.AsOf.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}", "record", snapshot is null ? "debt_snapshot:—" : $"debt_snapshot:{snapshot.Id}",
            snapshot?.AsOf.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd") ?? "—",
            snapshot is null ? "—" : Hash(new { snapshot.Principal, snapshot.Profit, snapshot.LateFees, snapshot.OtherFees, snapshot.Total, snapshot.Source, snapshot.AsOf }),
            snapshot is not null && snapshot.Total == c.OutstandingAmount);

        var offers = await db.Offers.AsNoTracking().Where(o => o.CaseId == c.Id).OrderBy(o => o.SentAt)
            .Select(o => new { o.VersionNo, o.SentAt, o.ValidUntil, status = o.Status.ToString(), o.RespondedAt, o.DeclineReason }).ToListAsync();
        Add("سجل العروض والردود", "generated", $"offers:{c.Reference}", $"{offers.Count} عروض", Hash(offers), true);

        var notice = new { r.NoticeSentAt, r.ObjectionEndsOn, r.ObjectionPeriodDays, r.NoticeTemplateCode };
        Add("إثبات الإشعار المسبق", "record", $"referral_notice:{r.Id}", r.NoticeSentAt?.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd") ?? "—", Hash(notice), r.NoticeSentAt is not null);

        var messages = await db.Messages.AsNoTracking().Where(m => m.CaseId == c.Id && !m.InternalOnly).OrderBy(m => m.At)
            .Select(m => new { m.At, channel = m.Channel.ToString(), m.AuthorType, m.Body }).ToListAsync();
        Add("سجل التواصل والإشعارات", "generated", $"messages:{c.Reference}", $"{messages.Count} رسالة", Hash(messages), true);

        var mappings = await MappingsAsync(c, r, snapshot);
        var manifestHash = Hash(items.Select(i => new { i.Seq, i.Title, i.SourceType, i.SourceRef, i.VersionLabel, i.Sha256, i.Verified }));
        return (items, mappings, manifestHash);
    }

    /// <summary>
    /// J02 field reconciliation. With no approved channel schema, each field is checked against its own evidence
    /// source inside the platform (the channel's expected values replace the right column once integrated).
    /// </summary>
    private async Task<List<FieldMapping>> MappingsAsync(Case c, JudicialReferral r, DebtSnapshot? snapshot)
    {
        var list = new List<FieldMapping>();
        var contract = await db.FinancingContracts.AsNoTracking().FirstOrDefaultAsync(f => f.CaseId == c.Id);
        var contractDoc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.CaseId == c.Id && d.DocumentTypeKey == "financing_contract");
        var masked = contract is null ? "—" : Mask.Reference(contract.ContractNumber);
        list.Add(contractDoc?.Status == DocumentStatus.Verified && contract is not null
            ? new("رقم العقد", masked, masked, "match", false)
            : new("رقم العقد", masked, "مستند العقد غير متحقق منه", "mismatch", true));

        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        var idMasked = party?.NationalIdMasked ?? "—";
        list.Add(party?.IdentityVerifiedAt is not null
            ? new("هوية المالك", idMasked, idMasked, "match", false)
            : new("هوية المالك", idMasked, "لم يُتحقق من هوية المالك", "mismatch", true));

        var outstanding = c.OutstandingAmount?.ToString("N2") ?? "—";
        var total = snapshot?.Total.ToString("N2") ?? "—";
        list.Add(new("المديونية", outstanding, total, outstanding == total ? "match" : "mismatch", outstanding != total));

        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id);
        var mortgage = await db.Mortgages.AsNoTracking().FirstOrDefaultAsync(m => m.CaseId == c.Id);
        var deed = property?.DeedNumberMasked ?? "—";
        list.Add(mortgage?.DeedMatched == true
            ? new("رقم الصك", deed, deed, "match", false)
            : new("رقم الصك", deed, "لم تُطابق القانونية الصك مع الرهن", "mismatch", true));

        if (r.NoticeSentAt is { } sent)
        {
            var d = DateOnly.FromDateTime(sent.ToOffset(TimeSpan.FromHours(3)).DateTime);
            list.Add(new("تاريخ الإشعار", d.ToString("yyyy-MM-dd"), Hijri.Format(d), "auto_converted", false));
        }
        else list.Add(new("تاريخ الإشعار", "—", "—", "mismatch", true));

        var city = property?.City ?? "—";
        list.Add(new("المدينة", c.City ?? "—", city, (c.City ?? "—") == city ? "match" : "mismatch", (c.City ?? "—") != city));
        return list;
    }

    public static string Hash(object value) =>
        "sha256:" + Tokens.Sha256Hex(JsonSerializer.Serialize(value, Infrastructure.Http.JsonOptions.Web));

    /// <summary>EXT-JD-2026-0038841 → EXT-JD-2026-•••8841.</summary>
    public static string MaskExternal(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "—";
        var dash = value.LastIndexOf('-');
        var tail = dash >= 0 ? value[(dash + 1)..] : value;
        var head = dash >= 0 ? value[..(dash + 1)] : "";
        return tail.Length <= 4 ? value : head + "•••" + tail[^4..];
    }

    public static string StatusLabel(ReferralStatus s) => s switch
    {
        ReferralStatus.Preparing => "قيد الإعداد",
        ReferralStatus.NoticeSent => "أُرسل الإشعار المسبق",
        ReferralStatus.PendingApproval => "بانتظار اعتماد القرار",
        ReferralStatus.Approved => "القرار معتمد",
        ReferralStatus.Rejected => "رُفض القرار",
        ReferralStatus.HandedOff => "سُلّم للجهة المختصة",
        _ => "مسحوبة",
    };

    public AuditEntry Blocked(Case c, string type, string title, IReadOnlyList<string> reasons, string? reason = null) =>
        new(type, title, c.Id, c.Reference, Reason: reason, Detail: "المانع: " + string.Join(" · ", reasons), Blocked: true, OrganizationId: c.OrganizationId);
}
