using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Market;

public sealed record AssignInput(Guid? UserId);
public sealed record CompletionInput(List<string>? Items, string? Note);
public sealed record DecisionInput(string? Reason);
public sealed record VerifyFigureInput(string? FieldKey, string? Value, string? Source, Guid? SourceDocumentId, DateOnly? SourceDate, string? Note);
public sealed record CorrectInput(string? FieldKey, string? Value, string? Reason);
public sealed record FileReviewInput(string? Status, string? Note);
public sealed record ExternalApprovalInput(Guid ObligationId, string? Status, string? Conditions, Guid? DocumentId, DateOnly? DecisionDate, DateOnly? ExpiresOn, string? Note);
public sealed record CapacityReviewInput(decimal? ReviewedAvailableNow, string? Note);
public sealed record FinanceApprovalInput(string? Status, string? Source, DateOnly? Date, decimal? Amount);

/// <summary>
/// «فريق رهون» workspace: sale requests, buyer requests, contact messages (opportunities and interests are in
/// TeamOpportunityEndpoints). Every action checks its permission on the server and writes a reasoned entry in the log.
/// Drafts the person hasn't sent are never shown to the team.
/// </summary>
public static class TeamMarketEndpoints
{
    public static readonly string[] Sources = ["owner_declared", "contract", "developer_statement", "financier_statement", "payoff_letter", "title_deed", "other"];
    public static readonly Dictionary<string, string> SourceLabels = new()
    {
        ["owner_declared"] = "إقرار صاحب العقار", ["contract"] = "العقد", ["developer_statement"] = "كشف المطور", ["financier_statement"] = "كشف جهة التمويل",
        ["payoff_letter"] = "خطاب مبلغ السداد", ["title_deed"] = "الصك", ["other"] = "مصدر آخر",
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/team/market").RequireOrg(OrganizationKind.Operator).RequirePermission(P.MarketView);
        g.MapGet("/overview", Overview).RequirePermission(P.DashboardView);
        g.MapGet("/members", Members);

        g.MapGet("/sale-requests", ListSale);
        g.MapGet("/sale-requests/{reference}", GetSale);
        g.MapGet("/sale-requests/{reference}/contact", SaleContact);
        g.MapPost("/sale-requests/{reference}/assign", AssignSale).Idempotent();
        g.MapPost("/sale-requests/{reference}/start-review", StartReviewSale).Idempotent();
        g.MapPost("/sale-requests/{reference}/request-completion", CompletionSale).Idempotent();
        g.MapPost("/sale-requests/{reference}/approve", ApproveSale).Idempotent();
        g.MapPost("/sale-requests/{reference}/reject", RejectSale).Idempotent();
        g.MapPost("/sale-requests/{reference}/verify-figure", VerifyFigure).Idempotent();
        g.MapPost("/sale-requests/{reference}/correct", Correct).Idempotent();
        g.MapPost("/sale-requests/{reference}/documents/{id:guid}/review", ReviewDocument).Idempotent();
        g.MapPost("/sale-requests/{reference}/photos/{id:guid}/review", ReviewPhoto).Idempotent();
        g.MapPost("/sale-requests/{reference}/external-approvals", RecordApproval).Idempotent();
        g.MapPost("/sale-requests/{reference}/note", NoteSale).Idempotent();

        g.MapGet("/buyer-requests", ListBuyer);
        g.MapGet("/buyer-requests/{reference}", GetBuyer);
        g.MapPost("/buyer-requests/{reference}/assign", AssignBuyer).Idempotent();
        g.MapPost("/buyer-requests/{reference}/start-review", StartReviewBuyer).Idempotent();
        g.MapPost("/buyer-requests/{reference}/request-completion", CompletionBuyer).Idempotent();
        g.MapPost("/buyer-requests/{reference}/approve", ApproveBuyer).Idempotent();
        g.MapPost("/buyer-requests/{reference}/reject", RejectBuyer).Idempotent();
        g.MapPost("/buyer-requests/{reference}/capacity-review", CapacityReview).Idempotent();
        g.MapPost("/buyer-requests/{reference}/finance-approval", FinanceApproval).Idempotent();
        g.MapPost("/buyer-requests/{reference}/note", NoteBuyer).Idempotent();

        g.MapGet("/contact-messages", ListContact);
        g.MapPost("/contact-messages/{reference}/handled", HandledContact).RequirePermission(P.MarketFollow).Idempotent();
    }

    internal static void Need(RequestContext rc, string permission)
    {
        if (!rc.Has(permission)) throw new ForbiddenException();
    }

    // ── Overview ──

