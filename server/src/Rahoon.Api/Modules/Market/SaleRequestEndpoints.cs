using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Market;

public sealed record ObligationInput(Guid? Id, string? Kind, Guid? PartyId, string? PartyOtherName, string? RelationNote, Dictionary<string, string?>? Answers);
public sealed record LocationInput(double? Lat, double? Lng, string? Label, string? DisplayWish);
public sealed record SaleRequestSave(
    Guid? ClientDraftId, string? PropertyType, string? City, string? District, string? Project, string? ObligationMode,
    Dictionary<string, string?>? Answers, List<ObligationInput>? Obligations, LocationInput? Location, string? ContactName, string? ContactEmail);
public sealed record SaleRequestSubmit(string? ContactName, string? Relationship, bool AcceptDeclarations, bool AcceptProcessing, string? ContactEmail);
public sealed record ReasonInput(string? Reason);
public sealed record NoteInput(string? Note);
public sealed record PhotoOrderInput(List<Guid>? Ids, Guid? CoverId);
public sealed record OwnerTermsDecision(Guid TermsId, string? Note);

/// <summary>
/// The owner's sale request (docs/product/product-definition.md §4): a first request in three steps, then one follow-up file
/// completed gradually on the same request — never a second request. Only the owner reads and writes it (applicant filter);
/// every change after submission is logged for the team.
/// </summary>
public static class SaleRequestEndpoints
{
    public const string DeclarationsVersion = "sale-declarations-2026-10";
    private static readonly string[] PhotoTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long PhotoMaxBytes = 10 * 1024 * 1024;
    private const int MaxPhotos = 30;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/market/sale-requests").RequireIndividual();
        g.MapPost("", Create).Idempotent();
        g.MapGet("/{reference}", Get);
        g.MapPut("/{reference}", Save);
        g.MapPost("/{reference}/submit", Submit).Idempotent();
        g.MapPost("/{reference}/resubmit", Resubmit).Idempotent();
        g.MapPost("/{reference}/mark-complete", MarkComplete).Idempotent();
        g.MapPost("/{reference}/withdraw", Withdraw).Idempotent();
        g.MapPost("/{reference}/documents", UploadDocument).DisableAntiforgery();
        g.MapPost("/{reference}/documents/{id:guid}/remove", RemoveDocument).Idempotent();
        g.MapPost("/{reference}/photos", UploadPhoto).DisableAntiforgery();
        g.MapPost("/{reference}/photos/order", OrderPhotos).Idempotent();
        g.MapPost("/{reference}/photos/{id:guid}/remove", RemovePhoto).Idempotent();
        g.MapGet("/{reference}/opportunity", OwnerOpportunity);
        g.MapPost("/{reference}/opportunity/confirm", ConfirmTerms).Idempotent();
        g.MapPost("/{reference}/opportunity/request-changes", RequestChanges).Idempotent();

