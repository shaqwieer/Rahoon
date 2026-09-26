using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Requests;

/// <summary>Autosave of one wizard step. Every field of the named step is written (null clears it).</summary>
public sealed record MyRequestPatch(
    string Step,
    Guid? InstitutionId = null,
    string? InstitutionOtherName = null,
    string? ApplicantFullName = null,
    string? ContractNumber = null,
    decimal? MonthlyInstallment = null,
    string? ArrearsDuration = null,
    string? PropertyCity = null,
    string? PathPreference = null,
    decimal? AffordableMonthly = null,
    string? SituationText = null);

public sealed record ConsentConfirm(string? Code, bool Accept);
public sealed record SubmitMyRequest(bool AcknowledgeDuplicate);
public sealed record AddInfoRequest(string? Text);
public sealed record WithdrawMyRequest(string? Reason);

/// <summary>
/// The individual's requests (OA01–OA05 + tracking, ADR 0001). Every read goes through the applicant query filter —
/// no system scope — so another individual's request is simply not found. DTOs are explicit: team-only documents,
/// internal timeline entries and coordination details are never projected here.
/// </summary>
public static class MyRequestEndpoints
{
    /// <summary>V4: provisional consent wording («صيغة مبدئية — تتطلب مراجعة»). The version is stored with every consent.</summary>
    public const string ConsentTextVersion = "request-consent-draft-2026-09";
    public static string ConsentText(string recipient) =>
        $"أوافق على أن تشارك منصة رهون مع {recipient} البيانات والمستندات اللازمة لدراسة طلبي والتنسيق بشأنه. " +
        "تُسجَّل موافقتي بتاريخها ونصها، ويمكنني سحبها، ويتوقف عندها التنسيق مع جهتي.";
    private static readonly List<string> ConsentCategories = ["identity", "contact", "declared_finance", "situation", "documents"];

    public static readonly string[] ArrearsKeys = ["not_late", "lt3m", "3to6m", "6to12m", "gt12m"];
    public static readonly Dictionary<string, string> DocumentKinds = new()
    {
        ["salary_statement"] = "تعريف بالراتب",
        ["bank_statement"] = "كشف حساب آخر 3 أشهر",
        ["title_deed"] = "صورة الصك",
        ["financing_contract"] = "عقد التمويل",
        ["other"] = "مستند آخر",
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/institutions", Institutions).RequireSession();

        var g = app.MapGroup("/api/my/requests").RequireIndividual();
        g.MapGet("", List);
        g.MapPost("", Create).Idempotent();
        g.MapGet("/{reference}", Get);
        g.MapPatch("/{reference}", Patch);
        g.MapPost("/{reference}/consent/otp", ConsentOtp);
        g.MapPost("/{reference}/consent", Consent).Idempotent();
        g.MapPost("/{reference}/consent/withdraw", WithdrawConsent).Idempotent();
        g.MapPost("/{reference}/documents", Upload).DisableAntiforgery();
        g.MapGet("/{reference}/documents/{versionId:guid}/file", Download);
        g.MapPost("/{reference}/submit", Submit).Idempotent();
        g.MapPost("/{reference}/additions", AddInfo).Idempotent();
        g.MapPost("/{reference}/withdraw", Withdraw).Idempotent();
        g.MapGet("/{reference}/messages", Messages);
        g.MapPost("/{reference}/messages", SendMessage).Idempotent();
    }

    // ───────── messages with the Rahoon team (internal notes never projected) ─────────

    private static async Task<IResult> Messages(string reference, RequestAccess access, RahoonDbContext db)
    {
        var r = await access.ForApplicantAsync(reference, track: false);
        var items = await db.RequestMessages.AsNoTracking().Where(m => m.RequestId == r.Id && !m.IsInternal).OrderBy(m => m.At)
            .Select(m => new { m.Id, m.AuthorKind, author = m.AuthorKind == "team" ? "فريق رهون" : "أنت", m.Body, m.At }).ToListAsync();
        return Results.Ok(new { r.Reference, canSend = !RequestStatusInfo.IsTerminal(r.Status) && r.Status != RequestStatus.Draft, items });
    }