    private static async Task<IResult> Overview(RahoonDbContext db, RequestContext rc)
    {
        var me = rc.UserId;
        // Counts come from the same scoped queries as the lists: assigned-scope members see their own work only.
        var sale = await db.SaleRequests.Scoped(rc).Where(r => r.Status != SaleRequestStatus.Draft).GroupBy(r => r.Status).Select(g => new { g.Key, n = g.Count() }).ToListAsync();
        var buyer = await db.BuyerRequests.Scoped(rc).Where(r => r.Status != BuyerRequestStatus.Draft).GroupBy(r => r.Status).Select(g => new { g.Key, n = g.Count() }).ToListAsync();
        var opps = await db.Opportunities.Scoped(db, rc).GroupBy(o => o.Status).Select(g => new { g.Key, n = g.Count() }).ToListAsync();
        var interests = await db.Interests.Scoped(db, rc).GroupBy(i => i.Status).Select(g => new { g.Key, n = g.Count() }).ToListAsync();
        int? contact = rc.HasAll(P.MarketFollow) ? await db.ContactMessages.CountAsync(c => c.Status == ContactMessageStatus.New) : null;
        var mySale = await db.SaleRequests.Where(r => r.AssignedToUserId == me && (r.Status == SaleRequestStatus.Submitted || r.Status == SaleRequestStatus.UnderReview))
            .OrderBy(r => r.StatusChangedAt).Select(r => new { kind = "sale", r.Reference, title = r.Reference, status = r.Status.ToString(), at = r.StatusChangedAt }).ToListAsync();
        var myBuyer = await db.BuyerRequests.Where(r => r.AssignedToUserId == me && (r.Status == BuyerRequestStatus.Submitted || r.Status == BuyerRequestStatus.UnderReview))
            .Select(r => new { kind = "buyer", r.Reference, title = r.Reference, status = r.Status.ToString(), at = r.StatusChangedAt }).ToListAsync();
        var myOpps = await db.Opportunities.Where(o => o.AssignedToUserId == me && (o.Status == OpportunityStatus.Preparing || o.Status == OpportunityStatus.ReadyToPublish))
            .Select(o => new { kind = "opportunity", o.Reference, title = o.Title, status = o.Status.ToString(), at = o.StatusChangedAt }).ToListAsync();
        var myInterests = await db.Interests.Where(i => i.AssignedToUserId == me && (i.Status == InterestStatus.Received || i.Status == InterestStatus.InFollowUp))
            .Select(i => new { kind = "interest", i.Reference, title = i.Reference, status = i.Status.ToString(), at = i.StatusChangedAt }).ToListAsync();
        var unassignedSale = await db.SaleRequests.Scoped(rc).CountAsync(r => r.AssignedToUserId == null && (r.Status == SaleRequestStatus.Submitted || r.Status == SaleRequestStatus.UnderReview));
        var unassignedInterest = await db.Interests.Scoped(db, rc).CountAsync(i => i.AssignedToUserId == null && i.Status == InterestStatus.Received);
        return Results.Ok(new
        {
            sale = sale.ToDictionary(x => x.Key.ToString(), x => x.n),
            buyer = buyer.ToDictionary(x => x.Key.ToString(), x => x.n),
            opportunities = opps.ToDictionary(x => x.Key.ToString(), x => x.n),
            interests = interests.ToDictionary(x => x.Key.ToString(), x => x.n),
            contactNew = contact,
            unassigned = new { sale = unassignedSale, interests = unassignedInterest },
            myTasks = mySale.Concat(myBuyer).Concat(myOpps).Concat(myInterests).OrderBy(t => t.at),
            permissions = rc.Permissions.Where(p => p.StartsWith("market.")),
            scopes = rc.Grants.Where(g => g.Key.StartsWith("market.")).ToDictionary(g => g.Key, g => g.Value == GrantScope.All ? "all" : "assigned"),
        });
    }

    private static async Task<IResult> Members(RahoonDbContext db, RequestContext rc)
    {
        using var _ = rc.BeginSystemScope();
        var org = rc.OrganizationId!.Value;
        var members = await db.Memberships.Include(m => m.User).Include(m => m.Roles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions)
            .Where(m => m.OrganizationId == org && m.Status == MembershipStatus.Active).ToListAsync();
        // Only members who can see request work can be given some.
        return Results.Ok(members.Where(m => EffectiveAccess.Grants(m.Roles.Select(r => r.Role!)).ContainsKey(P.MarketView))
            .Select(m => new { id = m.UserId, name = m.User!.FullName, role = m.Roles.Select(r => r.Role!).OrderByDescending(r => r.IsSystem).Select(r => r.NameAr).FirstOrDefault() }));
    }