        // Private files: the owner, or the Rahoon team with market.view. Never public.
        app.MapGet("/api/market/sale-requests/{reference}/documents/{id:guid}/file", DocumentFile).RequireSession();
        app.MapGet("/api/market/sale-requests/{reference}/photos/{id:guid}/file", PhotoFile).RequireSession();
    }

    internal static async Task<SaleRequestFile> LoadOwnAsync(RahoonDbContext db, RequestContext rc, string reference) =>
        await SaleRequestFile.LoadAsync(db, reference) is { } f && f.Request.ApplicantUserId == rc.UserId ? f : throw new NotFoundException();

    // ── Owner projection ──

    internal static object OwnerDto(SaleRequestFile f, CommissionPolicy policy)
    {
        var r = f.Request;
        var estimate = MarketCalculator.Compute(f.PreliminaryInput(), policy);
        var opp = f.Opportunities.LastOrDefault(o => o.Status != OpportunityStatus.Withdrawn);
        return new
        {
            reference = r.Reference, status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status], nextStep = SaleRequestFlow.NextStep(r.Status),
            editable = SaleRequestFlow.OwnerCanEdit(r.Status), canWithdraw = SaleRequestFlow.OwnerCanWithdraw(r.Status),
            filesEditable = SaleRequestFlow.OwnerCanEdit(r.Status) || r.Status == SaleRequestStatus.ApprovedForListing,
            r.CreatedAt, r.UpdatedAt, r.SubmittedAt, isDemo = r.IsDemo,
            r.PropertyType, r.City, r.District, r.Project, r.ObligationMode, answers = r.Answers,
            obligations = f.Obligations.Select(f.ObligationDto),
            location = new { lat = r.Latitude, lng = r.Longitude, label = r.LocationLabel, displayWish = r.LocationDisplayWish },
            contact = new { name = r.ContactName, email = r.ContactEmail, relationship = r.RelationshipDeclared },
            completeness = f.Completeness(),
            openCompletion = f.OpenCompletion is { } oc ? f.CompletionDto(oc) : null,
            documents = f.Documents.Select(f.DocumentDto),
            photos = f.Photos.Select(f.PhotoDto),
            events = f.Events.Where(e => e.VisibleToApplicant).OrderByDescending(e => e.At).Select(SaleRequestFile.EventDto),
            estimate,
            opportunity = opp is null ? null : new
            {
                opp.Reference, status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status],
                awaitingYou = opp.Status == OpportunityStatus.AwaitingOwnerConfirmation,
            },
        };
    }

    // ── Create / save ──

    private static async Task<IResult> Create(SaleRequestSave req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock,
        IConfiguration config)
    {
        if (req.ClientDraftId is not { } draftId || draftId == Guid.Empty) Validate.Throw("clientDraftId", "معرّف المسودة مطلوب.");
        // A retry or a second tab with the same device draft returns the same request.
        var existing = await db.SaleRequests.FirstOrDefaultAsync(r => r.ApplicantUserId == rc.UserId && r.ClientDraftId == req.ClientDraftId);
        if (existing is null)
        {
            var org = await market.OperatorOrgIdAsync();
            existing = new SaleRequest
            {
                OrganizationId = org, ApplicantUserId = rc.UserId, ClientDraftId = req.ClientDraftId, Reference = await market.NextReferenceAsync("SR"),
                StatusChangedAt = clock.UtcNow,
            };
            db.SaleRequests.Add(existing);
            market.Event(org, "sale_request", existing.Id, rc.UserId, "created", "بدأت طلب بيع", visible: false);
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                existing = await db.SaleRequests.FirstAsync(r => r.ApplicantUserId == rc.UserId && r.ClientDraftId == req.ClientDraftId);
            }
        }
        var f = await LoadOwnAsync(db, rc, existing.Reference);
        var invalid = await ApplySaveAsync(db, f, req, market, clock);
        await db.SaveChangesAsync();
        f = await LoadOwnAsync(db, rc, existing.Reference);
        return Results.Ok(new { saved = true, invalid, file = OwnerDto(f, CommissionPolicy.From(config)) });
    }

    private static async Task<IResult> Save(string reference, SaleRequestSave req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock,
        IConfiguration config)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        if (!SaleRequestFlow.OwnerCanEdit(f.Request.Status))
            throw new ConflictException("locked", $"لا يمكن تعديل الطلب وهو في حالة «{SaleRequestFlow.Labels[f.Request.Status]}». تواصل مع فريق رهون لأي تصحيح.");
        var invalid = await ApplySaveAsync(db, f, req, market, clock);
        await db.SaveChangesAsync();
        f = await LoadOwnAsync(db, rc, reference);
        return Results.Ok(new { saved = true, invalid, file = OwnerDto(f, CommissionPolicy.From(config)) });
    }

    /// <summary>
    /// Applies the sections present in the body. The catalog drops whatever doesn't apply to the chosen type, party or
    /// conditions; invalid values are returned in <c>invalid</c> and not kept, valid ones are kept.
    /// </summary>
    internal static async Task<Dictionary<string, string>> ApplySaveAsync(RahoonDbContext db, SaleRequestFile f, SaleRequestSave req, MarketService market, IClock clock)
    {
        var r = f.Request;
        var invalid = new Dictionary<string, string>();
        var now = clock.UtcNow;
        var changed = new List<string>();

        if (req.PropertyType is not null)
        {
            if (FieldCatalog.PropertyTypes.Any(p => p.Value == req.PropertyType)) { if (r.PropertyType != req.PropertyType) changed.Add("property_type"); r.PropertyType = req.PropertyType; }
            else invalid["propertyType"] = "اختر نوع العقار من القائمة.";
        }
        if (req.City is not null)
        {
            if (FieldCatalog.City(req.City) is not null) { if (r.City != req.City) changed.Add("city"); r.City = req.City; }
            else invalid["city"] = "اختر المدينة من القائمة.";
        }
        if (req.District is not null)
        {
            var d = req.District.Trim();
            if (d.Length > 100) invalid["district"] = "اسم الحي أطول من المسموح.";
            else { if (r.District != d) changed.Add("district"); r.District = d.Length == 0 ? null : d; }
        }
        if (req.Project is not null)
        {
            var p = req.Project.Trim();
            if (p.Length > 150) invalid["project"] = "اسم المشروع أطول من المسموح.";
            else r.Project = p.Length == 0 ? null : p;
        }
        if (req.ContactName is not null) r.ContactName = PhoneAuthEndpoints.CleanName(req.ContactName) ?? r.ContactName;
        if (req.ContactEmail is not null)
        {
            var e = req.ContactEmail.Trim();
            if (e.Length == 0) r.ContactEmail = null;
            else if (e.Length > 200 || !System.Text.RegularExpressions.Regex.IsMatch(e, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) invalid["contactEmail"] = "أدخل بريدًا إلكترونيًا صحيحًا أو اتركه فارغًا.";
            else r.ContactEmail = e;
        }

        // Property answers: re-applied whenever answers or the type change, so another type's fields are dropped.
        if (req.Answers is not null || req.PropertyType is not null)
        {
            var source = req.Answers ?? r.Answers.ToDictionary(kv => kv.Key, kv => (string?)kv.Value);
            var applied = FieldCatalog.Apply("property", source, r.PropertyType, null);
            foreach (var (k, v) in applied.Errors) invalid[k] = v;
            if (!DictEqual(r.Answers, applied.Answers)) changed.Add("answers");
            r.Answers = applied.Answers;
        }

        if (req.Location is { } loc)
        {
            if (loc.Lat is null || loc.Lng is null) { r.Latitude = null; r.Longitude = null; }
            else if (loc.Lat is < 16 or > 32.5 || loc.Lng is < 34.5 or > 55.7) invalid["location"] = "الموقع خارج المملكة العربية السعودية. حرّك العلامة إلى موقع العقار.";
            else
            {
                if (r.Latitude != loc.Lat || r.Longitude != loc.Lng) changed.Add("location");
                r.Latitude = Math.Round(loc.Lat.Value, 6);
                r.Longitude = Math.Round(loc.Lng.Value, 6);
            }
            if (loc.Label is not null) r.LocationLabel = loc.Label.Trim() is { Length: > 0 and <= 200 } lbl ? lbl : null;
            if (loc.DisplayWish is "exact" or "approximate") r.LocationDisplayWish = loc.DisplayWish;
        }

        if (req.ObligationMode is not null)
        {
            if (FieldCatalog.ObligationModes.All(m => m.Value != req.ObligationMode)) invalid["obligationMode"] = "اختر جهة الالتزام.";
            else { if (r.ObligationMode != req.ObligationMode) changed.Add("obligation_mode"); r.ObligationMode = req.ObligationMode; }
        }
        if (req.Obligations is not null || req.ObligationMode is not null)
            await SyncObligationsAsync(db, f, req.Obligations, invalid, changed, now);

        if (r.Status is SaleRequestStatus.UnderReview or SaleRequestStatus.Submitted or SaleRequestStatus.NeedsCompletion && changed.Count > 0)
            market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "owner_edit", "عدّل صاحب الطلب بيانات في ملفه", visible: false,
                data: new { changed = changed.Distinct() });
        return invalid;
    }

    private static bool DictEqual(Dictionary<string, string> a, Dictionary<string, string> b) => a.Count == b.Count && !a.Except(b).Any();

    private static async Task SyncObligationsAsync(RahoonDbContext db, SaleRequestFile f, List<ObligationInput>? incoming, Dictionary<string, string> invalid,
        List<string> changed, DateTimeOffset now)
    {
        var r = f.Request;
        var list = incoming ?? f.Obligations.Select(o => new ObligationInput(o.Id, o.Kind, o.PartyId, o.PartyOtherName, o.RelationNote,
            o.Answers.ToDictionary(kv => kv.Key, kv => (string?)kv.Value))).ToList();
        // The mode decides the shape: one developer, one financier, or 2–4 obligations of either kind.
        switch (r.ObligationMode)
        {
            case "developer": list = [.. list.Take(1).Select(o => o with { Kind = "developer" })]; if (list.Count == 0) list.Add(new(null, "developer", null, null, null, null)); break;
            case "financier": list = [.. list.Take(1).Select(o => o with { Kind = "financier" })]; if (list.Count == 0) list.Add(new(null, "financier", null, null, null, null)); break;
            case "multiple":
                list = [.. list.Take(4)];
                while (list.Count < 2) list.Add(new(null, list.Count == 0 ? "developer" : "financier", null, null, null, null));
                break;
            default: return;
        }

        var parties = await db.ObligationParties.Where(p => p.Active).ToListAsync();
        var keep = new HashSet<Guid>();
        for (var i = 0; i < list.Count; i++)
        {
            var input = list[i];
            var kind = input.Kind is "developer" or "financier" ? input.Kind : "developer";
            var o = input.Id is { } id ? f.Obligations.FirstOrDefault(x => x.Id == id) : null;
            if (o is null)
            {
                o = new SaleObligation { OrganizationId = r.OrganizationId, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind };
                db.SaleObligations.Add(o);
                f.Obligations.Add(o);
                changed.Add("obligations");
            }
            if (o.Kind != kind) changed.Add("obligation_kind");
            o.Kind = kind;
            o.SortOrder = i;
            if (input.PartyId is { } pid)
            {
                var party = parties.FirstOrDefault(p => p.Id == pid && p.Kind == kind);
                if (party is null) invalid[$"o{i}.party"] = "اختر الجهة من القائمة أو «غير موجودة بالقائمة».";
                else { o.PartyId = pid; o.Party = party; o.PartyOtherName = null; }
            }
            else if (input.PartyOtherName is not null)
            {
                var other = input.PartyOtherName.Trim();
                if (other.Length > 200) invalid[$"o{i}.party"] = "اسم الجهة أطول من المسموح.";
                else { o.PartyId = null; o.Party = null; o.PartyOtherName = other.Length == 0 ? null : other; }
            }
            // A party of the other kind doesn't survive a kind switch.
            if (o.Party is { } cur && cur.Kind != kind) { o.PartyId = null; o.Party = null; }
            if (input.RelationNote is not null) o.RelationNote = input.RelationNote.Trim() is { Length: > 0 and <= 500 } note ? note : null;

            var source = input.Answers ?? o.Answers.ToDictionary(kv => kv.Key, kv => (string?)kv.Value);
            var applied = FieldCatalog.Apply("obligation", source, null, kind, $"o{i}.");
            foreach (var (k, v) in applied.Errors) invalid[k] = v;
            foreach (var (k, v) in FieldCatalog.CrossCheck(kind, applied.Answers, $"o{i}.")) invalid[k] = v;
            // An edited figure that the team had verified with another value is no longer verified.
            foreach (var v in f.Verifications.Where(v => v.FieldKey.StartsWith($"o:{o.Id}:") && v.SupersededAt is null))
            {
                var key = v.FieldKey.Split(':')[2];
                if (applied.Answers.TryGetValue(key, out var nv) && nv != v.Value) v.SupersededAt = now;
            }
            if (!DictEqual(o.Answers, applied.Answers)) changed.Add("figures");
            o.Answers = applied.Answers;
            keep.Add(o.Id);
        }
        foreach (var o in f.Obligations.Where(o => !keep.Contains(o.Id) && o.RemovedAt is null).ToList())
        {
            o.RemovedAt = now;
            f.Obligations.Remove(o);
            changed.Add("obligations");
        }
    }

    private static async Task<IResult> Get(string reference, RahoonDbContext db, RequestContext rc, IConfiguration config) =>
        Results.Ok(OwnerDto(await LoadOwnAsync(db, rc, reference), CommissionPolicy.From(config)));

    // ── Submit and follow-up ──

    private static async Task<IResult> Submit(string reference, SaleRequestSubmit req, RahoonDbContext db, RequestContext rc, MarketService market,
        IClock clock, IConfiguration config)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        var r = f.Request;
        if (r.Status != SaleRequestStatus.Draft)
            // Already sent (double click, second tab): the same request, never a second one.
            return Results.Ok(new { r.Reference, status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status], nextStep = SaleRequestFlow.NextStep(r.Status), alreadySubmitted = true });

        var v = new Validator();
        v.Require(r.PropertyType is not null, "propertyType", "اختر نوع العقار.")
         .Require(r.City is not null, "city", "اختر المدينة.")
         .Require(!string.IsNullOrWhiteSpace(r.District), "district", "اكتب الحي أو اختره.")
         .Require(r.ObligationMode is not null && f.Obligations.Count > 0, "obligationMode", "اختر جهة الالتزام.");
        for (var i = 0; i < f.Obligations.Count; i++)
        {
            var o = f.Obligations[i];
            v.Require(o.PartyId is not null || !string.IsNullOrWhiteSpace(o.PartyOtherName), $"o{i}.party",
                o.Kind == "developer" ? "اختر المطور أو اكتب اسمه إن لم يكن في القائمة." : "اختر جهة التمويل أو اكتب اسمها إن لم تكن في القائمة.");
            foreach (var fd in FieldCatalog.ObligationFields(o.Kind, o.Answers).Where(x => x.SubmitAnswer && !FieldCatalog.IsAnswered(o.Answers, x.Key)))
                v.Require(false, $"o{i}.{fd.Key}", fd.AllowUnknown ? $"أدخل «{fd.Label}» أو اختر «لا أعرف»." : $"اختر إجابة لـ«{fd.Label}».");
        }
        var name = PhoneAuthEndpoints.CleanName(req.ContactName ?? r.ContactName);
        v.Require(name is not null, "contactName", "اكتب اسمك.")
         .Require(req.Relationship is "owner" or "authorized", "relationship", "حدد علاقتك بالعقار.")
         .Require(req.AcceptDeclarations, "acceptDeclarations", "للإرسال، أقرّ بأنك صاحب العلاقة بالعقار أو مخوّل بذلك.")
         .Require(req.AcceptProcessing, "acceptProcessing", "للإرسال، وافق على معالجة طلبك والتواصل معك بشأنه.");
        if (req.ContactEmail is { Length: > 0 } em && !System.Text.RegularExpressions.Regex.IsMatch(em.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            v.Require(false, "contactEmail", "أدخل بريدًا إلكترونيًا صحيحًا أو اتركه فارغًا.");
        v.ThrowIfInvalid();

        var now = clock.UtcNow;
        r.ContactName = name;
        if (req.ContactEmail is { Length: > 0 } email) r.ContactEmail = email.Trim();
        r.RelationshipDeclared = req.Relationship;
        r.DeclarationsAcceptedAt = now;
        r.DeclarationsVersion = DeclarationsVersion;
        r.Status = SaleRequestStatus.Submitted;
        r.StatusChangedAt = now;
        r.SubmittedAt = now;
        await RenamePlaceholderAsync(db, rc, name!);
        var e = market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "submitted", "استلمنا طلبك", visible: true,
            body: SaleRequestFlow.NextStep(SaleRequestStatus.Submitted), from: nameof(SaleRequestStatus.Draft), to: nameof(SaleRequestStatus.Submitted));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(r.ApplicantUserId, e.Id, $"رهون: استلمنا طلب البيع {r.Reference}. تابع حالته واستكمل ملفك من حسابك.");
        return Results.Ok(new { r.Reference, status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status], nextStep = SaleRequestFlow.NextStep(r.Status), alreadySubmitted = false });
    }

    internal static async Task RenamePlaceholderAsync(RahoonDbContext db, RequestContext rc, string name)
    {
        using var _ = rc.BeginSystemScope();
        var user = await db.Users.FirstAsync(u => u.Id == rc.UserId);
        if (user.FullName == "عميل رهون" || user.FullName.Contains('•')) user.FullName = name;
    }

    private static async Task<IResult> Resubmit(string reference, NoteInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        var r = f.Request;
        SaleRequestFlow.Ensure(r.Status, SaleRequestStatus.NeedsCompletion);
        var now = clock.UtcNow;
        if (f.OpenCompletion is { } oc) oc.AnsweredAt = now;
        r.Status = SaleRequestStatus.UnderReview;
        r.StatusChangedAt = now;
        market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "resubmitted", "أرسلت ملفك للمراجعة بعد الاستكمال", visible: true,
            body: req.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 1000)] : null,
            from: nameof(SaleRequestStatus.NeedsCompletion), to: nameof(SaleRequestStatus.UnderReview));
        await db.SaveChangesAsync();
        return Results.Ok(new { r.Reference, status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status], nextStep = SaleRequestFlow.NextStep(r.Status) });
    }

    private static async Task<IResult> MarkComplete(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        var r = f.Request;
        SaleRequestFlow.Ensure(r.Status, SaleRequestStatus.Submitted, SaleRequestStatus.UnderReview);
        r.OwnerMarkedCompleteAt = clock.UtcNow;
        market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "owner_complete", "أبلغت الفريق باستكمال ملفك", visible: true);
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> Withdraw(string reference, ReasonInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        var r = f.Request;
        if (!SaleRequestFlow.OwnerCanWithdraw(r.Status))
            throw new ConflictException("invalid_status", "لا يمكن سحب الطلب في حالته الحالية. تواصل مع فريق رهون.");
        var from = r.Status;
        r.Status = SaleRequestStatus.Withdrawn;
        r.StatusChangedAt = clock.UtcNow;
        r.WithdrawReason = req.Reason?.Trim() is { Length: > 0 } reason ? reason[..Math.Min(reason.Length, 500)] : null;
        market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "withdrawn", "سحبت الطلب", visible: true, reason: r.WithdrawReason,
            from: from.ToString(), to: nameof(SaleRequestStatus.Withdrawn));
        await db.SaveChangesAsync();
        return Results.Ok(new { r.Reference, status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status] });
    }

    // ── Files ──

    private static bool FilesEditable(SaleRequestStatus s) => SaleRequestFlow.OwnerCanEdit(s) || s == SaleRequestStatus.ApprovedForListing;

    private static async Task<IResult> UploadDocument(string reference, HttpRequest http, RahoonDbContext db, RequestContext rc, IDocumentStorage storage,
        IFileScanner scanner, MarketService market)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        if (!FilesEditable(f.Request.Status)) throw new ConflictException("locked", "لا يمكن إضافة مستندات للطلب في حالته الحالية.");
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملفًا."] });
        var kind = form["kind"].ToString();
        var allowedKinds = FieldCatalog.DocumentsFor(f.ActiveKinds).Select(d => d.Key).Append("other").ToHashSet();
        if (!allowedKinds.Contains(kind)) Validate.Throw("kind", "اختر نوع المستند من القائمة.");
        Guid? obligationId = Guid.TryParse(form["obligationId"].ToString(), out var oid) && f.Obligations.Any(o => o.Id == oid) ? oid : null;

        var doc = await StoreDocumentAsync(db, storage, scanner, f.Request, file, kind, obligationId, "applicant", rc.UserId);
        market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "document_added",
            $"أُضيف مستند: {FieldCatalog.Documents.FirstOrDefault(d => d.Key == kind)?.Label ?? kind}", visible: true);
        await db.SaveChangesAsync();
        return Results.Ok(f.DocumentDto(doc));
    }

    internal static async Task<PrivateDocument> StoreDocumentAsync(RahoonDbContext db, IDocumentStorage storage, IFileScanner scanner, SaleRequest r,
        IFormFile file, string kind, Guid? obligationId, string source, Guid uploadedBy)
    {
        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, r.OrganizationId, area: "market-docs");
        if (await scanner.ScanAsync(stored.StorageKey) == ScanStatus.Infected)
            throw new DomainException("file_infected", "رُفض الملف لأن الفحص كشف محتوى ضارًا.");
        var doc = new PrivateDocument
        {
            OrganizationId = r.OrganizationId, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, ObligationId = obligationId, Kind = kind,
            FileName = SafeName(file.FileName), ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256,
            StorageKey = stored.StorageKey, Source = source, UploadedByUserId = uploadedBy,
        };
        db.PrivateDocuments.Add(doc);
        return doc;
    }

    internal static string SafeName(string name)
    {
        var n = Path.GetFileName(name ?? "file");
        n = new string(n.Where(c => !char.IsControl(c) && c is not ('/' or '\\' or '"')).ToArray());
        return n.Length == 0 ? "file" : n.Length > 200 ? n[^200..] : n;
    }

    private static async Task<IResult> RemoveDocument(string reference, Guid id, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        if (!FilesEditable(f.Request.Status)) throw new ConflictException("locked", "لا يمكن تعديل مستندات الطلب في حالته الحالية.");
        var doc = f.Documents.FirstOrDefault(d => d.Id == id) ?? throw new NotFoundException();
        if (doc.ReviewStatus == FileReviewStatus.Accepted) throw new ConflictException("accepted", "قَبِل الفريق هذا المستند. تواصل مع الفريق لاستبداله.");
        doc.RemovedAt = clock.UtcNow;
        market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "document_removed", $"أُزيل مستند: {doc.FileName}", visible: true);
        await db.SaveChangesAsync();
        return Results.Ok(new { removed = true });
    }

    private static async Task<IResult> UploadPhoto(string reference, HttpRequest http, RahoonDbContext db, RequestContext rc, IDocumentStorage storage,
        IFileScanner scanner, MarketService market)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        if (!FilesEditable(f.Request.Status)) throw new ConflictException("locked", "لا يمكن إضافة صور للطلب في حالته الحالية.");
        if (f.Photos.Count >= MaxPhotos) throw new ConflictException("too_many", $"الحد الأقصى {MaxPhotos} صورة.");
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر صورة."] });
        var photo = await StorePhotoAsync(db, storage, scanner, f, file, rc.UserId);
        await db.SaveChangesAsync();
        return Results.Ok(f.PhotoDto(photo));
    }

    internal static async Task<ListingPhoto> StorePhotoAsync(RahoonDbContext db, IDocumentStorage storage, IFileScanner scanner, SaleRequestFile f, IFormFile file, Guid uploadedBy)
    {
        if (file.Length > PhotoMaxBytes) throw new DomainException("file_too_large", $"حجم الصورة يتجاوز {PhotoMaxBytes / (1024 * 1024)} م.ب.");
        byte[] bytes;
        await using (var input = file.OpenReadStream())
        using (var ms = new MemoryStream())
        {
            await input.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        // Embedded metadata (EXIF GPS on phone photos) would reveal the exact location: removed before storing.
        bytes = ImageMetadata.Strip(bytes, ImageMetadata.DetectType(bytes.AsSpan(0, Math.Min(12, bytes.Length))));
        await using var stream = new MemoryStream(bytes);
        // Listing photos live in their own storage area, apart from private documents.
        var stored = await storage.SaveAsync(stream, file.FileName, f.Request.OrganizationId, area: "market-photos", allowedTypes: PhotoTypes, maxBytes: PhotoMaxBytes);
        if (await scanner.ScanAsync(stored.StorageKey) == ScanStatus.Infected)
            throw new DomainException("file_infected", "رُفض الملف لأن الفحص كشف محتوى ضارًا.");
        var photo = new ListingPhoto
        {
            OrganizationId = f.Request.OrganizationId, SaleRequestId = f.Request.Id, ApplicantUserId = f.Request.ApplicantUserId,
            FileName = SafeName(file.FileName), ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey,
            SortOrder = f.Photos.Count == 0 ? 0 : f.Photos.Max(p => p.SortOrder) + 1, IsCover = f.Photos.Count == 0, UploadedByUserId = uploadedBy,
        };
        db.ListingPhotos.Add(photo);
        f.Photos.Add(photo);
        return photo;
    }

    private static async Task<IResult> OrderPhotos(string reference, PhotoOrderInput req, RahoonDbContext db, RequestContext rc)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        if (!FilesEditable(f.Request.Status)) throw new ConflictException("locked", "لا يمكن تعديل الصور في حالة الطلب الحالية.");
        ApplyPhotoOrder(f, req);
        await db.SaveChangesAsync();
        return Results.Ok(new { photos = f.Photos.OrderBy(p => p.SortOrder).Select(f.PhotoDto) });
    }

    internal static void ApplyPhotoOrder(SaleRequestFile f, PhotoOrderInput req)
    {
        var order = (req.Ids ?? []).Where(id => f.Photos.Any(p => p.Id == id)).Distinct().ToList();
        var rest = f.Photos.Where(p => !order.Contains(p.Id)).OrderBy(p => p.SortOrder).Select(p => p.Id);
        var i = 0;
        foreach (var id in order.Concat(rest)) f.Photos.First(p => p.Id == id).SortOrder = i++;
        if (req.CoverId is { } cover)
        {
            if (f.Photos.All(p => p.Id != cover)) Validate.Throw("coverId", "اختر صورة الغلاف من صورك.");
            foreach (var p in f.Photos) p.IsCover = p.Id == cover;
        }
    }

    private static async Task<IResult> RemovePhoto(string reference, Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        if (!FilesEditable(f.Request.Status)) throw new ConflictException("locked", "لا يمكن تعديل الصور في حالة الطلب الحالية.");
        var photo = f.Photos.FirstOrDefault(p => p.Id == id) ?? throw new NotFoundException();
        if (f.Opportunities.Any(o => o.Status is OpportunityStatus.Published or OpportunityStatus.Paused && o.PhotoIds.Contains(id)))
            throw new ConflictException("in_use", "هذه الصورة معروضة في فرصة منشورة. تواصل مع الفريق لاستبدالها.");
        photo.RemovedAt = clock.UtcNow;
        f.Photos.Remove(photo);
        if (photo.IsCover && f.Photos.Count > 0) f.Photos.OrderBy(p => p.SortOrder).First().IsCover = true;
        await db.SaveChangesAsync();
        return Results.Ok(new { removed = true });
    }

    /// <summary>The owner (own rows through the applicant filter) or a team member with market.view. Same 404 otherwise.</summary>
    private static async Task<SaleRequestFile> LoadForFileAccessAsync(RahoonDbContext db, RequestContext rc, string reference)
    {
        if (!(rc.IsIndividual || (rc.IsOperator && rc.Has(P.MarketView)))) throw new NotFoundException();
        var f = await SaleRequestFile.LoadAsync(db, reference) ?? throw new NotFoundException();
        if (rc.IsIndividual && f.Request.ApplicantUserId != rc.UserId) throw new NotFoundException();
        return f;
    }

    private static async Task<IResult> DocumentFile(string reference, Guid id, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, HttpContext http)
    {
        var f = await LoadForFileAccessAsync(db, rc, reference);
        var doc = await db.PrivateDocuments.FirstOrDefaultAsync(d => d.Id == id && d.SaleRequestId == f.Request.Id) ?? throw new NotFoundException();
        var stream = await storage.OpenReadAsync(doc.StorageKey);
        http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(doc.FileName)}";
        return Results.Stream(stream, doc.ContentType);
    }

    private static async Task<IResult> PhotoFile(string reference, Guid id, RahoonDbContext db, RequestContext rc, IDocumentStorage storage)
    {
        var f = await LoadForFileAccessAsync(db, rc, reference);
        var photo = await db.ListingPhotos.FirstOrDefaultAsync(p => p.Id == id && p.SaleRequestId == f.Request.Id) ?? throw new NotFoundException();
        return Results.Stream(await storage.OpenReadAsync(photo.StorageKey), photo.ContentType);
    }

    // ── The prepared opportunity: the owner reviews and confirms (or asks for changes); never publishes ──

    private static async Task<IResult> OwnerOpportunity(string reference, RahoonDbContext db, RequestContext rc)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        var opp = f.Opportunities.LastOrDefault(o => o.Status != OpportunityStatus.Withdrawn) ?? throw new NotFoundException();
        var terms = await db.OpportunityTerms.Where(t => t.OpportunityId == opp.Id).OrderByDescending(t => t.VersionNo).ToListAsync();
        var pending = terms.FirstOrDefault(t => t.Status == TermsStatus.SentToOwner);
        var shown = pending ?? terms.FirstOrDefault(t => t.Id == opp.PublishedTermsId) ?? terms.FirstOrDefault(t => t.Status == TermsStatus.OwnerConfirmed);
        return Results.Ok(new
        {
            opp.Reference, status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status],
            content = OpportunityProjection.Content(opp),
            photos = opp.PhotoIds.Select(pid => new { id = pid, url = $"/api/market/sale-requests/{f.Request.Reference}/photos/{pid}/file" }),
            location = new { precision = opp.LocationPrecision, lat = opp.PublicLatitude, lng = opp.PublicLongitude },
            terms = shown is null ? null : OpportunityProjection.Terms(shown),
            awaitingConfirmation = pending is not null && opp.DraftTermsId == pending.Id,
            publicUrl = opp.Status is OpportunityStatus.Published ? $"/opportunities/{opp.Reference}" : null,
            history = terms.Select(t => new { t.Id, t.VersionNo, status = t.Status, t.SentToOwnerAt, t.OwnerDecidedAt, t.OwnerNote }),
        });
    }

    private static async Task<(SaleRequestFile F, Opportunity Opp, OpportunityTerms Terms)> PendingTermsAsync(RahoonDbContext db, RequestContext rc, string reference, Guid termsId)
    {
        var f = await LoadOwnAsync(db, rc, reference);
        var opp = f.Opportunities.LastOrDefault(o => o.Status != OpportunityStatus.Withdrawn) ?? throw new NotFoundException();
        var terms = await db.OpportunityTerms.FirstOrDefaultAsync(t => t.Id == termsId && t.OpportunityId == opp.Id) ?? throw new NotFoundException();
        if (terms.Status != TermsStatus.SentToOwner || opp.DraftTermsId != terms.Id)
            throw new ConflictException("stale_terms", "هذا الملخص لم يعد بانتظار تأكيدك. حدّث الصفحة لرؤية آخر نسخة.");
        return (f, opp, terms);
    }

    private static async Task<IResult> ConfirmTerms(string reference, OwnerTermsDecision req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var (f, opp, terms) = await PendingTermsAsync(db, rc, reference, req.TermsId);
        var now = clock.UtcNow;
        terms.Status = TermsStatus.OwnerConfirmed;
        terms.OwnerDecidedAt = now;
        terms.OwnerConfirmationText = $"أكد صاحب العقار ملخص الفرصة {opp.Reference} (الإصدار {terms.VersionNo}) بما فيه الأرقام وشروط النقل ودقة الموقع والصور.";
        var from = opp.Status;
        // First publication waits in «جاهزة للنشر»; a published opportunity keeps showing its published version until the team republishes.
        if (opp.Status is OpportunityStatus.AwaitingOwnerConfirmation) opp.Status = OpportunityStatus.ReadyToPublish;
        opp.StatusChangedAt = now;
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "owner_confirmed", $"أكدت ملخص الفرصة (الإصدار {terms.VersionNo})", visible: true,
            from: from.ToString(), to: opp.Status.ToString());
        await db.SaveChangesAsync();
        return Results.Ok(new { opp.Reference, status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status] });
    }

    private static async Task<IResult> RequestChanges(string reference, OwnerTermsDecision req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var note = req.Note?.Trim();
        if (string.IsNullOrEmpty(note) || note.Length < 5) Validate.Throw("note", "اكتب ما تريد تعديله في الملخص.");
        var (_, opp, terms) = await PendingTermsAsync(db, rc, reference, req.TermsId);
        var now = clock.UtcNow;
        terms.Status = TermsStatus.OwnerRequestedChanges;
        terms.OwnerDecidedAt = now;
        terms.OwnerNote = note![..Math.Min(note.Length, 1000)];
        var from = opp.Status;
        if (opp.Status is OpportunityStatus.AwaitingOwnerConfirmation) opp.Status = OpportunityStatus.Preparing;
        opp.DraftTermsId = null;
        opp.StatusChangedAt = now;
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "owner_changes", "طلبت تعديل ملخص الفرصة", visible: true, reason: terms.OwnerNote,
            from: from.ToString(), to: opp.Status.ToString());
        await db.SaveChangesAsync();
        return Results.Ok(new { opp.Reference, status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status] });
    }
}
