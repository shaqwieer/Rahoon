using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Owner;

public sealed record CounterRequest(int? InstallmentDay, string? FirstMonth, decimal? InstallmentAmount, string? Reason);
public sealed record ConsentRequest(List<string> Acknowledgements, string Code);
public sealed record DeclineRequest(string? Reason);
public sealed record OwnerMessageRequest(string Body);
public sealed record HardshipBody(string? ReasonKey);
public sealed record PaymentNoticeRequest(DateOnly TransferDate, decimal Amount, string? Reference);
public sealed record OwnerComplaintRequest(string Type, string Body, string? Subject);
public sealed record RescheduleRequest(string? Note);

/// <summary>
/// Owner (debtor/property owner) portal API (B6). Every query is pinned to the single case
/// in the owner's session; internal notes, other parties and lender-only documents are never returned.
/// </summary>
public static class OwnerEndpoints
{
    public static readonly string[] Acks = ["terms_read", "voluntary"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/owner").RequireOwner();
        g.MapGet("/home", Home);
        g.MapGet("/journey", Journey);
        g.MapGet("/documents", Documents);
        g.MapPost("/documents/upload", Upload).DisableAntiforgery();
        g.MapPost("/documents/{documentId:guid}/help", DocumentHelp).Idempotent();
        g.MapGet("/debt", Debt);
        g.MapGet("/options", Options);
        g.MapPost("/options/{key}/inquiry", Inquiry).Idempotent();
        g.MapGet("/offers/{id:guid}", OfferDetail);
        g.MapPost("/offers/{id:guid}/decline", Decline).Idempotent();
        g.MapPost("/offers/{id:guid}/counter", Counter).Idempotent();
        g.MapPost("/offers/{id:guid}/consent/otp", ConsentOtp);
        g.MapPost("/offers/{id:guid}/consent", Consent).Idempotent();
        g.MapGet("/agreement", AgreementView);
        g.MapGet("/payments", Payments);
        g.MapPost("/payments/{no:int}/notice", Notice).Idempotent();
        g.MapPost("/hardship", Hardship).Idempotent();
        g.MapGet("/messages", Messages);
        g.MapPost("/messages", SendMessage).Idempotent();
        g.MapPost("/appointments/{id:guid}/confirm", ConfirmAppointment).Idempotent();
        g.MapPost("/appointments/{id:guid}/reschedule", RescheduleAppointment).Idempotent();
        g.MapGet("/complaints", Complaints);
        g.MapPost("/complaints", SubmitComplaint).Idempotent();
        g.MapGet("/closure", ClosureView);
        g.MapGet("/files/{versionId:guid}", DownloadOwnFile);
    }

    private static async Task<Case> OwnCaseAsync(RahoonDbContext db, RequestContext rc, bool track = false)
    {
        var q = db.Cases.Where(c => c.Id == rc.OwnerCaseId);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync() ?? throw new ForbiddenException();
    }

    private static async Task<(string Name, string Initials, string Role)?> ManagerAsync(RahoonDbContext db, Case c)
    {
        if (c.AssignedManagerId is null) return null;
        var m = await db.Memberships.Where(x => x.Id == c.AssignedManagerId).Select(x => new { x.User!.FullName, x.Title }).FirstOrDefaultAsync();
        return m is null ? null : (m.FullName.Split(' ')[0], AuthEndpoints.Initials(m.FullName), "مسؤولة حالتك");
    }

    /// <summary>5 plain-language owner stages (D03). Referral is never presented as an expected stage.</summary>
    private static async Task<int> OwnerStageAsync(RahoonDbContext db, Case c)
    {
        var s = c.Status == CaseStatus.Paused ? c.StatusBeforePause ?? CaseStatus.AwaitingData : c.Status;
        if (s is CaseStatus.Draft or CaseStatus.AwaitingData) return 1;
        if (s is CaseStatus.Verification or CaseStatus.Valuation) return 2;
        if (s is CaseStatus.ActiveSettlement or CaseStatus.AwaitingReconciliation or CaseStatus.Closed) return 5;
        if (await db.Agreements.AnyAsync(a => a.CaseId == c.Id && a.Status == AgreementStatus.PendingActivation)) return 4;
        return 3;
    }

    private static readonly string[] StageTitles = ["التعرف على حالتك", "التحقق والتقييم", "اختيار الحل المناسب", "الاتفاق", "السداد والإغلاق"];