    internal static async Task<(Guid Id, string Label)> AssigneeAsync(RahoonDbContext db, RequestContext rc, Guid? userId)
    {
        if (userId is null || userId == rc.UserId) return (rc.UserId, rc.UserName);
        Need(rc, P.MarketAssign);
        using var _ = rc.BeginSystemScope();
        var org = rc.OrganizationId!.Value;
        var m = await db.Memberships.Include(x => x.Roles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(x => x.OrganizationId == org && x.UserId == userId && x.Status == MembershipStatus.Active);
        if (m is null) Validate.Throw("userId", "اختر عضوًا نشطًا من فريق رهون.");
        if (!EffectiveAccess.Grants(m!.Roles.Select(r => r.Role!)).ContainsKey(P.MarketView))
            Validate.Throw("userId", "هذا العضو لا يملك صلاحية عرض الطلبات، فلا يمكن إسناد العمل إليه.");
        var name = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstAsync();
        return (userId!.Value, name);
    }

    // ── Sale requests ──

    private static readonly Dictionary<string, SaleRequestStatus[]> SaleQueues = new()
    {
        ["new"] = [SaleRequestStatus.Submitted],
        ["review"] = [SaleRequestStatus.UnderReview],
        ["completion"] = [SaleRequestStatus.NeedsCompletion],
        ["approved"] = [SaleRequestStatus.ApprovedForListing],
        ["closed"] = [SaleRequestStatus.Rejected, SaleRequestStatus.Withdrawn],
        ["all"] = [SaleRequestStatus.Submitted, SaleRequestStatus.UnderReview, SaleRequestStatus.NeedsCompletion, SaleRequestStatus.ApprovedForListing, SaleRequestStatus.Rejected, SaleRequestStatus.Withdrawn],
    };

    private static async Task<IResult> ListSale(RahoonDbContext db, RequestContext rc, string? queue, string? assigned, string? q)
    {
        var statuses = SaleQueues.GetValueOrDefault(queue ?? "all") ?? SaleQueues["all"];
        var query = db.SaleRequests.Scoped(rc).Include(r => r.Obligations).Where(r => statuses.Contains(r.Status));
        if (assigned == "me") query = query.Where(r => r.AssignedToUserId == rc.UserId);
        else if (assigned == "none") query = query.Where(r => r.AssignedToUserId == null);
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim(); query = query.Where(r => r.Reference.Contains(term) || (r.ContactName != null && r.ContactName.Contains(term))); }
        var rows = await query.OrderByDescending(r => r.StatusChangedAt).Take(200).ToListAsync();
        return Results.Ok(rows.Select(r => new
        {
            r.Reference, status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status],
            propertyTypeLabel = FieldCatalog.Label(FieldCatalog.PropertyTypes, r.PropertyType), cityLabel = FieldCatalog.City(r.City)?.Label, r.District,
            obligationLabel = FieldCatalog.Label(FieldCatalog.ObligationModes, r.ObligationMode), applicantName = r.ContactName,
            r.SubmittedAt, r.StatusChangedAt, assignedTo = r.AssignedToLabel, ownerMarkedComplete = r.OwnerMarkedCompleteAt is not null,
        }));
    }

    /// <summary>A sent sale request the caller may see (404 otherwise) and act on with <paramref name="permission"/> (403 otherwise).</summary>
    internal static async Task<SaleRequestFile> LoadSaleAsync(RahoonDbContext db, RequestContext rc, string reference, string permission)
    {
        var f = await SaleRequestFile.LoadAsync(db, reference) is { } x && x.Request.Status != SaleRequestStatus.Draft ? x : throw new NotFoundException();
        TeamScope.Need(rc, permission, TeamScope.Assignees(f.Request));
        return f;
    }

    private static async Task<IResult> GetSale(string reference, RahoonDbContext db, RequestContext rc, IConfiguration config)
    {
        Need(rc, P.MarketView);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketView);
        var r = f.Request;
        var policy = CommissionPolicy.From(config);
        string? phoneMasked;
        using (rc.BeginSystemScope())
            phoneMasked = await db.IndividualProfiles.Where(p => p.UserId == r.ApplicantUserId).Select(p => p.PhoneMasked).FirstOrDefaultAsync();
        var itemOptions = ItemOptions(f);
        return Results.Ok(new
        {
            file = SaleRequestEndpoints.OwnerDto(f, policy),
            events = f.Events.OrderByDescending(e => e.At).Select(SaleRequestFile.EventDto),
            applicant = new { name = r.ContactName, phoneMasked, email = r.ContactEmail, relationship = r.RelationshipDeclared, r.DeclarationsAcceptedAt, r.DeclarationsVersion },
            assignedTo = r.AssignedToUserId is null ? null : new { id = r.AssignedToUserId, label = r.AssignedToLabel },
            decisionReason = r.DecisionReason,
            ownerMarkedCompleteAt = r.OwnerMarkedCompleteAt,
            verifications = f.Verifications.Select(v => new
            {
                v.FieldKey, label = f.ItemLabel(v.FieldKey), v.Value, v.Source, sourceLabel = SourceLabels.GetValueOrDefault(v.Source, v.Source), v.SourceDocumentId,
                v.SourceDate, v.Note, v.VerifiedByLabel, v.VerifiedAt,
            }),
            approvals = f.Obligations.Select(o => new
            {
                obligationId = o.Id, partyName = o.PartyDisplayName, kind = o.Kind,
                current = f.Approvals.LastOrDefault(a => a.ObligationId == o.Id) is { } a ? ApprovalDto(a) : null,
                history = f.Approvals.Where(a => a.ObligationId == o.Id).OrderByDescending(a => a.RecordedAt).Select(ApprovalDto),
            }),
            completionRequests = f.CompletionRequests.OrderByDescending(c => c.RequestedAt).Select(f.CompletionDto),
            opportunities = f.Opportunities.Select(o => new { o.Reference, status = o.Status, statusLabel = OpportunityFlow.Labels[o.Status] }),
            itemOptions,
            verifiableKeys = VerifiableKeys(f),
            actions = SaleActions(f, rc),
        });
    }

    private static object ApprovalDto(ExternalApproval a) => new
    {
        a.Id, status = a.Status, statusLabel = ExternalApprovalLabels.Labels[a.Status], a.Conditions, a.DocumentId, a.DecisionDate, a.ExpiresOn, a.Note,
        a.RecordedByLabel, a.RecordedAt,
    };

    /// <summary>Everything the team may ask the owner to complete, with labels (fields, obligation fields, documents, location, photos).</summary>
    private static IEnumerable<object> ItemOptions(SaleRequestFile f) => ItemKeys(f).Select(k => new { key = k, label = f.ItemLabel(k) });

    private static List<string> ItemKeys(SaleRequestFile f)
    {
        var r = f.Request;
        var keys = new List<string> { "district", "location", "photos", "cover" };
        keys.AddRange(FieldCatalog.PropertyFields(r.PropertyType, r.Answers).Select(x => x.Key));
        foreach (var o in f.Obligations)
        {
            keys.Add(SaleRequestFile.ObligationKey(o.Id, "party"));
            keys.AddRange(FieldCatalog.Fields.Where(x => x.Scope == "obligation" && x.ObligationKinds!.Contains(o.Kind)).Select(x => SaleRequestFile.ObligationKey(o.Id, x.Key)));
        }
        keys.AddRange(FieldCatalog.DocumentsFor(f.ActiveKinds).Select(d => "doc:" + d.Key));
        keys.Add("other");
        // What the file is still missing comes first, so the usual completion request is a few ticks at the top.
        var missing = f.Completeness().SelectMany(g => g.Missing).Select(m => m.Key).Where(keys.Contains).ToList();
        return missing.Concat(keys).Distinct().ToList();
    }

    private static IEnumerable<object> VerifiableKeys(SaleRequestFile f)
    {
        foreach (var o in f.Obligations)
            foreach (var x in FieldCatalog.Fields.Where(x => x.Scope == "obligation" && x.ObligationKinds!.Contains(o.Kind) && x.Type is FieldType.Money or FieldType.Integer or FieldType.Select or FieldType.Date))
            {
                var key = SaleRequestFile.ObligationKey(o.Id, x.Key);
                yield return new { key, label = f.ItemLabel(key), current = o.Answers.GetValueOrDefault(x.Key), type = x.Type, options = x.Options };
            }
        foreach (var x in FieldCatalog.PropertyFields(f.Request.PropertyType, f.Request.Answers).Where(x => x.Type is FieldType.Decimal or FieldType.Integer))
            yield return new { key = x.Key, label = x.Label, current = f.Request.Answers.GetValueOrDefault(x.Key), type = x.Type, options = x.Options };
    }

    private static object SaleActions(SaleRequestFile f, RequestContext rc)
    {
        var s = f.Request.Status;
        var who = TeamScope.Assignees(f.Request);
        var review = rc.CanOn(P.MarketReview, who);
        var decide = rc.CanOn(P.MarketDecide, who);
        var open = s is not (SaleRequestStatus.Rejected or SaleRequestStatus.Withdrawn);
        return new
        {
            assign = review && s is SaleRequestStatus.Submitted or SaleRequestStatus.UnderReview or SaleRequestStatus.NeedsCompletion or SaleRequestStatus.ApprovedForListing,
            assignOthers = rc.Has(P.MarketAssign),
            startReview = review && s == SaleRequestStatus.Submitted,
            requestCompletion = review && s is SaleRequestStatus.Submitted or SaleRequestStatus.UnderReview,
            approve = decide && s == SaleRequestStatus.UnderReview,
            reject = decide && s is SaleRequestStatus.Submitted or SaleRequestStatus.UnderReview or SaleRequestStatus.NeedsCompletion,
            verify = rc.CanOn(P.MarketVerify, who) && open,
            correct = rc.CanOn(P.MarketEdit, who) && open,
            note = rc.CanOn(P.MarketEdit, who),
            reviewFiles = rc.CanOn(P.DocumentsReview, who),
            openDocuments = rc.CanOn(P.DocumentsRead, who),
            contact = rc.CanOn(P.MarketFollow, who),
            externalApproval = review,
            createOpportunity = rc.CanOn(P.MarketPrepare, who) && s == SaleRequestStatus.ApprovedForListing
                                && !f.Opportunities.Any(o => o.Status != OpportunityStatus.Withdrawn),
        };
    }

    private static async Task<IResult> SaleContact(string reference, RahoonDbContext db, RequestContext rc, PiiProtector pii, AuditLog audit)
    {
        Need(rc, P.MarketFollow);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketFollow);
        string phone;
        using (rc.BeginSystemScope())
        {
            var p = await db.IndividualProfiles.FirstAsync(x => x.UserId == f.Request.ApplicantUserId);
            phone = pii.Unprotect(p.PhoneEnc);
        }
        await audit.RecordAsync(new AuditEntry("market.contact_reveal", "إظهار جوال صاحب طلب البيع", SubjectType: "sale_request", SubjectReference: reference));
        await db.SaveChangesAsync();
        return Results.Ok(new { phone });
    }

    private static async Task<IResult> AssignSale(string reference, AssignInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketReview);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketReview);
        var (id, label) = await AssigneeAsync(db, rc, req.UserId);
        var r = f.Request;
        r.AssignedToUserId = id;
        r.AssignedToLabel = label;
        r.AssignedAt = clock.UtcNow;
        market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "assigned", $"أُسند الطلب إلى {label}", visible: false);
        await db.SaveChangesAsync();
        return Results.Ok(new { assignedTo = new { id, label } });
    }

    private static void Transition(SaleRequest r, SaleRequestStatus to, IClock clock)
    {
        r.Status = to;
        r.StatusChangedAt = clock.UtcNow;
    }

    private static async Task<IResult> StartReviewSale(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketReview);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketReview);
        var r = f.Request;
        SaleRequestFlow.Ensure(r.Status, SaleRequestStatus.Submitted);
        Transition(r, SaleRequestStatus.UnderReview, clock);
        if (r.AssignedToUserId is null) { r.AssignedToUserId = rc.UserId; r.AssignedToLabel = rc.UserName; r.AssignedAt = clock.UtcNow; }
        market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "status", "بدأ فريق رهون مراجعة طلبك", visible: true,
            from: nameof(SaleRequestStatus.Submitted), to: nameof(SaleRequestStatus.UnderReview));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> CompletionSale(string reference, CompletionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketReview);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketReview);
        var r = f.Request;
        SaleRequestFlow.Ensure(r.Status, SaleRequestStatus.Submitted, SaleRequestStatus.UnderReview);
        var allowed = ItemKeys(f).ToHashSet();
        var items = (req.Items ?? []).Where(allowed.Contains).Distinct().ToList();
        var note = req.Note?.Trim() ?? "";
        new Validator()
            .Require(items.Count > 0, "items", "اختر ما يحتاج استكمالًا.")
            .Require(note.Length >= 5, "note", "اكتب ملاحظة واضحة لصاحب الطلب.")
            .ThrowIfInvalid();
        var from = r.Status;
        Transition(r, SaleRequestStatus.NeedsCompletion, clock);
        db.CompletionRequests.Add(new CompletionRequest
        {
            OrganizationId = r.OrganizationId, SubjectType = "sale_request", SubjectId = r.Id, ApplicantUserId = r.ApplicantUserId, Items = items,
            Note = note[..Math.Min(note.Length, 1000)], RequestedByUserId = rc.UserId, RequestedByLabel = rc.UserName, RequestedAt = clock.UtcNow,
        });
        var e = market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "completion_requested", "يحتاج طلبك استكمالًا", visible: true,
            body: note, from: from.ToString(), to: nameof(SaleRequestStatus.NeedsCompletion), data: new { items });
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(r.ApplicantUserId, e.Id, $"رهون: يحتاج طلبك {r.Reference} استكمال بعض البيانات. التفاصيل في حسابك.");
        return Results.Ok(new { status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> ApproveSale(string reference, DecisionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketDecide);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketDecide);
        var r = f.Request;
        SaleRequestFlow.Ensure(r.Status, SaleRequestStatus.UnderReview);
        Transition(r, SaleRequestStatus.ApprovedForListing, clock);
        r.DecidedAt = clock.UtcNow;
        r.DecidedByUserId = rc.UserId;
        r.DecisionReason = req.Reason?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 1000)] : null;
        var e = market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "approved", "اعتمد الفريق طلبك لإعداد فرصة", visible: true,
            body: "سنجهز ملخص الفرصة ونرسله لك لتؤكده قبل أي نشر. الاعتماد لا يعني موافقة المطور أو جهة التمويل.", reason: r.DecisionReason,
            from: nameof(SaleRequestStatus.UnderReview), to: nameof(SaleRequestStatus.ApprovedForListing));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(r.ApplicantUserId, e.Id, $"رهون: اعتمد الفريق طلبك {r.Reference} لإعداد فرصة. سنرسل لك الملخص لتأكيده.");
        return Results.Ok(new { status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> RejectSale(string reference, DecisionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketDecide);
        var reason = req.Reason?.Trim() ?? "";
        if (reason.Length < 5) Validate.Throw("reason", "اكتب سبب الرفض بوضوح؛ سيظهر لصاحب الطلب.");
        var f = await LoadSaleAsync(db, rc, reference, P.MarketDecide);
        var r = f.Request;
        SaleRequestFlow.Ensure(r.Status, SaleRequestStatus.Submitted, SaleRequestStatus.UnderReview, SaleRequestStatus.NeedsCompletion);
        var from = r.Status;
        Transition(r, SaleRequestStatus.Rejected, clock);
        r.DecidedAt = clock.UtcNow;
        r.DecidedByUserId = rc.UserId;
        r.DecisionReason = reason[..Math.Min(reason.Length, 1000)];
        var e = market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "rejected", "لم يُقبل طلبك", visible: true, reason: r.DecisionReason,
            from: from.ToString(), to: nameof(SaleRequestStatus.Rejected));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(r.ApplicantUserId, e.Id, $"رهون: تحديث على طلبك {r.Reference}. التفاصيل في حسابك.");
        return Results.Ok(new { status = r.Status, statusLabel = SaleRequestFlow.Labels[r.Status] });
    }

    /// <summary>«o:{obligationId}:{key}» or a property key → the field definition and the answers holding it.</summary>
    private static (FieldDef Def, Dictionary<string, string> Answers, SaleObligation? Obligation) ResolveKey(SaleRequestFile f, string? fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey)) throw new ValidationFailedException(new Dictionary<string, string[]> { ["fieldKey"] = ["اختر الحقل."] });
        if (fieldKey.StartsWith("o:"))
        {
            var parts = fieldKey.Split(':');
            var o = parts.Length == 3 ? f.Obligations.FirstOrDefault(x => x.Id.ToString() == parts[1]) : null;
            if (o is null || !FieldCatalog.ByKey.TryGetValue(parts[2], out var od) || od.Scope != "obligation" || !od.ObligationKinds!.Contains(o.Kind))
                throw new ValidationFailedException(new Dictionary<string, string[]> { ["fieldKey"] = ["الحقل غير موجود في هذا الطلب."] });
            return (od, o.Answers, o);
        }
        if (!FieldCatalog.ByKey.TryGetValue(fieldKey, out var pd) || pd.Scope != "property" || !FieldCatalog.Applies(pd, f.Request.PropertyType, null, f.Request.Answers))
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["fieldKey"] = ["الحقل غير موجود في هذا الطلب."] });
        return (pd, f.Request.Answers, null);
    }

    private static async Task<IResult> VerifyFigure(string reference, VerifyFigureInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketVerify);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketVerify);
        var (def, answers, _) = ResolveKey(f, req.FieldKey);
        var raw = req.Value ?? answers.GetValueOrDefault(def.Key);
        var v = new Validator();
        v.Require(!string.IsNullOrWhiteSpace(raw) && raw != FieldCatalog.Unknown, "value", "أدخل القيمة كما في المستند.");
        v.Require(req.Source is not null && Sources.Contains(req.Source), "source", "اختر مصدر الرقم.");
        v.Require(req.SourceDate is not null, "sourceDate", "أدخل تاريخ المستند أو المصدر.");
        v.Require(req.SourceDate is null || req.SourceDate <= clock.TodayRiyadh, "sourceDate", "تاريخ المصدر لا يمكن أن يكون في المستقبل.");
        if (req.SourceDocumentId is { } did) v.Require(f.Documents.Any(d => d.Id == did), "sourceDocumentId", "اختر مستندًا من مستندات الطلب.");
        v.ThrowIfInvalid();
        var (ok, value, error) = FieldCatalog.Normalize(def, raw!, f.Request.PropertyType);
        if (!ok) Validate.Throw("value", error!);

        var now = clock.UtcNow;
        foreach (var old in f.Verifications.Where(x => x.FieldKey == req.FieldKey)) old.SupersededAt = now;
        db.FigureVerifications.Add(new FigureVerification
        {
            OrganizationId = f.Request.OrganizationId, SaleRequestId = f.Request.Id, ApplicantUserId = f.Request.ApplicantUserId, FieldKey = req.FieldKey!, Value = value!,
            Source = req.Source!, SourceDocumentId = req.SourceDocumentId, SourceDate = req.SourceDate!.Value, Note = req.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 500)] : null,
            VerifiedByUserId = rc.UserId, VerifiedByLabel = rc.UserName, VerifiedAt = now,
        });
        var differs = answers.GetValueOrDefault(def.Key) is { } declared && declared != value;
        market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "figure_verified",
            $"راجع الفريق «{f.ItemLabel(req.FieldKey!)}» من {SourceLabels[req.Source!]}", visible: true,
            body: differs ? "القيمة المعتمدة تختلف عما أدخلته؛ تُستخدم القيمة المعتمدة في إعداد الفرصة." : null,
            data: new { req.FieldKey, value, declared = answers.GetValueOrDefault(def.Key), req.Source, req.SourceDate });
        await db.SaveChangesAsync();
        return Results.Ok(new { verified = true });
    }

    private static async Task<IResult> Correct(string reference, CorrectInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketEdit);
        var reason = req.Reason?.Trim() ?? "";
        if (reason.Length < 5) Validate.Throw("reason", "اكتب سبب التصحيح.");
        var f = await LoadSaleAsync(db, rc, reference, P.MarketEdit);
        if (f.Request.Status is SaleRequestStatus.Rejected or SaleRequestStatus.Withdrawn) throw new ConflictException("invalid_status", "الطلب مغلق.");
        var (def, answers, o) = ResolveKey(f, req.FieldKey);
        var old = answers.GetValueOrDefault(def.Key);
        if (string.IsNullOrWhiteSpace(req.Value)) answers.Remove(def.Key);
        else
        {
            var (ok, value, error) = FieldCatalog.Normalize(def, req.Value, f.Request.PropertyType);
            if (!ok) Validate.Throw("value", error!);
            answers[def.Key] = value!;
        }
        // Owned dictionaries are tracked by value: reassign so EF sees the change.
        if (o is not null) o.Answers = new Dictionary<string, string>(answers); else f.Request.Answers = new Dictionary<string, string>(answers);
        foreach (var v in f.Verifications.Where(x => x.FieldKey == req.FieldKey && x.Value != answers.GetValueOrDefault(def.Key))) v.SupersededAt = clock.UtcNow;
        market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "corrected", $"صحّح الفريق «{f.ItemLabel(req.FieldKey!)}»", visible: true,
            reason: reason, data: new { req.FieldKey, old, @new = answers.GetValueOrDefault(def.Key) });
        await db.SaveChangesAsync();
        return Results.Ok(new { corrected = true });
    }

    private static FileReviewStatus ParseReview(FileReviewInput req)
    {
        var status = req.Status switch { "accepted" => FileReviewStatus.Accepted, "rejected" => FileReviewStatus.Rejected, _ => (FileReviewStatus?)null };
        if (status is null) Validate.Throw("status", "اختر القرار.");
        if (status == FileReviewStatus.Rejected && (req.Note?.Trim().Length ?? 0) < 5) Validate.Throw("note", "اكتب سبب الرفض ليعرف صاحب الطلب ما المطلوب.");
        return status!.Value;
    }

    private static async Task<IResult> ReviewDocument(string reference, Guid id, FileReviewInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.DocumentsReview);
        var status = ParseReview(req);
        var f = await LoadSaleAsync(db, rc, reference, P.DocumentsReview);
        var d = f.Documents.FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException();
        d.ReviewStatus = status;
        d.ReviewNote = req.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 500)] : null;
        d.ReviewedAt = clock.UtcNow;
        d.ReviewedByUserId = rc.UserId;
        var label = FieldCatalog.Documents.FirstOrDefault(x => x.Key == d.Kind)?.Label ?? d.Kind;
        market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "document_review",
            status == FileReviewStatus.Accepted ? $"قُبل المستند: {label}" : $"لم يُقبل المستند: {label}", visible: true, reason: d.ReviewNote);
        await db.SaveChangesAsync();
        return Results.Ok(new { status = d.ReviewStatus });
    }

    private static async Task<IResult> ReviewPhoto(string reference, Guid id, FileReviewInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.DocumentsReview);
        var status = ParseReview(req);
        var f = await LoadSaleAsync(db, rc, reference, P.DocumentsReview);
        var p = f.Photos.FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException();
        p.ReviewStatus = status;
        p.ReviewNote = req.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 500)] : null;
        if (status == FileReviewStatus.Rejected)
            market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "photo_review", "لم تُقبل إحدى الصور للعرض", visible: true, reason: p.ReviewNote);
        await db.SaveChangesAsync();
        return Results.Ok(new { status = p.ReviewStatus });
    }

    private static async Task<IResult> RecordApproval(string reference, ExternalApprovalInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketReview);
        var f = await LoadSaleAsync(db, rc, reference, P.MarketReview);
        var o = f.Obligations.FirstOrDefault(x => x.Id == req.ObligationId) ?? throw new NotFoundException();
        if (!Enum.TryParse<ExternalApprovalStatus>(req.Status, true, out var status)) Validate.Throw("status", "اختر حالة الموافقة.");
        var v = new Validator();
        if (status is ExternalApprovalStatus.Conditional) v.Require(!string.IsNullOrWhiteSpace(req.Conditions), "conditions", "اكتب شروط الموافقة كما في مستند الجهة.");
        if (status is ExternalApprovalStatus.Approved or ExternalApprovalStatus.Conditional or ExternalApprovalStatus.Rejected)
        {
            v.Require(req.DocumentId is { } did && f.Documents.Any(d => d.Id == did), "documentId", "اختر مستند الجهة الذي يثبت القرار.");
            v.Require(req.DecisionDate is not null, "decisionDate", "أدخل تاريخ قرار الجهة.");
        }
        v.ThrowIfInvalid();
        db.ExternalApprovals.Add(new ExternalApproval
        {
            OrganizationId = f.Request.OrganizationId, SaleRequestId = f.Request.Id, ApplicantUserId = f.Request.ApplicantUserId, ObligationId = o.Id, Status = status,
            Conditions = req.Conditions?.Trim(), DocumentId = req.DocumentId, DecisionDate = req.DecisionDate, ExpiresOn = req.ExpiresOn,
            Note = req.Note?.Trim(), RecordedByUserId = rc.UserId, RecordedByLabel = rc.UserName, RecordedAt = clock.UtcNow,
        });
        market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "external_approval",
            $"موافقة {(o.Kind == "developer" ? "المطور" : "جهة التمويل")} ({o.PartyDisplayName}): {ExternalApprovalLabels.Labels[status]}", visible: true,
            body: "هذه موافقة الجهة نفسها، وهي منفصلة عن اعتماد رهون للطلب.", reason: req.Conditions);
        await db.SaveChangesAsync();
        return Results.Ok(new { status, statusLabel = ExternalApprovalLabels.Labels[status] });
    }

    private static async Task<IResult> NoteSale(string reference, NoteInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        Need(rc, P.MarketEdit);
        var note = req.Note?.Trim() ?? "";
        if (note.Length < 2) Validate.Throw("note", "اكتب الملاحظة.");
        var f = await LoadSaleAsync(db, rc, reference, P.MarketEdit);
        market.Event(f.Request.OrganizationId, "sale_request", f.Request.Id, f.Request.ApplicantUserId, "note", "ملاحظة داخلية", visible: false, body: note[..Math.Min(note.Length, 2000)]);
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    // ── Buyer requests ──

    private static readonly Dictionary<string, BuyerRequestStatus[]> BuyerQueues = new()
    {
        ["new"] = [BuyerRequestStatus.Submitted],
        ["review"] = [BuyerRequestStatus.UnderReview],
        ["completion"] = [BuyerRequestStatus.NeedsCompletion],
        ["approved"] = [BuyerRequestStatus.ApprovedForMatching],
        ["closed"] = [BuyerRequestStatus.Rejected, BuyerRequestStatus.Withdrawn],
        ["all"] = [BuyerRequestStatus.Submitted, BuyerRequestStatus.UnderReview, BuyerRequestStatus.NeedsCompletion, BuyerRequestStatus.ApprovedForMatching, BuyerRequestStatus.Rejected, BuyerRequestStatus.Withdrawn],
    };

    private static async Task<IResult> ListBuyer(RahoonDbContext db, RequestContext rc, string? queue, string? assigned)
    {
        var statuses = BuyerQueues.GetValueOrDefault(queue ?? "all") ?? BuyerQueues["all"];
        var query = db.BuyerRequests.Scoped(rc).Where(r => statuses.Contains(r.Status));
        if (assigned == "me") query = query.Where(r => r.AssignedToUserId == rc.UserId);
        else if (assigned == "none") query = query.Where(r => r.AssignedToUserId == null);
        var rows = await query.OrderByDescending(r => r.StatusChangedAt).Take(200).ToListAsync();
        return Results.Ok(rows.Select(r => new
        {
            r.Reference, status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status], name = r.ContactName, r.AvailableNow, r.InstallmentComfort,
            r.InstallmentFrequency, cities = r.Cities.Select(c => FieldCatalog.City(c)?.Label ?? c), types = r.PropertyTypes.Select(t => FieldCatalog.Label(FieldCatalog.PropertyTypes, t)),
            r.PurchaseMode, r.SubmittedAt, r.StatusChangedAt, assignedTo = r.AssignedToLabel, r.FinanceApprovalStatus,
        }));
    }

    /// <summary>A sent buyer request the caller may see (404 otherwise) and act on with <paramref name="permission"/> (403 otherwise).</summary>
    internal static async Task<BuyerRequest> LoadBuyerAsync(RahoonDbContext db, RequestContext rc, string reference, string permission)
    {
        var r = await db.BuyerRequests.FirstOrDefaultAsync(r => r.Reference == reference && r.Status != BuyerRequestStatus.Draft) ?? throw new NotFoundException();
        TeamScope.Need(rc, permission, TeamScope.Assignees(r));
        return r;
    }

    private static async Task<IResult> GetBuyer(string reference, RahoonDbContext db, RequestContext rc)
    {
        Need(rc, P.MarketView);
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketView);
        var events = await db.MarketEvents.Where(e => e.SubjectId == r.Id).OrderByDescending(e => e.At).ToListAsync();
        var completions = await db.CompletionRequests.Where(c => c.SubjectId == r.Id).OrderByDescending(c => c.RequestedAt).ToListAsync();
        var interests = await db.Interests.Where(i => i.ApplicantUserId == r.ApplicantUserId)
            .Join(db.Opportunities, i => i.OpportunityId, o => o.Id, (i, o) => new { i.Reference, status = i.Status, opportunity = o.Reference, o.Title, i.CreatedAt }).ToListAsync();
        string? phoneMasked;
        using (rc.BeginSystemScope())
            phoneMasked = await db.IndividualProfiles.Where(p => p.UserId == r.ApplicantUserId).Select(p => p.PhoneMasked).FirstOrDefaultAsync();
        var s = r.Status;
        var who = TeamScope.Assignees(r);
        var review = rc.CanOn(P.MarketReview, who);
        var decide = rc.CanOn(P.MarketDecide, who);
        return Results.Ok(new
        {
            request = BuyerEndpoints.BuyerDto(r, completions.FirstOrDefault(c => c.AnsweredAt is null)),
            phoneMasked,
            events = events.Select(SaleRequestFile.EventDto),
            completionRequests = completions.Select(c => new { c.Id, c.Items, c.Note, c.RequestedAt, c.RequestedByLabel, c.AnsweredAt }),
            interests,
            assignedTo = r.AssignedToUserId is null ? null : new { id = r.AssignedToUserId, label = r.AssignedToLabel },
            actions = new
            {
                assign = review, assignOthers = rc.Has(P.MarketAssign),
                startReview = review && s == BuyerRequestStatus.Submitted,
                requestCompletion = review && s is BuyerRequestStatus.Submitted or BuyerRequestStatus.UnderReview,
                approve = decide && s == BuyerRequestStatus.UnderReview,
                reject = decide && s is BuyerRequestStatus.Submitted or BuyerRequestStatus.UnderReview or BuyerRequestStatus.NeedsCompletion,
                capacity = review && s is not (BuyerRequestStatus.Rejected or BuyerRequestStatus.Withdrawn),
                financeApproval = rc.CanOn(P.MarketVerify, who) && s is not (BuyerRequestStatus.Rejected or BuyerRequestStatus.Withdrawn),
                note = rc.CanOn(P.MarketEdit, who),
            },
        });
    }

    private static async Task<IResult> AssignBuyer(string reference, AssignInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        Need(rc, P.MarketReview);
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketReview);
        var (id, label) = await AssigneeAsync(db, rc, req.UserId);
        r.AssignedToUserId = id;
        r.AssignedToLabel = label;
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "assigned", $"أُسند الطلب إلى {label}", visible: false);
        await db.SaveChangesAsync();
        return Results.Ok(new { assignedTo = new { id, label } });
    }

    private static async Task<IResult> StartReviewBuyer(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketReview);
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketReview);
        BuyerRequestFlow.Ensure(r.Status, BuyerRequestStatus.Submitted);
        r.Status = BuyerRequestStatus.UnderReview;
        r.StatusChangedAt = clock.UtcNow;
        if (r.AssignedToUserId is null) { r.AssignedToUserId = rc.UserId; r.AssignedToLabel = rc.UserName; }
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "status", "بدأ فريق رهون مراجعة طلبك", visible: true,
            from: nameof(BuyerRequestStatus.Submitted), to: nameof(BuyerRequestStatus.UnderReview));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> CompletionBuyer(string reference, CompletionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketReview);
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketReview);
        BuyerRequestFlow.Ensure(r.Status, BuyerRequestStatus.Submitted, BuyerRequestStatus.UnderReview);
        var note = req.Note?.Trim() ?? "";
        if (note.Length < 5) Validate.Throw("note", "اكتب ما يحتاج استكمالًا بوضوح.");
        var from = r.Status;
        r.Status = BuyerRequestStatus.NeedsCompletion;
        r.StatusChangedAt = clock.UtcNow;
        db.CompletionRequests.Add(new CompletionRequest
        {
            OrganizationId = r.OrganizationId, SubjectType = "buyer_request", SubjectId = r.Id, ApplicantUserId = r.ApplicantUserId,
            Items = (req.Items ?? []).Take(10).ToList(), Note = note[..Math.Min(note.Length, 1000)], RequestedByUserId = rc.UserId, RequestedByLabel = rc.UserName,
            RequestedAt = clock.UtcNow,
        });
        var e = market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "completion_requested", "يحتاج طلب الشراء استكمالًا", visible: true, body: note,
            from: from.ToString(), to: nameof(BuyerRequestStatus.NeedsCompletion));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(r.ApplicantUserId, e.Id, $"رهون: يحتاج طلب الشراء {r.Reference} استكمال بعض البيانات. التفاصيل في حسابك.");
        return Results.Ok(new { status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> ApproveBuyer(string reference, DecisionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketDecide);
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketDecide);
        BuyerRequestFlow.Ensure(r.Status, BuyerRequestStatus.UnderReview);
        r.Status = BuyerRequestStatus.ApprovedForMatching;
        r.StatusChangedAt = clock.UtcNow;
        r.DecidedAt = clock.UtcNow;
        r.DecisionReason = req.Reason?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 1000)] : null;
        var e = market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "approved", "ملفك معتمد للمطابقة", visible: true,
            body: "اعتماد فريق رهون لملفك لا يعني موافقة بنك أو جهة تمويل.", reason: r.DecisionReason,
            from: nameof(BuyerRequestStatus.UnderReview), to: nameof(BuyerRequestStatus.ApprovedForMatching));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(r.ApplicantUserId, e.Id, $"رهون: ملف الشراء {r.Reference} معتمد للمطابقة.");
        return Results.Ok(new { status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> RejectBuyer(string reference, DecisionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketDecide);
        var reason = req.Reason?.Trim() ?? "";
        if (reason.Length < 5) Validate.Throw("reason", "اكتب سبب الرفض بوضوح؛ سيظهر لصاحب الطلب.");
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketDecide);
        BuyerRequestFlow.Ensure(r.Status, BuyerRequestStatus.Submitted, BuyerRequestStatus.UnderReview, BuyerRequestStatus.NeedsCompletion);
        var from = r.Status;
        r.Status = BuyerRequestStatus.Rejected;
        r.StatusChangedAt = clock.UtcNow;
        r.DecidedAt = clock.UtcNow;
        r.DecisionReason = reason[..Math.Min(reason.Length, 1000)];
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "rejected", "لم يُقبل طلب الشراء", visible: true, reason: r.DecisionReason,
            from: from.ToString(), to: nameof(BuyerRequestStatus.Rejected));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> CapacityReview(string reference, CapacityReviewInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketReview);
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketReview);
        var note = req.Note?.Trim() ?? "";
        new Validator()
            .Require(req.ReviewedAvailableNow is >= 0, "reviewedAvailableNow", "أدخل المبلغ الذي راجعه الفريق.")
            .Require(note.Length >= 5, "note", "اكتب ما راجعه الفريق (نوع الإثبات وتاريخه).")
            .ThrowIfInvalid();
        r.ReviewedAvailableNow = req.ReviewedAvailableNow;
        r.CapacityReviewNote = note[..Math.Min(note.Length, 500)];
        r.CapacityReviewedAt = clock.UtcNow;
        r.CapacityReviewedByLabel = rc.UserName;
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "capacity_reviewed", "راجع الفريق إثبات القدرة الشرائية", visible: true,
            body: "هذه مراجعة من فريق رهون، وليست موافقة تمويل.", data: new { req.ReviewedAvailableNow });
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> FinanceApproval(string reference, FinanceApprovalInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        Need(rc, P.MarketVerify);
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketVerify);
        var v = new Validator();
        v.Require(req.Status is "none" or "pre_approval" or "approved", "status", "اختر حالة موافقة التمويل.");
        if (req.Status is "pre_approval" or "approved")
            v.Require(!string.IsNullOrWhiteSpace(req.Source), "source", "اكتب اسم جهة التمويل التي أصدرت الموافقة.")
             .Require(req.Date is not null, "date", "أدخل تاريخ الموافقة.");
        v.ThrowIfInvalid();
        r.FinanceApprovalStatus = req.Status!;
        r.FinanceApprovalSource = req.Status == "none" ? null : req.Source!.Trim();
        r.FinanceApprovalDate = req.Status == "none" ? null : req.Date;
        r.FinanceApprovalAmount = req.Status == "none" ? null : req.Amount;
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "finance_approval",
            req.Status == "none" ? "أُزيلت موافقة التمويل المسجلة" : $"سُجلت {(req.Status == "approved" ? "موافقة تمويل" : "موافقة مبدئية")} من {r.FinanceApprovalSource}", visible: true);
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> NoteBuyer(string reference, NoteInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        Need(rc, P.MarketEdit);
        var note = req.Note?.Trim() ?? "";
        if (note.Length < 2) Validate.Throw("note", "اكتب الملاحظة.");
        var r = await LoadBuyerAsync(db, rc, reference, P.MarketEdit);
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "note", "ملاحظة داخلية", visible: false, body: note[..Math.Min(note.Length, 2000)]);
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }

    // ── Contact messages ──

    /// <summary>Visitor messages belong to no case: only members who follow all work (market.follow, all) see them.</summary>
    private static async Task<IResult> ListContact(RahoonDbContext db, RequestContext rc, string? status)
    {
        if (!rc.HasAll(P.MarketFollow)) throw new ForbiddenException();
        var query = db.ContactMessages.AsQueryable();
        if (status == "new") query = query.Where(c => c.Status == ContactMessageStatus.New);
        var rows = await query.OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync();
        return Results.Ok(rows.Select(c => new
        {
            c.Reference, c.Name, c.PhoneMasked, c.Topic, c.Message, status = c.Status, c.CreatedAt, c.HandledNote, c.HandledByLabel, c.HandledAt,
        }));
    }

    private static async Task<IResult> HandledContact(string reference, NoteInput req, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        if (!rc.HasAll(P.MarketFollow)) throw new ForbiddenException();
        var c = await db.ContactMessages.FirstOrDefaultAsync(x => x.Reference == reference) ?? throw new NotFoundException();
        c.Status = ContactMessageStatus.Handled;
        c.HandledNote = req.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 500)] : null;
        c.HandledAt = clock.UtcNow;
        c.HandledByUserId = rc.UserId;
        c.HandledByLabel = rc.UserName;
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }
}