    private static async Task<IResult> SendMessage(string reference, AddInfoRequest body, RequestAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var text = Clean(body.Text, 2000);
        if (text is null) Validate.Throw("text", "اكتب رسالتك.");
        var r = await access.ForApplicantAsync(reference, write: true);
        if (r.Status == RequestStatus.Draft || RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "لا يمكن إرسال رسائل على هذا الطلب.");
        db.RequestMessages.Add(new RequestMessage
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, AuthorKind = "applicant", AuthorUserId = rc.UserId,
            AuthorLabel = r.ApplicantFullName ?? "العميل", Body = text!, At = clock.UtcNow,
        });
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.message_sent", "رسالة من العميل إلى فريق رهون"));
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> Institutions(RahoonDbContext db) =>
        Results.Ok(await db.FinancingInstitutions.AsNoTracking().Where(i => i.Active).OrderBy(i => i.SortOrder).ThenBy(i => i.NameAr)
            .Select(i => new { i.Id, i.NameAr, i.NameEn, i.Kind }).ToListAsync());

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc)
    {
        var rows = await db.Requests.AsNoTracking().Include(r => r.Institution)
            .Where(r => r.ApplicantUserId == rc.UserId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();
        return Results.Ok(rows.Select(r => new
        {
            r.Reference,
            status = RequestStatusInfo.Key(r.Status),
            waitingOn = RequestStatusInfo.WaitingKey(r.WaitingOn),
            institutionName = r.InstitutionDisplayName,
            r.CreatedAt,
            r.SubmittedAt,
            r.StatusChangedAt,
        }));
    }

    private static async Task<IResult> Create(RahoonDbContext db, RequestContext rc, RequestService svc, IClock clock, AuditLog audit)
    {
        // Reuse an untouched draft instead of piling up empty ones.
        var empty = await db.Requests.Where(r => r.ApplicantUserId == rc.UserId && r.Status == RequestStatus.Draft && r.InstitutionId == null && r.InstitutionOtherName == null)
            .OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
        if (empty is not null) return Results.Ok(new { empty.Reference });

        var now = clock.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = new Request
        {
            OrganizationId = await svc.OperatorOrganizationIdAsync(), Reference = await svc.NextReferenceAsync(), ApplicantUserId = rc.UserId,
            Status = RequestStatus.Draft, WaitingOn = RequestWaitingOn.Applicant, StatusChangedAt = now,
        };
        db.Requests.Add(r);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.created", "بدء طلب معالجة (مسودة)"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { r.Reference });
    }

    private static async Task<IResult> Get(string reference, RequestAccess access, RahoonDbContext db, RequestContext rc)
    {
        var r = await access.ForApplicantAsync(reference, track: false);
        return Results.Ok(await DetailAsync(db, r));
    }

    internal static async Task<object> DetailAsync(RahoonDbContext db, Request r)
    {
        var consent = await RequestWorkflow.ActiveConsentAsync(db, r);
        var docs = await db.RequestDocuments.AsNoTracking().Include(d => d.Versions)
            .Where(d => d.RequestId == r.Id && d.Visibility == RequestDocumentVisibility.ApplicantAndTeam)
            .OrderBy(d => d.CreatedAt).ToListAsync();
        var timeline = await db.RequestUpdates.AsNoTracking().Where(u => u.RequestId == r.Id && u.VisibleToApplicant)
            .OrderByDescending(u => u.At).Select(u => new { u.Kind, u.Title, u.Body, u.At, u.AuthorKind }).ToListAsync();
        var duplicate = await RequestWorkflow.FindDuplicateAsync(db, r);
        var recipient = string.IsNullOrWhiteSpace(r.InstitutionDisplayName) ? "جهتك الممولة" : r.InstitutionDisplayName;
        return new
        {
            r.Reference,
            status = RequestStatusInfo.Key(r.Status),
            statusLabel = RequestStatusInfo.LabelAr(r.Status),
            waitingOn = RequestStatusInfo.WaitingKey(r.WaitingOn),
            r.NextStepText,
            r.CreatedAt,
            r.SubmittedAt,
            r.StatusChangedAt,
            institution = r.Institution is null ? null : new { r.Institution.Id, r.Institution.NameAr, r.Institution.NameEn },
            r.InstitutionOtherName,
            institutionName = r.InstitutionDisplayName,
            fields = new
            {
                r.ApplicantFullName, r.ContractNumber, r.MonthlyInstallment, r.ArrearsDuration, r.PropertyCity,
                pathPreference = PreferenceKey(r.PathPreference), r.AffordableMonthly, r.SituationText,
            },
            consent = consent is null ? null : new { consent.TextVersion, consent.TextSnapshot, consent.RecipientName, recordedAt = consent.OtpVerifiedAt },
            consentText = new { version = ConsentTextVersion, text = ConsentText(recipient) },
            documents = docs.Select(d =>
            {
                var v = d.Versions.OrderByDescending(x => x.VersionNo).FirstOrDefault();
                return new
                {
                    d.Id, d.Kind, d.Name, d.AddedAfterSubmit, versionId = v?.Id, fileName = v?.FileName, uploadedAt = v?.UploadedAt,
                    sizeBytes = v?.SizeBytes, scan = v?.ScanStatus.ToString().ToLowerInvariant(),
                };
            }),
            timeline,
            duplicate = duplicate is null ? null : new { duplicate.Reference, status = RequestStatusInfo.Key(duplicate.Status) },
            r.DuplicateAcknowledged,
            missing = r.Status == RequestStatus.Draft ? RequestWorkflow.MissingFields(r) : [],
            outcome = r.Status is RequestStatus.Closed ? new { code = r.OutcomeCode, summary = r.OutcomeSummary } : null,
            notEligibleReason = r.Status == RequestStatus.NotEligible ? r.NotEligibleReason : null,
            canEdit = r.Status == RequestStatus.Draft,
            canAddInfo = r.Status is not RequestStatus.Draft && !RequestStatusInfo.IsTerminal(r.Status),
            canWithdraw = !RequestStatusInfo.IsTerminal(r.Status),
        };
    }

    public static string? PreferenceKey(RequestPathPreference? p) => p switch
    {
        RequestPathPreference.KeepHome => "keep_home",
        RequestPathPreference.Settlement => "settlement",
        RequestPathPreference.SellMyself => "sell_myself",
        RequestPathPreference.NotSure => "not_sure",
        _ => null,
    };

    private static RequestPathPreference? ParsePreference(string? key) => key switch
    {
        "keep_home" => RequestPathPreference.KeepHome,
        "settlement" => RequestPathPreference.Settlement,
        "sell_myself" => RequestPathPreference.SellMyself,
        "not_sure" => RequestPathPreference.NotSure,
        _ => null,
    };

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, max)];

    private static async Task<IResult> Patch(string reference, MyRequestPatch body, RequestAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var r = await access.ForApplicantAsync(reference, write: true);
        if (r.Status != RequestStatus.Draft)
            throw new ConflictException("locked", "أُرسل الطلب ولا يمكن تعديله. يمكنك إضافة معلومة أو مستند من صفحة الطلب.");

        var v = new Validator();
        switch (body.Step)
        {
            case "lender":
            {
                var other = Clean(body.InstitutionOtherName, 200);
                Guid? institutionId = null;
                if (body.InstitutionId is { } iid)
                {
                    v.Require(await db.FinancingInstitutions.AnyAsync(i => i.Id == iid && i.Active), "institutionId", "اختر جهة من القائمة.");
                    institutionId = iid;
                    other = null;
                }
                v.ThrowIfInvalid();
                if (institutionId != r.InstitutionId || other != r.InstitutionOtherName)
                {
                    // The recorded consent names a recipient: a different lender needs a fresh consent.
                    foreach (var c in await db.RequestConsents.Where(c => c.RequestId == r.Id && c.WithdrawnAt == null).ToListAsync())
                    {
                        c.WithdrawnAt = clock.UtcNow;
                        c.WithdrawnReason = "recipient_changed";
                    }
                    r.DuplicateAcknowledged = false;
                }
                r.InstitutionId = institutionId;
                r.InstitutionOtherName = other;
                break;
            }
            case "finance":
                v.Require(body.MonthlyInstallment is null or (>= 0 and <= 10_000_000), "monthlyInstallment", "أدخل مبلغاً صحيحاً بالريال.")
                 .Require(body.ArrearsDuration is null || ArrearsKeys.Contains(body.ArrearsDuration), "arrearsDuration", "اختر مدة التأخر.")
                 .ThrowIfInvalid();
                if (Clean(body.ContractNumber, 60) != r.ContractNumber) r.DuplicateAcknowledged = false;
                r.ApplicantFullName = Clean(body.ApplicantFullName, 200);
                r.ContractNumber = Clean(body.ContractNumber, 60);
                r.MonthlyInstallment = body.MonthlyInstallment;
                r.ArrearsDuration = body.ArrearsDuration;
                r.PropertyCity = Clean(body.PropertyCity, 100);
                break;
            case "situation":
                v.Require(body.PathPreference is null || ParsePreference(body.PathPreference) is not null, "pathPreference", "اختر ما يناسبك.")
                 .Require(body.AffordableMonthly is null or (>= 0 and <= 10_000_000), "affordableMonthly", "أدخل مبلغاً صحيحاً بالريال.")
                 .ThrowIfInvalid();
                r.PathPreference = ParsePreference(body.PathPreference);
                r.AffordableMonthly = body.AffordableMonthly;
                r.SituationText = Clean(body.SituationText, 1500);
                break;
            default:
                Validate.Throw("step", "خطوة غير معروفة.");
                break;
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { savedAt = clock.UtcNow, missing = RequestWorkflow.MissingFields(r) });
    }

    // ───────── consent (OA04, V4 provisional) ─────────

    private static async Task<IResult> ConsentOtp(string reference, RequestAccess access, RahoonDbContext db, RequestContext rc, OtpService otp, PiiProtector pii)
    {
        var r = await access.ForApplicantAsync(reference, write: true, track: false);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        if (string.IsNullOrWhiteSpace(r.InstitutionDisplayName)) Validate.Throw("institution", "اختر جهتك الممولة أولاً.");
        var profile = await db.IndividualProfiles.AsNoTracking().FirstAsync(p => p.UserId == rc.UserId);
        var issued = await otp.IssueAsync(OtpPurpose.Consent, pii.Unprotect(profile.PhoneEnc), rc.UserId, rc.SessionId, context: $"request-consent:{r.Id}");
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task<IResult> Consent(string reference, ConsentConfirm body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        OtpService otp, RequestService svc, RequestWorkflow workflow, IClock clock, AuditLog audit)
    {
        if (!body.Accept) Validate.Throw("accept", "للمتابعة، وافق على مشاركة البيانات اللازمة مع جهتك الممولة.");
        var r = await access.ForApplicantAsync(reference, write: true);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        if (string.IsNullOrWhiteSpace(r.InstitutionDisplayName)) Validate.Throw("institution", "اختر جهتك الممولة أولاً.");
        if (!await otp.VerifyAsync(OtpPurpose.Consent, rc.SessionId, body.Code ?? "", $"request-consent:{r.Id}"))
            throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. اطلب رمزاً جديداً.", StatusCodes.Status423Locked);

        await using var tx = await db.Database.BeginTransactionAsync();
        var now = clock.UtcNow;
        foreach (var old in await db.RequestConsents.Where(c => c.RequestId == r.Id && c.WithdrawnAt == null).ToListAsync())
        {
            old.WithdrawnAt = now;
            old.WithdrawnReason = "superseded";
        }
        var consent = new RequestConsent
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, TextVersion = ConsentTextVersion,
            TextSnapshot = ConsentText(r.InstitutionDisplayName), InstitutionId = r.InstitutionId, RecipientName = r.InstitutionDisplayName,
            DataCategories = [.. ConsentCategories], OtpVerifiedAt = now, IpMasked = rc.IpMasked,
        };
        db.RequestConsents.Add(consent);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.consent_recorded", $"موافقة موثقة على المشاركة مع {r.InstitutionDisplayName}",
            detail: $"{ConsentTextVersion} · مؤكدة برمز الجوال"));
        if (r.Status != RequestStatus.Draft)
        {
            svc.AddUpdate(r, "consent", "سجّلت موافقتك على المشاركة مع جهتك الممولة", authorKind: "applicant");
            // A consent withdrawal paused the request: renewing consent resumes it.
            if (r.Status == RequestStatus.InfoRequested && r.InfoRequestIsConsent)
            {
                r.InfoRequestIsConsent = false;
                await workflow.TransitionAsync(r, "info_provided");
            }
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { recordedAt = now, consent.TextVersion, consent.RecipientName });
    }

    private static async Task<IResult> WithdrawConsent(string reference, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var r = await access.ForApplicantAsync(reference, write: true);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var active = await db.RequestConsents.Where(c => c.RequestId == r.Id && c.WithdrawnAt == null).ToListAsync();
        if (active.Count == 0) throw new ConflictException("no_consent", "لا توجد موافقة سارية لسحبها.");
        var now = clock.UtcNow;
        foreach (var c in active) { c.WithdrawnAt = now; c.WithdrawnReason = "withdrawn_by_applicant"; }
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.consent_withdrawn", "سحب الموافقة على المشاركة مع الجهة الممولة"));
        if (r.Status is not RequestStatus.Draft)
        {
            // ADR §4.5 withdraw_consent: coordination stops; the request waits on the individual (effect wording V4).
            if (r.Status != RequestStatus.InfoRequested)
            {
                var from = r.Status;
                r.StatusBeforeInfoRequest = from;
                r.Status = RequestStatus.InfoRequested;
                r.StatusChangedAt = now;
                r.InfoRequestIsConsent = true;
                await audit.RecordAsync(RequestWorkflow.Entry(r, "request.transition", $"{RequestStatusInfo.LabelAr(from)} ← {RequestStatusInfo.LabelAr(r.Status)}",
                    RequestStatusInfo.Key(from), RequestStatusInfo.Key(r.Status), detail: "سحب الموافقة"));
            }
            r.WaitingOn = RequestWaitingOn.Applicant;
            r.NextStepText = "توقف التنسيق مع جهتك الممولة لأنك سحبت موافقتك. لمتابعة طلبك وافق من جديد، أو اسحب الطلب إن لم تعد بحاجة إليه.";
            svc.AddUpdate(r, "consent", "سحبت موافقتك على المشاركة، وتوقف التنسيق مع جهتك الممولة", authorKind: "applicant");
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { withdrawnAt = now });
    }

    // ───────── documents ─────────

    private static async Task<IResult> Upload(string reference, HttpRequest http, RequestAccess access, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IFileScanner scanner, RequestService svc, IClock clock, AuditLog audit)
    {
        if (!http.HasFormContentType) throw new DomainException("form_required", "أرسل الملف كنموذج متعدد الأجزاء.", 400);
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملفاً."] });
        var kind = form["kind"].ToString();
        if (!DocumentKinds.TryGetValue(kind, out var kindName)) Validate.Throw("kind", "نوع المستند غير معروف.");
        var r = await access.ForApplicantAsync(reference, write: true);
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق ولا يقبل مستندات جديدة.");

        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, r.OrganizationId);
        var scan = await scanner.ScanAsync(stored.StorageKey);
        if (scan == ScanStatus.Infected) throw new DomainException("file_infected", "رُفض الملف: فشل فحص الأمان.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var afterSubmit = r.Status != RequestStatus.Draft;
        // In a draft, a new file of the same kind replaces the previous one as a new version; after submission every file is an addition.
        var doc = !afterSubmit
            ? await db.RequestDocuments.FirstOrDefaultAsync(d => d.RequestId == r.Id && d.Kind == kind && d.Source == "applicant" && !d.AddedAfterSubmit)
            : null;
        if (doc is null)
        {
            doc = new RequestDocument
            {
                OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind, Name = kind == "other" ? Clean(form["name"], 120) ?? kindName! : kindName!,
                Source = "applicant", AddedAfterSubmit = afterSubmit,
            };
            db.RequestDocuments.Add(doc);
        }
        var version = new RequestDocumentVersion
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, DocumentId = doc.Id, VersionNo = doc.VersionCount + 1,
            FileName = Path.GetFileName(file.FileName), ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256,
            StorageKey = stored.StorageKey, UploadedByUserId = rc.UserId, UploadedByLabel = "العميل", UploadedAt = clock.UtcNow, ScanStatus = scan,
        };
        db.RequestDocumentVersions.Add(version);
        doc.VersionCount = version.VersionNo;
        doc.CurrentVersionId = version.Id;
        if (afterSubmit) svc.AddUpdate(r, "info_added", $"أضفت مستنداً: {doc.Name}", authorKind: "applicant");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.document_uploaded", $"رفع {doc.Name} v{version.VersionNo}",
            detail: $"{stored.ContentType} · {stored.SizeBytes / 1024} ك.ب · فحص: {scan}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { documentId = doc.Id, versionId = version.Id, version = version.VersionNo, scan = scan.ToString().ToLowerInvariant() });
    }

    private static async Task<IResult> Download(string reference, Guid versionId, RequestAccess access, RahoonDbContext db, IDocumentStorage storage, AuditLog audit)
    {
        var r = await access.ForApplicantAsync(reference, track: false);
        var v = await db.RequestDocumentVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId && x.RequestId == r.Id) ?? throw new NotFoundException();
        var doc = await db.RequestDocuments.AsNoTracking().FirstAsync(d => d.Id == v.DocumentId);
        if (doc.Visibility != RequestDocumentVisibility.ApplicantAndTeam) throw new NotFoundException();
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.document_downloaded", $"تنزيل {doc.Name} v{v.VersionNo}"));
        await db.SaveChangesAsync();
        return Results.File(await storage.OpenReadAsync(v.StorageKey), v.ContentType, v.FileName);
    }

    // ───────── submit, add info, withdraw ─────────

    private static async Task<IResult> Submit(string reference, SubmitMyRequest body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForApplicantAsync(reference, write: true);
        if (body.AcknowledgeDuplicate && r.Status == RequestStatus.Draft)
        {
            var dup = await RequestWorkflow.FindDuplicateAsync(db, r);
            r.DuplicateAcknowledged = dup is not null;
            r.DuplicateOfRequestId = dup?.Id;
        }
        await workflow.TransitionAsync(r, "submit", expected: RequestStatus.Draft);
        if (r.DuplicateOfRequestId is not null)
            await audit.RecordAsync(RequestWorkflow.Entry(r, "request.duplicate_acknowledged", "تأكيد العميل أن الطلب لتمويل مختلف رغم وجود طلب قائم لنفس الجهة"));

        // The person's name is collected with the request (Q5 interim); it replaces the masked-ID display label.
        var user = await db.Users.FirstAsync(u => u.Id == rc.UserId);
        var profile = await db.IndividualProfiles.AsNoTracking().FirstAsync(p => p.UserId == rc.UserId);
        if (!string.IsNullOrWhiteSpace(r.ApplicantFullName) && user.FullName == profile.NationalIdMasked) user.FullName = r.ApplicantFullName!;

        svc.AddUpdate(r, "submitted", "أرسلت طلبك إلى فريق رهون", authorKind: "applicant");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { r.Reference, status = RequestStatusInfo.Key(r.Status), r.SubmittedAt });
    }

    private static async Task<IResult> AddInfo(string reference, AddInfoRequest body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, AuditLog audit)
    {
        var text = Clean(body.Text, 2000);
        if (text is null) Validate.Throw("text", "اكتب المعلومة التي تريد إضافتها.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForApplicantAsync(reference, write: true);
        if (r.Status == RequestStatus.Draft) throw new ConflictException("draft", "الطلب لم يُرسل بعد؛ عدّل بياناته مباشرة.");
        if (RequestStatusInfo.IsTerminal(r.Status)) throw new ConflictException("closed", "الطلب مغلق.");
        svc.AddUpdate(r, "info_added", "أضفت معلومة إلى طلبك", text, authorKind: "applicant");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.info_added", "إضافة معلومة من العميل"));
        // Answering a team information request returns the request to where it was (not when paused by a consent withdrawal).
        if (r.Status == RequestStatus.InfoRequested && !r.InfoRequestIsConsent) await workflow.TransitionAsync(r, "info_provided");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    private static async Task<IResult> Withdraw(string reference, WithdrawMyRequest body, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, IClock clock)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForApplicantAsync(reference, write: true);
        await workflow.TransitionAsync(r, "withdraw", Clean(body.Reason, 500));
        foreach (var c in await db.RequestConsents.Where(c => c.RequestId == r.Id && c.WithdrawnAt == null).ToListAsync())
        {
            c.WithdrawnAt = clock.UtcNow;
            c.WithdrawnReason = "request_withdrawn";
        }
        svc.AddUpdate(r, "withdrawn", "سحبت طلبك، وتوقفت أي مشاركة لبياناته", authorKind: "applicant");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }
}