    private static async Task<IResult> Home(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await OwnCaseAsync(db, rc);
        var today = clock.TodayRiyadh;
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        var stage = await OwnerStageAsync(db, c);
        var offer = await db.Offers.AsNoTracking().Where(o => o.CaseId == c.Id && o.Status == OfferStatus.Sent && o.ValidUntil >= today).OrderByDescending(o => o.SentAt).FirstOrDefaultAsync();
        var docsNeeded = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && d.VisibleToOwner && (d.Status == DocumentStatus.Requested || d.Status == DocumentStatus.Rejected)).ToListAsync();
        var agreement = await db.Agreements.AsNoTracking().Where(a => a.CaseId == c.Id).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync();
        object? next = null;
        if (c.Status == CaseStatus.Closed)
            next = new { type = "closed", title = "أُغلقت حالتك", body = "مستندات الإغلاق جاهزة للتنزيل.", deadline = (DateOnly?)null, daysLeft = (int?)null, cta = "مستندات الإغلاق", route = "/owner/documents/closure" };
        else if (offer is not null)
        {
            var v = await db.Solutions.AsNoTracking().FirstAsync(s => s.Id == offer.SolutionVersionId);
            var days = offer.ValidUntil.DayNumber - today.DayNumber;
            next = new
            {
                type = "offer", title = "راجع العرض الجديد",
                body = v.Kind == SolutionKind.ReducedPayoff ? $"سداد مخفض بمبلغ {v.RescheduledAmount:N2} ريال." : $"قسط شهري {v.InstallmentAmount:N2} ريال يوم {v.FirstDueDate.Day} من كل شهر.",
                deadline = offer.ValidUntil, daysLeft = days, cta = "عرض التفاصيل", route = $"/owner/offers/{offer.Id}",
            };
        }
        else if (docsNeeded.Count > 0)
        {
            var d = docsNeeded[0];
            var req = await db.DocumentRequests.AsNoTracking().Where(r => r.DocumentId == d.Id).OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
            next = new { type = "document", title = d.Status == DocumentStatus.Rejected ? $"ارفع نسخة جديدة من {d.Name}" : $"ارفع {d.Name}", body = "نحتاجه لمتابعة حالتك.",
                deadline = req?.DueOn, daysLeft = req is null ? (int?)null : req.DueOn.DayNumber - today.DayNumber, cta = "المستندات", route = "/owner/documents" };
        }
        else if (agreement is { Status: AgreementStatus.PendingActivation })
            next = new { type = "agreement", title = "سجّلنا موافقتك", body = $"يراجع المصرف الاتفاق {agreement.Number} ويفعّله قريباً. لا شيء مطلوب منك الآن.", deadline = (DateOnly?)null, daysLeft = (int?)null, cta = "تفاصيل الاتفاق", route = "/owner/agreement" };
        else if (agreement is { Status: AgreementStatus.Active })
        {
            var inst = await db.Installments.AsNoTracking().Where(i => i.AgreementId == agreement.Id && i.Status != InstallmentStatus.Matched).OrderBy(i => i.No).FirstOrDefaultAsync();
            if (inst is not null)
                next = new { type = "payment", title = "القسط القادم", body = $"{inst.Amount:N2} ريال · القسط {inst.No} من {agreement.InstallmentCount}", deadline = (DateOnly?)inst.DueDate,
                    daysLeft = (int?)(inst.DueDate.DayNumber - today.DayNumber), cta = "المدفوعات", route = "/owner/payments" };
        }
        var mgr = await ManagerAsync(db, c);
        var appt = await db.Appointments.AsNoTracking().Where(a => a.CaseId == c.Id && a.StartsAt > clock.UtcNow && a.Status != AppointmentStatus.Cancelled).OrderBy(a => a.StartsAt).FirstOrDefaultAsync();
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        return Results.Ok(new
        {
            greetingName = party.FullName.Split(' ')[0], lenderName = org.NameAr, caseRef = c.Reference,
            nextStep = next,
            journey = new { current = stage, total = 5, label = StageTitles[stage - 1] },
            docsSummary = docsNeeded.Count == 0 ? "كلها مكتملة" : $"{docsNeeded.Count} يحتاج رفعاً",
            debtSummary = "تفصيل واضح",
            caseManager = mgr is null ? null : new
            {
                firstName = mgr.Value.Name, initials = mgr.Value.Initials, role = mgr.Value.Role,
                nextAppointment = appt is null ? null : $"{(appt.Type == "call" ? "مكالمتكم" : "موعدكم")} {appt.StartsAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}",
            },
            unreadCount = await db.Notifications.CountAsync(n => n.UserId == rc.UserId && n.ReadAt == null),
        });
    }

    private static async Task<IResult> Journey(RahoonDbContext db, RequestContext rc)
    {
        var c = await OwnCaseAsync(db, rc);
        var stage = await OwnerStageAsync(db, c);
        var descriptions = new[]
        {
            "جمعنا بيانات التمويل والعقار.", "راجعنا المستندات وقيّم مكتب مستقل العقار.", "نعمل معك على حل يناسب دخلك.",
            "بعد موافقتك يُفعَّل الاتفاق.", "تسدد حسب الجدول، ثم نغلق الحالة ونرسل لك مستنداتها.",
        };
        var transitions = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id && e.Type == "case.transition").OrderBy(e => e.Seq)
            .Select(e => new { e.ToState, e.OccurredAt }).ToListAsync();
        string? When(params string[] states) => transitions.LastOrDefault(t => states.Contains(t.ToState))?.OccurredAt.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd");
        var metas = new[] { c.OpenedOn?.ToString("yyyy-MM-dd"), When("valuation", "proposed_solution"), null, null, null };
        return Results.Ok(new
        {
            steps = StageTitles.Select((t, i) => new
            {
                title = t,
                description = i + 1 == stage ? descriptions[i] + " أنت هنا." : descriptions[i],
                status = i + 1 < stage ? "done" : i + 1 == stage ? "current" : "todo",
                meta = i + 1 < stage ? metas[i] ?? "—" : i + 1 == stage ? "الآن" : "—",
            }),
            note = "لن يُتخذ أي إجراء قانوني دون إشعارك مسبقاً وإتاحة فرصة الاعتراض.",
        });
    }

    private static async Task<IResult> Documents(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await OwnCaseAsync(db, rc);
        var today = clock.TodayRiyadh;
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && (d.VisibleToOwner || d.Source == DocumentSource.Owner)).Include(d => d.Versions).ToListAsync();
        var requests = await db.DocumentRequests.AsNoTracking().Where(r => r.CaseId == c.Id).ToListAsync();
        var items = docs.Select(d =>
        {
            var current = d.Versions.OrderByDescending(v => v.VersionNo).FirstOrDefault();
            var req = requests.Where(r => r.DocumentId == d.Id).OrderByDescending(r => r.CreatedAt).FirstOrDefault();
            var expiringSoon = d.ValidUntil is { } vu && vu.DayNumber - today.DayNumber <= 30;
            var (status, tone) = d.Status switch
            {
                DocumentStatus.Rejected => ("مطلوب من جديد", "err"),
                DocumentStatus.Requested => ("مطلوب", "info"),
                DocumentStatus.InReview or DocumentStatus.Uploaded => ("قيد المراجعة", "warn"),
                DocumentStatus.Verified when d.Source != DocumentSource.Owner => ("أرسله المصرف", "ok"),
                DocumentStatus.Verified when expiringSoon => ("استلمناه · تنتهي صلاحيته قريباً", "ok"),
                DocumentStatus.Verified => ("استلمناه", "ok"),
                _ => ("—", "neutral"),
            };
            return new
            {
                d.Id, title = d.Name, status, tone, rawStatus = d.Status.ToString(),
                reason = d.Status == DocumentStatus.Rejected ? current?.OwnerFacingReason : null,
                help = d.Status == DocumentStatus.Rejected ? "جرّب ملف PDF من تطبيق البنك، أو صوّر الصفحة في مكان مضاء." : null,
                dueOn = req?.DueOn, requestedBy = org.NameAr, typeKey = d.DocumentTypeKey,
                canUpload = d.Status is DocumentStatus.Requested or DocumentStatus.Rejected,
            };
        }).OrderBy(i => i.rawStatus == "Rejected" ? 0 : i.rawStatus == "Requested" ? 1 : 2).ToList();
        return Results.Ok(new { items, needsAction = items.Count(i => i.canUpload) });
    }

    private static async Task<IResult> Upload(HttpRequest http, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, IFileScanner scanner,
        IClock clock, AuditLog audit, Notifier notifier)
    {
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملفاً أو صوّر المستند."] });
        if (!Guid.TryParse(form["documentId"], out var documentId)) Validate.Throw("documentId", "المستند غير محدد.");
        var c = await OwnCaseAsync(db, rc);
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.CaseId == c.Id) ?? throw new NotFoundException();
        if (doc.Status is not (DocumentStatus.Requested or DocumentStatus.Rejected)) throw new ConflictException("not_requested", "هذا المستند ليس مطلوباً منك الآن.");
        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, c.OrganizationId);
        if (await scanner.ScanAsync(stored.StorageKey) == ScanStatus.Infected) throw new DomainException("file_infected", "تعذّر قبول الملف. جرّب ملفاً آخر.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var version = new DocumentVersion
        {
            OrganizationId = c.OrganizationId, DocumentId = doc.Id, CaseId = c.Id, VersionNo = doc.VersionCount + 1, FileName = Path.GetFileName(file.FileName),
            ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey,
            UploadedByUserId = rc.UserId, UploadedByLabel = "المالك", UploadedAt = clock.UtcNow, ScanStatus = ScanStatus.Clean,
        };
        db.DocumentVersions.Add(version);
        doc.VersionCount = version.VersionNo;
        doc.CurrentVersionId = version.Id;
        doc.Status = DocumentStatus.InReview;
        doc.VisibleToOwner = true;
        foreach (var r in await db.DocumentRequests.Where(r => r.DocumentId == doc.Id && r.Status == DocumentRequestStatus.Open).ToListAsync()) r.Status = DocumentRequestStatus.Fulfilled;
        await NotifyManagerAsync(db, notifier, c, $"رفع المالك {doc.Name} v{version.VersionNo}", "بانتظار المراجعة", $"/cases/{c.Reference}/documents");
        await audit.RecordAsync(new AuditEntry("document.uploaded", $"رفع المالك {doc.Name} v{version.VersionNo}", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "قيد المراجعة", version = version.VersionNo });
    }

    private static async Task NotifyManagerAsync(RahoonDbContext db, Notifier notifier, Case c, string title, string? body, string link)
    {
        var mgr = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        if (mgr is { } u) notifier.Notify(u, c.OrganizationId, "owner", title, body, link, c.Id);
    }

    private static async Task<IResult> DocumentHelp(Guid documentId, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier)
    {
        var c = await OwnCaseAsync(db, rc);
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && d.CaseId == c.Id) ?? throw new NotFoundException();
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "owner", AuthorUserId = rc.UserId,
            AuthorLabel = rc.UserName, Body = $"أحتاج مساعدة في مستند «{doc.Name}».", At = clock.UtcNow,
        });
        await NotifyManagerAsync(db, notifier, c, $"طلب مساعدة من المالك: {doc.Name}", null, $"/cases/{c.Reference}/comms");
        await db.SaveChangesAsync();
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> Debt(RahoonDbContext db, RequestContext rc)
    {
        var c = await OwnCaseAsync(db, rc);
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        var d = await db.DebtSnapshots.AsNoTracking().Where(x => x.CaseId == c.Id && x.IsCurrent).OrderByDescending(x => x.AsOf).FirstOrDefaultAsync();
        if (d is null) return Results.Ok(new { total = (decimal?)null });
        var offerWaives = await db.Offers.AnyAsync(o => o.CaseId == c.Id && o.Status == OfferStatus.Sent && db.Solutions.Any(s => s.Id == o.SolutionVersionId && s.WaiverAmount > 0));
        return Results.Ok(new
        {
            total = d.Total, currency = "ريال", source = $"من سجلات {org.NameAr} · تحديث {d.AsOf.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
            items = new[]
            {
                new { key = "principal", label = "أصل التمويل المتبقي", amount = d.Principal, explanation = "المبلغ الذي استلمته ولم يُسدَّد بعد." },
                new { key = "profit", label = "الأرباح المستحقة", amount = d.Profit, explanation = "أرباح التمويل حسب عقدك للفترة المتبقية حتى اليوم." },
                new { key = "late_fees", label = "غرامات التأخير", amount = d.LateFees, explanation = offerWaives ? "ناتجة عن الأقساط المتأخرة. العرض الحالي يلغيها." : "ناتجة عن الأقساط المتأخرة." },
                new { key = "other", label = "رسوم أخرى", amount = d.OtherFees, explanation = d.OtherFees == 0 ? "لا توجد رسوم إضافية." : "رسوم إدارية حسب العقد." },
            },
        });
    }

    private static async Task<IResult> Options(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await OwnCaseAsync(db, rc);
        var offer = await db.Offers.AsNoTracking().Where(o => o.CaseId == c.Id && o.Status == OfferStatus.Sent && o.ValidUntil >= clock.TodayRiyadh).OrderByDescending(o => o.SentAt).FirstOrDefaultAsync();
        object? active = null;
        if (offer is not null)
        {
            var v = await db.Solutions.AsNoTracking().FirstAsync(s => s.Id == offer.SolutionVersionId);
            active = new
            {
                offer.Id, eyebrow = "عرض مقدَّم لك",
                title = v.Kind switch { SolutionKind.ReducedPayoff => "سداد بمبلغ مخفض", SolutionKind.GracePeriod => "تأجيل مؤقت ثم استئناف", _ => "قسط أقل لمدة أطول" },
                body = v.Kind == SolutionKind.ReducedPayoff ? $"تسدد {v.RescheduledAmount:N2} ريال دفعة واحدة وتنتهي المديونية." : $"تبقى في منزلك، وتدفع {v.InstallmentAmount:N2} ريال شهرياً لمدة {v.TermMonths} شهراً.",
            };
        }
        return Results.Ok(new
        {
            activeOffer = active,
            otherOptions = new[]
            {
                new { key = "grace", title = "تأجيل مؤقت", description = "إيقاف الأقساط لفترة قصيرة إذا مررت بظرف مؤقت، ثم الاستئناف.", link = "اسأل عن هذا الخيار" },
                new { key = "voluntary_sale", title = "البيع الطوعي", description = "إذا رغبت أنت في بيع العقار بنفسك وبسعر السوق، يمكننا مساعدتك في الترتيب. هذا خيارك فقط.", link = "أريد معرفة المزيد" },
            },
        });
    }

    private static async Task<IResult> Inquiry(string key, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier, AuditLog audit)
    {
        if (key is not ("grace" or "voluntary_sale")) throw new NotFoundException();
        var c = await OwnCaseAsync(db, rc);
        var label = key == "grace" ? "التأجيل المؤقت" : "البيع الطوعي";
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "owner", AuthorUserId = rc.UserId,
            AuthorLabel = rc.UserName, Body = $"أرغب في معرفة المزيد عن خيار «{label}».", At = clock.UtcNow,
        });
        var mgr = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        notifier.Task(c.OrganizationId, c.Id, $"استفسار المالك عن خيار «{label}»", mgr, BusinessDays.Add(clock.TodayRiyadh, 1), "owner_inquiry", $"/cases/{c.Reference}/comms", rc.UserId);
        await audit.RecordAsync(new AuditEntry("owner.option_inquiry", $"استفسار المالك عن {label}", c.Id, c.Reference, Detail: "استفسار وليس التزاماً", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { sent = true, message = "وصل سؤالك إلى مسؤول حالتك وسيتواصل معك خلال يوم عمل." });
    }

    private static async Task<(Case Case, Offer Offer, SolutionVersion Version)> LoadOfferAsync(RahoonDbContext db, RequestContext rc, Guid id, bool track = false)
    {
        var c = await OwnCaseAsync(db, rc, track);
        var offer = await (track ? db.Offers : db.Offers.AsNoTracking()).FirstOrDefaultAsync(o => o.Id == id && o.CaseId == c.Id) ?? throw new NotFoundException();
        var v = await (track ? db.Solutions : db.Solutions.AsNoTracking()).FirstAsync(s => s.Id == offer.SolutionVersionId);
        return (c, offer, v);
    }

    private static async Task<IResult> OfferDetail(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var (c, offer, v) = await LoadOfferAsync(db, rc, id);
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        var expired = offer.Status == OfferStatus.Sent && offer.ValidUntil < clock.TodayRiyadh;
        return Results.Ok(new
        {
            offer.Id, status = expired ? "Expired" : offer.Status.ToString(), validUntil = offer.ValidUntil, version = v.VersionNo, kind = v.Kind.ToString(),
            installment = v.InstallmentAmount, dueDay = v.FirstDueDate.Day, termMonths = v.TermMonths, start = v.FirstDueDate, end = v.LastDueDate,
            waiver = v.WaiverAmount, rescheduled = v.RescheduledAmount, cureDays = v.BreachCureDays, missedConsecutive = v.BreachMissedConsecutive,
            phoneMasked = party.PhoneMasked,
            summary = new[]
            {
                v.Kind == SolutionKind.ReducedPayoff ? $"دفعة واحدة {v.RescheduledAmount:N2} ريال" : $"{v.TermMonths} قسطاً × {v.InstallmentAmount:N2} ريال",
                $"يوم {v.FirstDueDate.Day} من كل شهر، يبدأ {v.FirstDueDate:yyyy-MM-dd}",
            }.Concat(v.WaiverAmount > 0 ? [$"إلغاء غرامات التأخير {v.WaiverAmount:N0} ريال"] : []).Append("السداد بتحويل إلى حساب المصرف").ToArray(),
        });
    }

    private static async Task<IResult> Decline(Guid id, DeclineRequest req, RahoonDbContext db, RequestContext rc, IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, offer, v) = await LoadOfferAsync(db, rc, id, track: true);
        if (offer.Status != OfferStatus.Sent) throw new ConflictException("offer_closed", "لم يعد هذا العرض قائماً.");
        // Decline returns the case to solution preparation — never to referral.
        await workflow.TransitionAsync(c, "owner_declined", req.Reason ?? "المالك: لا يناسبني", CaseStatus.AwaitingCustomer, systemInitiated: true);
        offer.Status = OfferStatus.Declined;
        offer.RespondedAt = clock.UtcNow;
        offer.DeclineReason = req.Reason?.Trim();
        v.Status = SolutionStatus.Declined;
        db.NegotiationEntries.Add(new NegotiationEntry
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, OfferId = offer.Id, Kind = NegotiationKind.OwnerDecline, AuthorType = "owner", AuthorUserId = rc.UserId,
            AuthorLabel = rc.UserName, At = clock.UtcNow, Body = string.IsNullOrWhiteSpace(req.Reason) ? "العرض لا يناسبني." : req.Reason.Trim(),
        });
        await NotifyManagerAsync(db, notifier, c, $"المالك لم يقبل العرض v{v.VersionNo}", req.Reason, $"/cases/{c.Reference}/solutions");
        await audit.RecordAsync(new AuditEntry("owner.offer_declined", $"المالك لم يقبل العرض v{v.VersionNo}", c.Id, c.Reference, Reason: req.Reason, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { message = "سجّلنا ردك. سيتواصل معك مسؤول حالتك للبحث عن خيار آخر، ولن يُتخذ أي إجراء آخر دون إشعارك." });
    }

    private static async Task<IResult> Counter(Guid id, CounterRequest req, RahoonDbContext db, RequestContext rc, IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier)
    {
        var v1 = new Validator();
        DateOnly? start = null;
        if (req.FirstMonth is { Length: > 0 })
        {
            if (DateOnly.TryParseExact(req.FirstMonth + "-01", "yyyy-MM-dd", out var m)) start = m;
            else v1.Require(false, "firstMonth", "اختر الشهر المناسب.");
        }
        v1.Require(req.InstallmentDay is null or (>= 1 and <= 28), "installmentDay", "اختر يوماً بين 1 و28.");
        v1.Require(req.InstallmentAmount is null or > 0, "installmentAmount", "المبلغ يجب أن يكون أكبر من صفر.");
        v1.Require(req.InstallmentDay is not null || start is not null || req.InstallmentAmount is not null, "changes", "اختر ما تود تغييره.");
        v1.Require((req.Reason?.Length ?? 0) <= 500, "reason", "السبب حتى 500 حرف.");
        v1.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, offer, v) = await LoadOfferAsync(db, rc, id, track: true);
        if (offer.Status != OfferStatus.Sent || offer.ValidUntil < clock.TodayRiyadh) throw new ConflictException("offer_closed", "لم يعد هذا العرض قائماً.");
        await workflow.TransitionAsync(c, "owner_counteroffer", "اقتراح من المالك", CaseStatus.AwaitingCustomer, systemInitiated: true);
        var requestedStart = start is { } s ? new DateOnly(s.Year, s.Month, Math.Min(req.InstallmentDay ?? v.FirstDueDate.Day, 28)) : req.InstallmentDay is { } day ? new DateOnly(v.FirstDueDate.Year, v.FirstDueDate.Month, day) : (DateOnly?)null;
        offer.Status = OfferStatus.Countered;
        offer.RespondedAt = clock.UtcNow;
        v.Status = SolutionStatus.Countered;
        var terms = new List<string>();
        if (req.InstallmentDay is { } d) terms.Add($"تاريخ الاستحقاق يوم {d}");
        if (requestedStart is { } rs) terms.Add($"البدء {rs:yyyy-MM-dd}");
        if (req.InstallmentAmount is { } amt) terms.Add($"القسط {amt:N2}");
        db.NegotiationEntries.Add(new NegotiationEntry
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, OfferId = offer.Id, Kind = NegotiationKind.OwnerCounter, AuthorType = "owner", AuthorUserId = rc.UserId,
            AuthorLabel = $"{rc.UserName} — اقتراح بديل", At = clock.UtcNow, Body = string.IsNullOrWhiteSpace(req.Reason) ? "أقترح تعديل موعد القسط." : req.Reason.Trim(),
            RequestedDueDay = req.InstallmentDay, RequestedStartDate = requestedStart, RequestedInstallment = req.InstallmentAmount,
        });
        c.StageDueOn = BusinessDays.Add(clock.TodayRiyadh, 3);
        var mgr = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        notifier.Task(c.OrganizationId, c.Id, "مراجعة رد على العرض المقابل", mgr, c.StageDueOn, "negotiation", $"/cases/{c.Reference}/solutions/negotiation", rc.UserId);
        await NotifyManagerAsync(db, notifier, c, "اقتراح بديل من المالك", "طلب: " + string.Join(" · ", terms), $"/cases/{c.Reference}/solutions/negotiation");
        await audit.RecordAsync(new AuditEntry("owner.counteroffer", $"اقتراح بديل من المالك على العرض v{v.VersionNo}", c.Id, c.Reference, Detail: "طلب: " + string.Join(" · ", terms), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { responseDueOn = c.StageDueOn, message = "وصل اقتراحك. سيُراجَع، وقد يُقبل أو يُعدَّل، وسنرد خلال 3 أيام عمل." });
    }

    private static async Task<IResult> ConsentOtp(Guid id, RahoonDbContext db, RequestContext rc, OtpService otp, PiiProtector pii, IClock clock)
    {
        var (c, offer, _) = await LoadOfferAsync(db, rc, id);
        if (offer.Status != OfferStatus.Sent || offer.ValidUntil < clock.TodayRiyadh) throw new ConflictException("offer_closed", "لم يعد هذا العرض قائماً.");
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        var issued = await otp.IssueAsync(OtpPurpose.Consent, pii.Unprotect(party.PhoneEnc!), rc.UserId, rc.SessionId, context: offer.Id.ToString(), orgId: c.OrganizationId, caseId: c.Id);
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    /// <summary>
    /// Records informed consent (two acknowledgements + OTP) and creates the agreement pending
    /// activation. This is a consent record, not a licensed electronic signature (A-05).
    /// </summary>
    private static async Task<IResult> Consent(Guid id, ConsentRequest req, HttpContext http, RahoonDbContext db, RequestContext rc, IClock clock,
        OtpService otp, AgreementService agreements, AuditLog audit, Notifier notifier)
    {
        if (req.Acknowledgements is null || !Acks.All(req.Acknowledgements.Contains))
            Validate.Throw("acknowledgements", "يجب تأكيد الإقرارين قبل الموافقة.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, offer, v) = await LoadOfferAsync(db, rc, id, track: true);
        if (offer.Status != OfferStatus.Sent) throw new ConflictException("offer_closed", "لم يعد هذا العرض قائماً.");
        if (offer.ValidUntil < clock.TodayRiyadh) throw new ConflictException("offer_expired", "انتهت صلاحية العرض. يمكنك طلب عرض جديد.");
        if (!await otp.VerifyAsync(OtpPurpose.Consent, rc.SessionId, req.Code, offer.Id.ToString()))
            throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. اطلب رمزاً جديداً.", 401);

        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        var text = $"{c.Reference}|v{v.VersionNo}|{v.TermMonths}x{v.InstallmentAmount}|{v.FirstDueDate:yyyy-MM-dd}|waiver {v.WaiverAmount}";
        var consent = new ConsentRecord
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, OfferId = offer.Id, PartyId = party.Id, Kind = "offer_acceptance", AcceptedAt = clock.UtcNow,
            Channel = "portal", OtpDestinationMasked = party.PhoneMasked, OtpVerifiedAt = clock.UtcNow, Device = DeviceLabel(http.Request.Headers.UserAgent.ToString()),
            IpMasked = rc.IpMasked, Acknowledgements = [.. Acks], TextHash = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
        };
        db.ConsentRecords.Add(consent);
        offer.Status = OfferStatus.Accepted;
        offer.RespondedAt = clock.UtcNow;
        v.Status = SolutionStatus.Accepted;
        var agreement = await agreements.CreatePendingAsync(c, offer, v, consent);
        db.NegotiationEntries.Add(new NegotiationEntry
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, OfferId = offer.Id, Kind = NegotiationKind.OwnerAccept, AuthorType = "owner", AuthorUserId = rc.UserId,
            AuthorLabel = rc.UserName, At = clock.UtcNow, Body = $"قبول العرض v{v.VersionNo} داخل البوابة برمز تحقق.",
        });
        c.StageDueOn = BusinessDays.Add(clock.TodayRiyadh, 2);

        // Activation still needs legal review and a schedule, by people with those permissions.
        var legal = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.Roles.Any(r => r.Role!.Key == SystemRoles.Legal))
            .Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        notifier.Task(c.OrganizationId, c.Id, $"مراجعة وتفعيل الاتفاق {agreement.Number}", legal, c.StageDueOn, "agreement", $"/cases/{c.Reference}/agreement", rc.UserId);
        if (legal is { } l) notifier.Notify(l, c.OrganizationId, "agreement", $"قبل المالك العرض v{v.VersionNo}", $"{agreement.Number} بانتظار المراجعة والتفعيل", $"/cases/{c.Reference}/agreement", c.Id, "ok");
        await NotifyManagerAsync(db, notifier, c, $"قبل المالك العرض v{v.VersionNo}", agreement.Number, $"/cases/{c.Reference}/agreement");
        await audit.RecordAsync(new AuditEntry("owner.offer_accepted", $"قبول المالك للعرض v{v.VersionNo}", c.Id, c.Reference,
            Detail: $"سجل موافقة + رمز تحقق إلى {party.PhoneMasked} · {consent.Device} · ليس توقيعاً مرخّصاً", Evidence: [consent.TextHash], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new
        {
            agreementRef = agreement.Number, recordedAt = consent.AcceptedAt, hijriDate = Hijri.Format(clock.TodayRiyadh), firstDue = agreement.StartDate,
            message = $"سيراجع المصرف الاتفاق ويفعّله خلال يومي عمل. أول قسط {agreement.StartDate:yyyy-MM-dd}.",
        });
    }

    private static string DeviceLabel(string ua) =>
        ua.Contains("iPhone") ? "iPhone" : ua.Contains("Android") ? "Android" : ua.Contains("Windows") ? "Windows" : ua.Contains("Mac") ? "Mac" : "متصفح";

    private static async Task<IResult> AgreementView(RahoonDbContext db, RequestContext rc)
    {
        var c = await OwnCaseAsync(db, rc);
        var a = await db.Agreements.AsNoTracking().Where(x => x.CaseId == c.Id).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (a is null) return Results.Ok(new { agreement = (object?)null });
        var consent = a.ConsentRecordId is null ? null : await db.ConsentRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.ConsentRecordId);
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        return Results.Ok(new
        {
            agreement = new
            {
                a.Number, status = a.Status.ToString(), a.VersionLabel, lender = org.NameAr, a.RescheduledAmount, a.WaiverAmount, a.InstallmentCount, a.InstallmentAmount,
                a.DueDay, a.StartDate, a.EndDate, startHijri = Hijri.Format(a.StartDate), a.BreachMissedConsecutive, a.BreachCureDays,
                consentAt = consent?.AcceptedAt, consentHijri = consent is null ? null : Hijri.Format(DateOnly.FromDateTime(consent.AcceptedAt.ToOffset(TimeSpan.FromHours(3)).DateTime)),
                signature = "سجل موافقة داخل المنصة برمز تحقق — ليس توقيعاً إلكترونياً مرخّصاً.",
            },
        });
    }

    private static async Task<IResult> Payments(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await OwnCaseAsync(db, rc);
        var a = await db.Agreements.AsNoTracking().Where(x => x.CaseId == c.Id && (x.Status == AgreementStatus.Active || x.Status == AgreementStatus.BreachReview || x.Status == AgreementStatus.Completed))
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (a is null) return Results.Ok(new { active = false, message = "سيظهر جدول السداد هنا بعد تفعيل الاتفاق." });
        var installments = await db.Installments.AsNoTracking().Where(i => i.AgreementId == a.Id).OrderBy(i => i.No).ToListAsync();
        var matched = await db.Payments.AsNoTracking().Where(p => p.CaseId == c.Id && p.Status == PaymentStatus.Matched).ToListAsync();
        var notices = await db.PaymentNotices.AsNoTracking().Where(n => n.CaseId == c.Id).ToListAsync();
        var next = installments.FirstOrDefault(i => i.Status != InstallmentStatus.Matched && i.Status != InstallmentStatus.Waived);
        return Results.Ok(new
        {
            active = true, total = a.InstallmentCount,
            next = next is null ? null : new { next.No, next.Amount, next.DueDate },
            note = "تدفع مباشرة للمصرف بتحويل بنكي. رهون لا تستلم أي مبالغ.",
            // Owner sees matched payments as received; recorded-but-unmatched only as "being confirmed".
            items = installments.Take(Math.Max(3, installments.Count(i => i.Status != InstallmentStatus.Upcoming) + 1)).Select(i =>
            {
                var p = matched.FirstOrDefault(x => x.InstallmentId == i.Id);
                var notice = notices.FirstOrDefault(n => n.InstallmentNo == i.No);
                var (status, tone, meta) = i.Status switch
                {
                    InstallmentStatus.Matched => ("مستلم", "ok", $"مرجع {p?.BankReference} · إيصال متاح"),
                    InstallmentStatus.RecordedPendingMatch => ("قيد التأكيد", "warn", "استلمنا إشعارك · نتحقق مع المصرف"),
                    InstallmentStatus.Overdue => ("متأخر", "err", notice is null ? "إن سددت، أرسل إشعار التحويل" : "استلمنا إشعارك · نتحقق مع المصرف"),
                    _ when notice is not null => ("قيد التأكيد", "warn", "استلمنا إشعارك · نتحقق مع المصرف"),
                    _ => (i.Status == InstallmentStatus.Due ? "مستحق" : "قادم", "neutral", "—"),
                };
                return new { i.No, i.DueDate, i.Amount, status, tone, meta, canNotify = i.Status is InstallmentStatus.Due or InstallmentStatus.Overdue && notice is null };
            }),
            howToPay = new
            {
                title = "كيف أدفع؟",
                steps = new[] { "حوّل مبلغ القسط من حسابك إلى حساب التمويل لدى المصرف.", $"اكتب مرجع حالتك {c.Reference} في وصف التحويل.", "أرسل إشعار التحويل من هنا ليتحقق منه المصرف." },
            },
        });
    }

    private static async Task<IResult> Notice(int no, PaymentNoticeRequest req, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier)
    {
        new Validator().Require(req.Amount > 0, "amount", "المبلغ مطلوب.").Require(req.TransferDate <= clock.TodayRiyadh, "transferDate", "تاريخ التحويل لا يكون في المستقبل.").ThrowIfInvalid();
        var c = await OwnCaseAsync(db, rc);
        if (!await db.Installments.AnyAsync(i => i.CaseId == c.Id && i.No == no)) throw new NotFoundException();
        db.PaymentNotices.Add(new PaymentNotice { OrganizationId = c.OrganizationId, CaseId = c.Id, InstallmentNo = no, TransferDate = req.TransferDate, Amount = req.Amount, Reference = req.Reference?.Trim() });
        var finance = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.Roles.Any(r => r.Role!.Key == SystemRoles.Finance))
            .Select(m => m.UserId).ToListAsync();
        foreach (var f in finance) notifier.Notify(f, c.OrganizationId, "payment", $"إشعار تحويل من المالك · القسط {no}", $"{c.Reference} · {req.Amount:N2}", $"/cases/{c.Reference}/payments", c.Id);
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "استلمنا إشعارك. سيتحقق المصرف من التحويل، وتظهر الدفعة «مستلم» بعد المطابقة." });
    }

    private static async Task<IResult> Hardship(HardshipBody req, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier, AuditLog audit)
    {
        string[] keys = ["income_loss", "health", "family", "other", "prefer_not_say"];
        if (req.ReasonKey is not null && !keys.Contains(req.ReasonKey)) Validate.Throw("reasonKey", "اختر سبباً من القائمة أو اتركه فارغاً.");
        var c = await OwnCaseAsync(db, rc);
        db.HardshipRequests.Add(new HardshipRequest { OrganizationId = c.OrganizationId, CaseId = c.Id, ReasonKey = req.ReasonKey });
        var mgr = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        notifier.Task(c.OrganizationId, c.Id, "طلب مكالمة من المالك (ظرف طارئ)", mgr, BusinessDays.Add(clock.TodayRiyadh, 1), "hardship_callback", $"/cases/{c.Reference}/comms", rc.UserId);
        await audit.RecordAsync(new AuditEntry("owner.hardship", "المالك أبلغ عن تغيّر في وضعه وطلب مكالمة", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "وصل طلبك. سيتصل بك مسؤول حالتك خلال يوم عمل." });
    }

    private static async Task<IResult> Messages(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await OwnCaseAsync(db, rc);
        var mgr = await ManagerAsync(db, c);
        var msgs = await db.Messages.Where(m => m.CaseId == c.Id && !m.InternalOnly && m.Channel == MessageChannel.Portal).OrderBy(m => m.At).ToListAsync();
        foreach (var m in msgs.Where(m => m.AuthorType == "lender" && m.ReadByOwnerAt == null)) m.ReadByOwnerAt = clock.UtcNow;
        await db.SaveChangesAsync();
        var appts = await db.Appointments.AsNoTracking().Where(a => a.CaseId == c.Id && a.Status != AppointmentStatus.Cancelled && a.StartsAt > clock.UtcNow.AddDays(-1))
            .OrderBy(a => a.StartsAt).ToListAsync();
        return Results.Ok(new
        {
            with = mgr?.Name,
            replyHint = "الرد المتوقع خلال يوم عمل",
            appointments = appts.Select(a => new { a.Id, a.Type, a.StartsAt, status = a.Status.ToString(), statusText = a.Status == AppointmentStatus.Confirmed ? "أكّدتَ الحضور · يمكنك إعادة الجدولة" : "بانتظار تأكيدك" }),
            messages = msgs.Select(m => new
            {
                m.Id, mine = m.AuthorType == "owner", body = m.Body, at = m.At,
                author = m.AuthorType == "owner" ? "أنت" : m.AuthorLabel.Split(' ')[0],
                read = m.AuthorType == "owner" && m.ReadByTeamAt != null,
            }),
        });
    }

    private static async Task<IResult> SendMessage(OwnerMessageRequest req, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Body) && req.Body.Length <= 2000, "body", "اكتب رسالتك (حتى 2000 حرف).").ThrowIfInvalid();
        var c = await OwnCaseAsync(db, rc);
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "owner", AuthorUserId = rc.UserId,
            AuthorLabel = rc.UserName, Body = req.Body.Trim(), At = clock.UtcNow,
        });
        await NotifyManagerAsync(db, notifier, c, "رسالة جديدة من المالك", req.Body.Trim().Length > 80 ? req.Body.Trim()[..80] + "…" : req.Body.Trim(), $"/cases/{c.Reference}/comms");
        await db.SaveChangesAsync();
        return Results.Ok(new { sent = true });
    }

    private static async Task<IResult> ConfirmAppointment(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await OwnCaseAsync(db, rc);
        var a = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        a.Status = AppointmentStatus.Confirmed;
        a.ConfirmedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { status = a.Status.ToString() });
    }

    private static async Task<IResult> RescheduleAppointment(Guid id, RescheduleRequest req, RahoonDbContext db, RequestContext rc, IClock clock, Notifier notifier)
    {
        var c = await OwnCaseAsync(db, rc);
        var a = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        a.Status = AppointmentStatus.Rescheduled;
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "owner", AuthorUserId = rc.UserId, AuthorLabel = rc.UserName,
            Body = $"أرغب في إعادة جدولة الموعد.{(string.IsNullOrWhiteSpace(req.Note) ? "" : " " + req.Note.Trim())}", At = clock.UtcNow,
        });
        await NotifyManagerAsync(db, notifier, c, "المالك يطلب إعادة جدولة موعد", req.Note, $"/cases/{c.Reference}/comms");
        await db.SaveChangesAsync();
        return Results.Ok(new { status = a.Status.ToString() });
    }

    private static async Task<IResult> Complaints(RahoonDbContext db, RequestContext rc)
    {
        var c = await OwnCaseAsync(db, rc);
        return Results.Ok(await db.Complaints.AsNoTracking().Where(x => x.CaseId == c.Id).OrderByDescending(x => x.SubmittedAt).Select(x => new
        {
            x.Reference, type = x.Type.ToString(), x.Subject, status = x.Status.ToString(), x.SubmittedAt, x.DueOn, response = x.RespondedAt != null ? x.ResponseText : null, x.RespondedAt,
        }).ToListAsync());
    }

    private static async Task<IResult> SubmitComplaint(OwnerComplaintRequest req, RahoonDbContext db, RequestContext rc, IClock clock, ComplaintService complaints)
    {
        new Validator().Require(req.Type is "complaint" or "objection", "type", "اختر نوع الطلب.")
            .Require(!string.IsNullOrWhiteSpace(req.Body) && req.Body.Trim().Length >= 10, "body", "اشرح لنا ما حدث (10 أحرف على الأقل).").ThrowIfInvalid();
        var c = await OwnCaseAsync(db, rc, track: true);
        var complaint = await complaints.SubmitAsync(c, req.Type == "objection" ? ComplaintType.Objection : ComplaintType.Complaint,
            req.Subject ?? (req.Type == "objection" ? "اعتراض على مبلغ أو قرار" : "شكوى على طريقة التعامل"), req.Body.Trim(), "owner_portal", rc.UserId, rc.UserName);
        return Results.Ok(new { reference = complaint.Reference, dueOn = complaint.DueOn, message = $"وصلت شكواك برقم {complaint.Reference}. ستراجعها جهة مستقلة عن فريق حالتك، ونرد عليك كتابياً خلال 5 أيام عمل." });
    }

    private static async Task<IResult> ClosureView(RahoonDbContext db, RequestContext rc)
    {
        var c = await OwnCaseAsync(db, rc);
        var docs = await db.ClosureDocuments.AsNoTracking().Where(d => d.CaseId == c.Id && d.VisibleToOwner && d.Status == ClosureDocumentStatus.Ready).ToListAsync();
        return Results.Ok(new
        {
            closed = c.Status == CaseStatus.Closed, closedAt = c.ClosedAt,
            documents = docs.Select(d => new { d.Title, date = d.PreparedOn, fileVersionId = d.DocumentVersionId }),
            accessExpiresAt = c.ClosedAt?.AddDays(90),
        });
    }

    private static async Task<IResult> DownloadOwnFile(Guid versionId, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, AuditLog audit)
    {
        var c = await OwnCaseAsync(db, rc);
        var v = await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.CaseId == c.Id) ?? throw new NotFoundException();
        var doc = await db.Documents.AsNoTracking().FirstAsync(d => d.Id == v.DocumentId);
        var isClosureDoc = await db.ClosureDocuments.AnyAsync(d => d.DocumentVersionId == v.Id && d.VisibleToOwner);
        if (!doc.VisibleToOwner && !isClosureDoc) throw new NotFoundException();
        await audit.RecordAsync(new AuditEntry("document.downloaded", $"تنزيل المالك {doc.Name}", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.File(await storage.OpenReadAsync(v.StorageKey), v.ContentType, v.FileName);
    }
}
