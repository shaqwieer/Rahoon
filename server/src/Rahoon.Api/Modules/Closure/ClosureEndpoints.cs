using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Referral;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Closure;

public sealed record CreateReconciliationRequest(string? Basis, decimal? ExpectedAmount, string? ExpectedSource);
public sealed record ReconciliationLineRequest(string Kind, string Label, decimal Amount, string? Reference, string? SourceType, DateOnly? ValueDate);
public sealed record ExplanationRequest(string Explanation);
public sealed record SubmitReconciliationRequest(string? Note);
public sealed record ChainDecisionRequest(string Decision, string Reason);
public sealed record FeeRequest(string Label, decimal Amount, string BasisRef);
public sealed record CreateDistributionRequest(List<FeeRequest>? OtherFees, string? SurplusDestinationMasked);
public sealed record ExecutedLineRequest(Guid LineId, string TxnRef, DateOnly ExecutedOn);
public sealed record ExecuteDistributionRequest(List<ExecutedLineRequest> Lines);
public sealed record ClosureDocumentUpdateRequest(string? ExternalReference, bool? ShareWithOwner, bool? MarkReady);
public sealed record ClosureRequestBody(string Note);
public sealed record ClosureDecisionBody(string Decision, string Reason, bool TraceAcknowledged);

/// <summary>
/// Reconciliation, distribution, release documents and traceable closure (L26 manual → F01–F04).
/// Preparer ≠ reviewer ≠ approver on reconciliation and distribution; closure needs a second person, MFA and
/// the `close` transition guards. Closed cases are immutable; owner documents are published at closure only.
/// </summary>
public static class ClosureEndpoints
{
    private static readonly string[] LineKinds = ["receipt", "cost", "lender_share", "surplus", "waiver", "info"];
    private static readonly string[] SourceTypes = ["bank", "official", "core_banking", "internal"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequireOrg(OrganizationKind.Lender).RequirePermission(P.CaseView);
        g.MapGet("/reconciliation", GetReconciliation);
        g.MapPost("/reconciliation", CreateReconciliation).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/reconciliation/lines", AddLine).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapDelete("/reconciliation/lines/{lineId:guid}", RemoveLine).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPut("/reconciliation/explanation", Explain).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/reconciliation/submit", SubmitReconciliation).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/reconciliation/review", ReviewReconciliation).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/reconciliation/approve", ApproveReconciliation).RequirePermission(P.ReconciliationApprove).Idempotent();

        g.MapGet("/distribution", GetDistribution);
        g.MapPost("/distribution", CreateDistribution).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/distribution/submit", SubmitDistribution).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/distribution/check", CheckDistribution).RequirePermission(P.DistributionApprove).Idempotent();
        g.MapPost("/distribution/approve", ApproveDistribution).RequirePermission(P.DistributionApprove).Idempotent();
        g.MapPost("/distribution/execute", ExecuteDistribution).RequirePermission(P.ReconciliationPrepare).Idempotent();

        g.MapGet("/closure", Overview);
        g.MapGet("/closure/trace", Trace);
        g.MapGet("/closure/documents", Documents);
        g.MapPost("/closure/documents/init", InitDocuments).RequireAnyPermission(P.ReconciliationPrepare, P.ReferralInitiate, P.AgreementPrepare).Idempotent();
        g.MapPost("/closure/documents/{id:guid}/file", UploadDocument).RequirePermission(P.DocumentUpload).DisableAntiforgery();
        g.MapPut("/closure/documents/{id:guid}", UpdateDocument).RequireAnyPermission(P.ReconciliationPrepare, P.ReferralInitiate, P.AgreementPrepare).Idempotent();
        g.MapPost("/closure/documents/owner-summary", GenerateOwnerSummary).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/closure/request", RequestClosure).RequirePermission(P.ReconciliationPrepare).Idempotent();
        g.MapPost("/closure/decision", DecideClosure).RequirePermission(P.CaseClose).Idempotent();
    }

    private static DomainException SeparationOfDuties(string message) => new("separation_of_duties", message, StatusCodes.Status403Forbidden);

    private static void EnsureOpen(Case c)
    {
        if (c.Status == CaseStatus.Closed) throw new ConflictException("case_closed", "الحالة مغلقة ولا يمكن تعديلها.");
    }

    private static async Task<Dictionary<Guid, string>> NamesAsync(RahoonDbContext db, params Guid?[] ids)
    {
        var set = ids.Where(i => i is { } v && v != Guid.Empty).Select(i => i!.Value).Distinct().ToList();
        return await db.Users.Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    // ───────── F01 / L26 reconciliation ─────────

    private static async Task<object?> ReconciliationViewAsync(RahoonDbContext db, Reconciliation? r)
    {
        if (r is null) return null;
        var n = await NamesAsync(db, r.PreparedByUserId, r.ReviewedByUserId, r.ApprovedByUserId);
        string? Name(Guid? id) => id is { } v ? n.GetValueOrDefault(v) : null;
        return new
        {
            r.Id, r.Basis, basisLabel = ClosureService.BasisLabel(r.Basis), status = r.Status.ToString(), r.Version,
            r.ExpectedAmount, r.ExpectedSource, r.ReceivedAmount, r.WaivedAmount, r.Difference, r.DifferenceExplanation,
            differenceTone = r.Difference == 0 ? "ok" : string.IsNullOrWhiteSpace(r.DifferenceExplanation) ? "err" : "warn",
            differenceLabel = r.Difference == 0 ? "الفرق 0.00" : $"فرق {Math.Abs(r.Difference):N2}",
            lines = r.Lines.OrderBy(l => l.Kind == "receipt" ? 0 : l.Kind == "waiver" ? 1 : 2).ThenBy(l => l.ValueDate)
                .Select(l => new { l.Id, l.Kind, l.Label, l.Amount, l.Reference, l.MatchStatus, l.SourceType, l.ValueDate, countsIn = l.Kind is "receipt" ? "received" : l.Kind is "waiver" ? "waived" : "none" }),
            formula = "الفرق = المتوقع − المستلم − المتنازل عنه (يجب أن يكون صفراً أو مفسَّراً)",
            chain = new object[]
            {
                new { role = "المُعِدّ", user = r.SubmittedAt is null ? null : Name(r.PreparedByUserId), status = r.SubmittedAt is null ? "pending" : "done", at = r.SubmittedAt },
                new { role = "المدقق", user = Name(r.ReviewedByUserId), status = r.ReviewedAt is not null ? "done" : r.Status == ReconciliationStatus.Submitted ? "in_progress" : "pending", at = r.ReviewedAt, reason = r.ReviewReason },
                new { role = "المعتمد", user = Name(r.ApprovedByUserId), status = r.ApprovedAt is not null ? "done" : r.Status == ReconciliationStatus.Reviewed ? "in_progress" : "pending", at = r.ApprovedAt, reason = r.ApprovalReason },
            },
            r.ReturnReason, r.Note,
        };
    }

    private static async Task<IResult> GetReconciliation(string reference, CaseAccess access, ClosureService svc, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        return Results.Ok(new { caseStatus = CaseStatusInfo.Key(c.Status), reconciliation = await ReconciliationViewAsync(db, await svc.CurrentReconciliationAsync(c.Id, track: false)) });
    }

    private static async Task<IResult> CreateReconciliation(string reference, CreateReconciliationRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        if (c.Status != CaseStatus.AwaitingReconciliation)
            throw new DomainException("not_eligible", "التسوية المالية تبدأ عندما تكون الحالة «بانتظار التسوية المالية».", StatusCodes.Status409Conflict);
        var existing = await svc.CurrentReconciliationAsync(c.Id);
        if (existing is not null) throw new ConflictException("reconciliation_exists", "توجد تسوية مالية لهذه الحالة؛ عدّلها بدلاً من إنشاء أخرى.");

        var sale = await db.Set<SaleResult>().AsNoTracking().FirstOrDefaultAsync(s => s.CaseId == c.Id && s.Status == SaleResultStatus.Confirmed);
        Reconciliation r;
        if (sale is not null)
        {
            // F01: price and costs only from the officially confirmed result (not the agent's report).
            var price = sale.OfficialSalePrice!.Value;
            var costs = sale.DeclaredCosts ?? 0m;
            r = new Reconciliation
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, Basis = "judicial_sale", ExpectedAmount = price - costs, PreparedByUserId = rc.UserId,
                ExpectedSource = $"الصافي المتوقع = ثمن البيع الرسمي − تكاليف الإجراء · {sale.ConfirmationSource}",
            };
            r.Lines.Add(new ReconciliationLine { ReconciliationId = r.Id, Kind = "info", Label = "ثمن البيع الرسمي", Amount = price, Reference = sale.ConfirmationSource, SourceType = "official", ValueDate = sale.SaleMinutesDate });
            r.Lines.Add(new ReconciliationLine { ReconciliationId = r.Id, Kind = "cost", Label = "تكاليف الإجراء", Amount = costs, Reference = sale.ConfirmationSource, SourceType = "official", ValueDate = sale.OfficialConfirmationDate });
        }
        else
        {
            new Validator().Require(req.Basis is "settlement" or "voluntary_sale" or "discounted_payoff", "basis", "اختر أساس التسوية.")
                .Require(req.ExpectedAmount is > 0, "expectedAmount", "المبلغ المتوقع مطلوب.")
                .Require(!string.IsNullOrWhiteSpace(req.ExpectedSource), "expectedSource", "مصدر المبلغ المتوقع مطلوب (مثل: الاتفاق AGR-…).").ThrowIfInvalid();
            r = new Reconciliation
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, Basis = req.Basis!, ExpectedAmount = req.ExpectedAmount!.Value, ExpectedSource = req.ExpectedSource!.Trim(), PreparedByUserId = rc.UserId,
            };
        }
        ClosureService.Recompute(r);
        db.Reconciliations.Add(r);
        await audit.RecordAsync(new AuditEntry("reconciliation.created", $"بدء التسوية المالية ({ClosureService.BasisLabel(r.Basis)})", c.Id, c.Reference,
            Detail: $"المتوقع {r.ExpectedAmount:N2} · {r.ExpectedSource}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await ReconciliationViewAsync(db, r));
    }

    private static async Task<(Case, Reconciliation)> EditableAsync(string reference, CaseAccess access, ClosureService svc)
    {
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var r = await svc.CurrentReconciliationAsync(c.Id) ?? throw new ConflictException("no_reconciliation", "ابدأ التسوية المالية أولاً.");
        if (!ClosureService.Editable(r)) throw new ConflictException("reconciliation_locked", "أُرسلت التسوية للتدقيق؛ لا يمكن تعديلها إلا بعد إعادتها.");
        return (c, r);
    }

    private static async Task<IResult> AddLine(string reference, ReconciliationLineRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db, AuditLog audit)
    {
        var bankRef = req.Reference?.Trim();
        new Validator().Require(LineKinds.Contains(req.Kind), "kind", "نوع البند غير معروف.")
            .Require(!string.IsNullOrWhiteSpace(req.Label) && req.Label.Length <= 200, "label", "وصف البند مطلوب.")
            .Require(req.Amount > 0, "amount", "المبلغ يجب أن يكون أكبر من صفر.")
            .Require(req.Kind != "receipt" || bankRef is { Length: >= 6 and <= 40 }, "reference", "كل مبلغ مستلم يحتاج مرجع التحويل البنكي.")
            .Require(req.Kind != "waiver" || !string.IsNullOrWhiteSpace(bankRef), "reference", "التنازل يحتاج مرجع قرار اعتماده.")
            .Require(req.SourceType is null || SourceTypes.Contains(req.SourceType), "sourceType", "نوع المصدر غير معروف.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await EditableAsync(reference, access, svc);
        if (req.Kind == "receipt")
        {
            var normalized = bankRef!.ToUpperInvariant();
            var used = await db.Reconciliations.Where(x => x.OrganizationId == c.OrganizationId).SelectMany(x => x.Lines).AnyAsync(l => l.Kind == "receipt" && l.Reference == normalized);
            if (used) throw new ValidationFailedException(new Dictionary<string, string[]> { ["reference"] = ["المرجع مستخدم في تسوية أخرى."] });
            bankRef = normalized;
        }
        var line = new ReconciliationLine
        {
            ReconciliationId = r.Id, Kind = req.Kind, Label = req.Label.Trim(), Amount = req.Amount, Reference = bankRef, ValueDate = req.ValueDate,
            SourceType = req.SourceType ?? (req.Kind == "receipt" ? "bank" : "internal"), MatchStatus = req.Kind == "receipt" ? "matched" : "n/a",
        };
        r.Lines.Add(line);
        db.ReconciliationLines.Add(line);
        ClosureService.Recompute(r);
        await audit.RecordAsync(new AuditEntry("reconciliation.line_added", $"إضافة بند للتسوية: {line.Label}", c.Id, c.Reference,
            Detail: $"{line.Amount:N2} · {line.Reference ?? "—"} · الفرق الآن {r.Difference:N2}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await ReconciliationViewAsync(db, r));
    }

    private static async Task<IResult> RemoveLine(string reference, Guid lineId, CaseAccess access, ClosureService svc, RahoonDbContext db, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await EditableAsync(reference, access, svc);
        var line = r.Lines.FirstOrDefault(l => l.Id == lineId) ?? throw new NotFoundException();
        r.Lines.Remove(line);
        db.ReconciliationLines.Remove(line);
        ClosureService.Recompute(r);
        await audit.RecordAsync(new AuditEntry("reconciliation.line_removed", $"حذف بند من التسوية: {line.Label}", c.Id, c.Reference, Detail: $"{line.Amount:N2} · {line.Reference ?? "—"}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await ReconciliationViewAsync(db, r));
    }

    private static async Task<IResult> Explain(string reference, ExplanationRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db, AuditLog audit)
    {
        new Validator().Require(req.Explanation is null || req.Explanation.Length <= 2000, "explanation", "حتى 2000 حرف.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, r) = await EditableAsync(reference, access, svc);
        r.DifferenceExplanation = string.IsNullOrWhiteSpace(req.Explanation) ? null : req.Explanation.Trim();
        await audit.RecordAsync(new AuditEntry("reconciliation.explanation", "تفسير فرق المطابقة", c.Id, c.Reference, Detail: r.DifferenceExplanation ?? "حُذف التفسير", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await ReconciliationViewAsync(db, r));
    }

    private static async Task<IResult> SubmitReconciliation(string reference, SubmitReconciliationRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var (c, r) = await EditableAsync(reference, access, svc);
        var problems = new List<string>();
        if (!r.Lines.Any(l => l.Kind == "receipt")) problems.Add("لا توجد مبالغ مستلمة بمراجع.");
        if (r.Difference != 0 && string.IsNullOrWhiteSpace(r.DifferenceExplanation)) problems.Add($"فرق غير مفسَّر: {r.Difference:N2}");
        if (problems.Count > 0)
        {
            await audit.RecordBlockedAsync(new AuditEntry("reconciliation.submit_blocked", "إرسال تسوية غير متوازنة للتدقيق", c.Id, c.Reference,
                Detail: "المانع: " + string.Join(" · ", problems), Blocked: true, OrganizationId: c.OrganizationId));
            throw new DomainException("unbalanced", "لا يمكن الإرسال للتدقيق قبل موازنة المطابقة أو تفسير الفرق.", StatusCodes.Status422UnprocessableEntity, problems);
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        r.Status = ReconciliationStatus.Submitted;
        r.PreparedByUserId = rc.UserId;
        r.SubmittedAt = clock.UtcNow;
        r.Note = req.Note?.Trim();
        r.ReviewedByUserId = null; r.ReviewedAt = null; r.ReviewReason = null; r.ReturnReason = null;
        var reviewers = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.UserId != rc.UserId
            && m.Roles.Any(x => x.Role!.Permissions.Any(p => p.PermissionKey == P.ReconciliationPrepare))).Select(m => m.UserId).ToListAsync();
        foreach (var u in reviewers)
            notifier.Notify(u, c.OrganizationId, "reconciliation", $"تسوية مالية بانتظار التدقيق · {c.Reference}", $"الفرق {r.Difference:N2}", $"/cases/{c.Reference}/closure", c.Id);
        await audit.RecordAsync(new AuditEntry("reconciliation.submitted", "إرسال التسوية المالية للتدقيق", c.Id, c.Reference,
            Detail: $"المتوقع {r.ExpectedAmount:N2} · المستلم {r.ReceivedAmount:N2} · الفرق {r.Difference:N2}", Reason: r.Note, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await ReconciliationViewAsync(db, r));
    }

    private static async Task<IResult> ReviewReconciliation(string reference, ChainDecisionRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = ValidateDecision(req);
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var r = await svc.CurrentReconciliationAsync(c.Id) ?? throw new NotFoundException();
        if (r.Status != ReconciliationStatus.Submitted) throw new ConflictException("not_submitted", "التسوية ليست بانتظار التدقيق.");
        if (r.PreparedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("reconciliation.review_blocked", "محاولة تدقيق التسوية من مُعِدّها", c.Id, c.Reference,
                Detail: "المانع: المُعِدّ ≠ المدقق.", Blocked: true, OrganizationId: c.OrganizationId));
            throw SeparationOfDuties("أعددت هذه التسوية؛ التدقيق لموظف مالية آخر.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        var reason = req.Reason.Trim();
        if (decision == "approve")
        {
            r.Status = ReconciliationStatus.Reviewed;
            r.ReviewedByUserId = rc.UserId;
            r.ReviewedAt = clock.UtcNow;
            r.ReviewReason = reason;
        }
        else
        {
            r.Status = ReconciliationStatus.Returned;
            r.ReturnReason = reason;
            notifier.Notify(r.PreparedByUserId, c.OrganizationId, "reconciliation", $"أُعيدت التسوية المالية · {c.Reference}", reason, $"/cases/{c.Reference}/closure", c.Id, "warn");
        }
        await audit.RecordAsync(new AuditEntry("reconciliation.reviewed", decision == "approve" ? "تدقيق التسوية المالية (مكتمل)" : "إعادة التسوية المالية للمُعِدّ",
            c.Id, c.Reference, Reason: reason, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await ReconciliationViewAsync(db, r));
    }

    private static async Task<IResult> ApproveReconciliation(string reference, ChainDecisionRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = ValidateDecision(req);
        EndpointAccess.EnsureStepUp(rc, clock);
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var r = await svc.CurrentReconciliationAsync(c.Id) ?? throw new NotFoundException();
        if (r.Status != ReconciliationStatus.Reviewed) throw new ConflictException("not_reviewed", "التسوية لم يكتمل تدقيقها بعد.");
        if (r.PreparedByUserId == rc.UserId || r.ReviewedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("reconciliation.approval_blocked", "محاولة اعتماد التسوية من المُعِدّ أو المدقق", c.Id, c.Reference,
                Detail: "المانع: مُعِدّ ≠ مدقق ≠ معتمد.", Blocked: true, OrganizationId: c.OrganizationId));
            throw SeparationOfDuties("المُعِدّ ≠ المدقق ≠ المعتمد: الاعتماد لشخص ثالث.");
        }
        if (decision == "approve" && r.Difference != 0 && string.IsNullOrWhiteSpace(r.DifferenceExplanation))
            throw new DomainException("unbalanced", "فرق غير مفسَّر في المطابقة.", StatusCodes.Status422UnprocessableEntity);
        await using var tx = await db.Database.BeginTransactionAsync();
        var reason = req.Reason.Trim();
        if (decision == "approve")
        {
            r.Status = ReconciliationStatus.Approved;
            r.ApprovedByUserId = rc.UserId;
            r.ApprovedAt = clock.UtcNow;
            r.ApprovalReason = reason;
        }
        else
        {
            r.Status = ReconciliationStatus.Returned;
            r.ReturnReason = reason;
            notifier.Notify(r.PreparedByUserId, c.OrganizationId, "reconciliation", $"أُعيدت التسوية المالية · {c.Reference}", reason, $"/cases/{c.Reference}/closure", c.Id, "warn");
        }
        await audit.RecordAsync(new AuditEntry("reconciliation.decision", decision == "approve" ? "اعتماد التسوية المالية" : "إعادة التسوية المالية",
            c.Id, c.Reference, Reason: reason, Detail: $"الفرق {r.Difference:N2} · تأكيد برمز التحقق", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await ReconciliationViewAsync(db, r));
    }

    private static string ValidateDecision(ChainDecisionRequest req)
    {
        var decision = req.Decision?.ToLowerInvariant() ?? "";
        new Validator().Require(decision is "approve" or "return", "decision", "اختر: اعتماد أو إعادة.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "السبب إلزامي.").ThrowIfInvalid();
        return decision;
    }

    // ───────── F02 distribution ─────────

    private static async Task<object?> DistributionViewAsync(RahoonDbContext db, Distribution? d)
    {
        if (d is null) return null;
        var n = await NamesAsync(db, d.PreparedByUserId, d.CheckedByUserId, d.ApprovedByUserId);
        string? Name(Guid? id) => id is { } v ? n.GetValueOrDefault(v) : null;
        var pct = (decimal x) => d.NetProceeds == 0 ? 0 : Math.Round(x / d.NetProceeds * 100, 2);
        return new
        {
            d.Id, status = d.Status.ToString(), d.Version, d.SalePrice, d.ProcedureCosts, d.NetProceeds, d.DebtAmount, d.DebtBasisRef,
            d.LenderShare, d.OtherFees, d.OwnerSurplus, d.Shortfall, d.SurplusDestinationMasked,
            lines = d.Lines.OrderBy(l => l.Seq).Select(l => new { l.Id, l.Seq, l.Type, l.Label, l.Amount, l.BasisRef, l.DestinationMasked, l.ExecutedTxnRef, l.ExecutedOn, percent = pct(l.Amount) }),
            barAlt = $"التوزيع: {d.LenderShare:N0} للمصرف، {d.OwnerSurplus:N0} للمالك، من صافي {d.NetProceeds:N0}",
            order = "التكاليف (مخصومة مسبقاً) ← المديونية ← الرسوم الأخرى المعتمدة ← الفائض للمالك",
            chain = new object[]
            {
                new { role = "المُعِدّة", user = Name(d.PreparedByUserId), status = d.SubmittedAt is null ? "pending" : "done", at = d.SubmittedAt },
                new { role = "المدقق", user = Name(d.CheckedByUserId), status = d.CheckedAt is not null ? "done" : d.Status == DistributionStatus.InReview ? "in_progress" : "pending", at = d.CheckedAt, reason = d.CheckReason },
                new { role = "المعتمد", user = Name(d.ApprovedByUserId), status = d.ApprovedAt is not null ? "done" : d.Status == DistributionStatus.InApproval ? "in_progress" : "pending", at = d.ApprovedAt, reason = d.ApprovalReason },
            },
            rule = "الفائض للمالك يُحوَّل إلى حسابه المتحقق منه فقط، عبر القنوات النظامية، ويُسجّل مرجعه.",
            d.ReturnReason, d.ExecutedAt,
        };
    }

    private static async Task<IResult> GetDistribution(string reference, CaseAccess access, ClosureService svc, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        return Results.Ok(new { distribution = await DistributionViewAsync(db, await svc.CurrentDistributionAsync(c.Id, track: false)) });
    }

    private static async Task<IResult> CreateDistribution(string reference, CreateDistributionRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, AuditLog audit)
    {
        var fees = req.OtherFees ?? [];
        new Validator().Require(fees.All(f => f.Amount > 0 && !string.IsNullOrWhiteSpace(f.Label) && !string.IsNullOrWhiteSpace(f.BasisRef)), "otherFees", "لكل رسم وصف ومبلغ ومرجع اعتماد.")
            .Require(req.SurplusDestinationMasked is null || req.SurplusDestinationMasked.Length <= 40, "surplusDestinationMasked", "رقم الحساب المقنّع طويل.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var r = await svc.CurrentReconciliationAsync(c.Id, track: false) ?? throw new ConflictException("no_reconciliation", "ابدأ التسوية المالية أولاً.");
        if (!ClosureService.SaleBases.Contains(r.Basis)) throw new ConflictException("not_applicable", "التوزيع يخص حصيلة البيع فقط.");
        if (r.Difference != 0) throw new DomainException("unbalanced", $"الفرق في المطابقة {r.Difference:N2}؛ يجب أن يكون 0.00 قبل التوزيع.", StatusCodes.Status422UnprocessableEntity);
        var existing = await svc.CurrentDistributionAsync(c.Id);
        if (existing is not null && existing.Status != DistributionStatus.Returned) throw new ConflictException("distribution_exists", "يوجد توزيع قائم لهذه الحالة.");

        decimal price, costs;
        if (r.Basis == "judicial_sale")
        {
            var sale = await db.Set<SaleResult>().AsNoTracking().FirstOrDefaultAsync(s => s.CaseId == c.Id && s.Status == SaleResultStatus.Confirmed)
                       ?? throw new ConflictException("no_confirmed_result", "لا توجد نتيجة بيع مؤكدة رسمياً.");
            price = sale.OfficialSalePrice!.Value;
            costs = sale.DeclaredCosts ?? 0m;
        }
        else
        {
            costs = r.Lines.Where(l => l.Kind == "cost").Sum(l => l.Amount);
            price = r.ExpectedAmount + costs;
        }
        var debt = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync()
                   ?? throw new ConflictException("no_debt_statement", "لا يوجد كشف مديونية معتمد.");
        var w = Waterfall.Compute(price, costs, r.ReceivedAmount, debt.Total, fees.Select(f => new FeeInput(f.Label.Trim(), f.Amount, f.BasisRef.Trim())).ToList());
        if (!w.Balanced) throw new DomainException("unbalanced", "المستلم لا يساوي الصافي المتوقع.", StatusCodes.Status422UnprocessableEntity);

        var basis = $"كشف المديونية المعتمد {debt.AsOf.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}";
        var dist = new Distribution
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, ReconciliationId = r.Id, SalePrice = price, ProcedureCosts = costs, NetProceeds = w.ExpectedNet,
            DebtAmount = debt.Total, DebtBasisRef = basis, LenderShare = w.LenderShare, OtherFees = w.OtherFees, OwnerSurplus = w.OwnerSurplus, Shortfall = w.Shortfall,
            SurplusDestinationMasked = req.SurplusDestinationMasked?.Trim(), PreparedByUserId = rc.UserId,
        };
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == c.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var seq = 0;
        dist.Lines.Add(new DistributionLine { OrganizationId = c.OrganizationId, DistributionId = dist.Id, Seq = ++seq, Type = "debt_repayment", Label = $"سداد المديونية لـ{org}", Amount = w.LenderShare, BasisRef = basis });
        foreach (var f in w.FeesApplied)
            dist.Lines.Add(new DistributionLine { OrganizationId = c.OrganizationId, DistributionId = dist.Id, Seq = ++seq, Type = "other_fee", Label = f.Label, Amount = f.Amount, BasisRef = f.BasisRef });
        dist.Lines.Add(new DistributionLine
        {
            OrganizationId = c.OrganizationId, DistributionId = dist.Id, Seq = ++seq, Type = "owner_surplus", Label = "الفائض للمالك", Amount = w.OwnerSurplus,
            BasisRef = "الصافي − المديونية − الرسوم", DestinationMasked = dist.SurplusDestinationMasked,
        });
        db.Set<Distribution>().Add(dist);
        await audit.RecordAsync(new AuditEntry("distribution.prepared", "إعداد التوزيع المقترح", c.Id, c.Reference,
            Detail: $"الصافي {w.ExpectedNet:N2} · للمصرف {w.LenderShare:N2} · رسوم {w.OtherFees:N2} · الفائض للمالك {w.OwnerSurplus:N2}" + (w.Shortfall > 0 ? $" · عجز {w.Shortfall:N2}" : ""),
            OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await DistributionViewAsync(db, dist));
    }

    private static async Task<(Case, Distribution)> DistributionAsync(string reference, CaseAccess access, ClosureService svc)
    {
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var d = await svc.CurrentDistributionAsync(c.Id) ?? throw new NotFoundException();
        return (c, d);
    }

    private static async Task<IResult> SubmitDistribution(string reference, CaseAccess access, ClosureService svc, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, d) = await DistributionAsync(reference, access, svc);
        if (d.Status is not (DistributionStatus.Draft or DistributionStatus.Returned)) throw new ConflictException("not_draft", "التوزيع أُرسل مسبقاً.");
        var r = await svc.CurrentReconciliationAsync(c.Id, track: false);
        if (r is null || r.Difference != 0) throw new DomainException("unbalanced", "الفرق في المطابقة يجب أن يكون 0.00.", StatusCodes.Status422UnprocessableEntity);
        if (d.OwnerSurplus > 0 && string.IsNullOrWhiteSpace(d.SurplusDestinationMasked))
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["surplusDestinationMasked"] = ["حدد الحساب المتحقق منه للمالك قبل الإرسال."] });
        d.Status = DistributionStatus.InReview;
        d.PreparedByUserId = rc.UserId;
        d.SubmittedAt = clock.UtcNow;
        d.CheckedByUserId = null; d.CheckedAt = null; d.CheckReason = null; d.ReturnReason = null;
        await audit.RecordAsync(new AuditEntry("distribution.submitted", "إرسال التوزيع للتدقيق", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await DistributionViewAsync(db, d));
    }

    private static async Task<IResult> CheckDistribution(string reference, ChainDecisionRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        var decision = ValidateDecision(req);
        var (c, d) = await DistributionAsync(reference, access, svc);
        if (d.Status != DistributionStatus.InReview) throw new ConflictException("not_in_review", "التوزيع ليس بانتظار التدقيق.");
        if (d.PreparedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("distribution.check_blocked", "محاولة تدقيق التوزيع من مُعِدّته", c.Id, c.Reference, Detail: "المانع: مُعِدّة ≠ مدقق.", Blocked: true, OrganizationId: c.OrganizationId));
            throw SeparationOfDuties("أعددت هذا التوزيع؛ التدقيق لشخص آخر.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        if (decision == "approve") { d.Status = DistributionStatus.InApproval; d.CheckedByUserId = rc.UserId; d.CheckedAt = clock.UtcNow; d.CheckReason = req.Reason.Trim(); }
        else { d.Status = DistributionStatus.Returned; d.ReturnReason = req.Reason.Trim(); }
        await audit.RecordAsync(new AuditEntry("distribution.checked", decision == "approve" ? "تدقيق التوزيع (مكتمل)" : "إعادة التوزيع للمُعِدّة", c.Id, c.Reference, Reason: req.Reason.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await DistributionViewAsync(db, d));
    }

    private static async Task<IResult> ApproveDistribution(string reference, ChainDecisionRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        var decision = ValidateDecision(req);
        EndpointAccess.EnsureStepUp(rc, clock);
        var (c, d) = await DistributionAsync(reference, access, svc);
        if (d.Status != DistributionStatus.InApproval) throw new ConflictException("not_in_approval", "التوزيع ليس بانتظار الاعتماد.");
        if (d.PreparedByUserId == rc.UserId || d.CheckedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("distribution.approval_blocked", "محاولة اعتماد التوزيع من المُعِدّة أو المدقق", c.Id, c.Reference, Detail: "المانع: مُعِدّة ≠ مدقق ≠ معتمد.", Blocked: true, OrganizationId: c.OrganizationId));
            throw SeparationOfDuties("مُعِدّة ≠ مدقق ≠ معتمد: الاعتماد لشخص ثالث.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        if (decision == "approve") { d.Status = DistributionStatus.Approved; d.ApprovedByUserId = rc.UserId; d.ApprovedAt = clock.UtcNow; d.ApprovalReason = req.Reason.Trim(); }
        else { d.Status = DistributionStatus.Returned; d.ReturnReason = req.Reason.Trim(); }
        await audit.RecordAsync(new AuditEntry("distribution.decision", decision == "approve" ? "اعتماد التوزيع" : "إعادة التوزيع", c.Id, c.Reference,
            Reason: req.Reason.Trim(), Detail: "تأكيد برمز التحقق", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await DistributionViewAsync(db, d));
    }

    private static async Task<IResult> ExecuteDistribution(string reference, ExecuteDistributionRequest req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(req.Lines is { Count: > 0 }, "lines", "أدخل مراجع التحويل.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, d) = await DistributionAsync(reference, access, svc);
        if (d.Status != DistributionStatus.Approved) throw new ConflictException("not_approved", "التنفيذ بعد اعتماد التوزيع.");
        foreach (var x in req.Lines)
        {
            var line = d.Lines.FirstOrDefault(l => l.Id == x.LineId) ?? throw new NotFoundException();
            if (string.IsNullOrWhiteSpace(x.TxnRef) || x.TxnRef.Trim().Length is < 6 or > 40) Validate.Throw("lines", "مرجع التحويل غير صالح.");
            if (x.ExecutedOn > clock.TodayRiyadh) Validate.Throw("lines", "تاريخ التنفيذ لا يكون في المستقبل.");
            line.ExecutedTxnRef = x.TxnRef.Trim().ToUpperInvariant();
            line.ExecutedOn = x.ExecutedOn;
        }
        var missing = d.Lines.Where(l => l.Amount > 0 && l.ExecutedTxnRef is null).Select(l => l.Label).ToList();
        if (missing.Count > 0) throw new DomainException("missing_refs", "كل بند منفَّذ يحتاج مرجع تحويل.", StatusCodes.Status422UnprocessableEntity, missing);
        d.Status = DistributionStatus.Executed;
        d.ExecutedAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("distribution.executed", "تسجيل تنفيذ التوزيع بمراجع التحويل", c.Id, c.Reference,
            Detail: string.Join(" · ", d.Lines.Where(l => l.ExecutedTxnRef is not null).Select(l => $"{l.Label}: {l.Amount:N2} ({l.ExecutedTxnRef})")), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(await DistributionViewAsync(db, d));
    }

    // ───────── F03 release & clearance documents ─────────

    private static object DocumentRow(ClosureDocument d) => new
    {
        d.Id, d.Type, d.Title, status = d.Status == ClosureDocumentStatus.Ready ? "ready" : "pending", d.PreparedByDept, d.PreparedOn, d.DocumentVersionId,
        d.ExternalReference, d.BlocksClosure, d.ShareWithOwner, d.VisibleToOwner,
        meta = d.Status == ClosureDocumentStatus.Ready ? $"{d.PreparedByDept} · {d.PreparedOn:yyyy-MM-dd}" : d.BlocksClosure ? "غير جاهز — يحجب الإغلاق" : "قيد الانتظار — لا يحجب الإغلاق (افتراض)",
    };

    private static async Task<IResult> Documents(string reference, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var docs = await db.ClosureDocuments.AsNoTracking().Where(d => d.CaseId == c.Id).OrderBy(d => d.CreatedAt).ToListAsync();
        return Results.Ok(new { items = docs.Select(DocumentRow), ownerVisibility = "تُنشر المستندات المعلَّمة للمالك في بوابته عند الإغلاق فقط." });
    }

    private static async Task<IResult> InitDocuments(string reference, CaseAccess access, RahoonDbContext db, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var existing = await db.ClosureDocuments.Where(d => d.CaseId == c.Id).Select(d => d.Type).ToListAsync();
        var added = 0;
        foreach (var s in ClosureService.StandardDocuments.Where(s => !existing.Contains(s.Type)))
        {
            db.ClosureDocuments.Add(new ClosureDocument
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, Type = s.Type, Title = s.Title, Status = ClosureDocumentStatus.Pending,
                PreparedByDept = s.Dept, BlocksClosure = s.Blocks, ShareWithOwner = s.Share, VisibleToOwner = false,
            });
            added++;
        }
        if (added > 0) await audit.RecordAsync(new AuditEntry("closure.documents_initialized", $"تهيئة مستندات الإغلاق ({added})", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { added });
    }

    private static async Task<IResult> UploadDocument(string reference, Guid id, HttpRequest http, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IFileScanner scanner, IClock clock, AuditLog audit)
    {
        if (!http.HasFormContentType) throw new DomainException("form_required", "أرسل الملف كنموذج متعدد الأجزاء.", 400);
        var form = await http.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? throw new ValidationFailedException(new Dictionary<string, string[]> { ["file"] = ["اختر ملفاً."] });
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var d = await db.ClosureDocuments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        await using var stream = file.OpenReadStream();
        var stored = await storage.SaveAsync(stream, file.FileName, c.OrganizationId);
        var scan = await scanner.ScanAsync(stored.StorageKey);
        if (scan == ScanStatus.Infected) throw new DomainException("file_infected", "رُفض الملف: فشل فحص الأمان.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var version = await AttachFileAsync(db, rc, clock, c, d, stored, Path.GetFileName(file.FileName), scan);
        await audit.RecordAsync(new AuditEntry("closure.document_ready", $"رفع {d.Title}", c.Id, c.Reference, Detail: $"sha256:{stored.Sha256[..12]}… · فحص: {scan}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { document = DocumentRow(d), versionId = version.Id });
    }

    private static async Task<DocumentVersion> AttachFileAsync(RahoonDbContext db, RequestContext rc, IClock clock, Case c, ClosureDocument d, StoredFile stored, string fileName, ScanStatus scan)
    {
        var typeKey = d.Type is "final_clearance" or "lien_release_letter" ? d.Type : "external_official_document";
        var doc = new CaseDocument
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = typeKey, Name = d.Title, Source = d.PreparedByDept == "القانونية" ? DocumentSource.Legal : DocumentSource.Lender,
            Status = DocumentStatus.Verified, VisibleTo = ["case_team", "legal", "finance"], VisibleToOwner = false, VersionCount = 1,
        };
        db.Documents.Add(doc);
        var version = new DocumentVersion
        {
            OrganizationId = c.OrganizationId, DocumentId = doc.Id, CaseId = c.Id, VersionNo = 1, FileName = fileName, ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey, UploadedByUserId = rc.UserId, UploadedByLabel = rc.UserName,
            UploadedAt = clock.UtcNow, ScanStatus = scan, ReviewStatus = ReviewStatus.Verified, ReviewedByUserId = rc.UserId, ReviewedAt = clock.UtcNow,
        };
        db.DocumentVersions.Add(version);
        doc.CurrentVersionId = version.Id;
        d.DocumentVersionId = version.Id;
        d.Status = ClosureDocumentStatus.Ready;
        d.PreparedOn = clock.TodayRiyadh;
        await Task.CompletedTask;
        return version;
    }

    private static async Task<IResult> UpdateDocument(string reference, Guid id, ClosureDocumentUpdateRequest req, CaseAccess access, RahoonDbContext db, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var d = await db.ClosureDocuments.FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        if (req.ExternalReference is not null) d.ExternalReference = string.IsNullOrWhiteSpace(req.ExternalReference) ? null : req.ExternalReference.Trim();
        if (req.ShareWithOwner is { } share)
        {
            if (share && d.Type == "lien_release_submission_proof") Validate.Throw("shareWithOwner", "إثبات التقديم مستند داخلي ولا يُنشر للمالك.");
            d.ShareWithOwner = share;
        }
        if (req.MarkReady == true)
        {
            // Only the external submission proof can be ready without a file: it is a manual external reference.
            if (d.DocumentVersionId is null && (d.Type != "lien_release_submission_proof" || d.ExternalReference is null))
                Validate.Throw("markReady", d.Type == "lien_release_submission_proof" ? "أدخل المرجع الخارجي أولاً." : "ارفع الملف أولاً.");
            d.Status = ClosureDocumentStatus.Ready;
            d.PreparedOn ??= clock.TodayRiyadh;
        }
        await audit.RecordAsync(new AuditEntry("closure.document_updated", $"تحديث {d.Title}", c.Id, c.Reference,
            Detail: $"الحالة {(d.Status == ClosureDocumentStatus.Ready ? "جاهز" : "قيد الانتظار")} · للمالك عند الإغلاق: {(d.ShareWithOwner ? "نعم" : "لا")}" + (d.ExternalReference is null ? "" : $" · مرجع {d.ExternalReference}"),
            OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(DocumentRow(d));
    }

    private static async Task<IResult> GenerateOwnerSummary(string reference, CaseAccess access, ClosureService svc, RahoonDbContext db, RequestContext rc,
        IDocumentStorage storage, IClock clock, AuditLog audit)
    {
        var c = await access.GetAsync(reference, track: false);
        EnsureOpen(c);
        var r = await svc.CurrentReconciliationAsync(c.Id, track: false) ?? throw new ConflictException("no_reconciliation", "لا توجد تسوية مالية لتلخيصها.");
        var dist = await svc.CurrentDistributionAsync(c.Id, track: false);
        var lines = new List<string>
        {
            $"Rahoon - final case summary - {c.Reference}",
            $"Generated {clock.UtcNow.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} (Riyadh)",
            $"Basis: {r.Basis}",
            $"Expected: {r.ExpectedAmount:N2} SAR  Received: {r.ReceivedAmount:N2} SAR  Waived: {r.WaivedAmount:N2} SAR  Difference: {r.Difference:N2} SAR",
        };
        foreach (var l in r.Lines.Where(l => l.Kind == "receipt")) lines.Add($"Receipt {l.Reference}: {l.Amount:N2} SAR");
        if (dist is not null)
            lines.Add($"Distribution: lender {dist.LenderShare:N2} SAR, other fees {dist.OtherFees:N2} SAR, owner surplus {dist.OwnerSurplus:N2} SAR");
        lines.Add("The Arabic summary is shown in the owner portal.");

        await using var tx = await db.Database.BeginTransactionAsync();
        var d = await db.ClosureDocuments.FirstOrDefaultAsync(x => x.CaseId == c.Id && x.Type == "owner_final_summary");
        if (d is null)
        {
            var s = ClosureService.StandardDocuments.First(x => x.Type == "owner_final_summary");
            d = new ClosureDocument { OrganizationId = c.OrganizationId, CaseId = c.Id, Type = s.Type, Title = s.Title, PreparedByDept = s.Dept, BlocksClosure = true, ShareWithOwner = true };
            db.ClosureDocuments.Add(d);
        }
        var stored = await storage.SaveAsync(new MemoryStream(SimplePdf.Render(lines)), $"summary-{c.Reference}.pdf", c.OrganizationId);
        var version = await AttachFileAsync(db, rc, clock, c, d, stored, $"summary-{c.Reference}.pdf", ScanStatus.Skipped);
        await audit.RecordAsync(new AuditEntry("closure.owner_summary_generated", "توليد ملخص الحالة النهائي للمالك", c.Id, c.Reference, Detail: $"sha256:{stored.Sha256[..12]}…", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { document = DocumentRow(d), versionId = version.Id });
    }

    // ───────── F04 traceable closure ─────────

    private static async Task<IResult> Trace(string reference, CaseAccess access, ClosureService svc)
    {
        var c = await access.GetAsync(reference, track: false);
        var trace = await svc.TraceAsync(c, await svc.CurrentReconciliationAsync(c.Id, track: false), await svc.CurrentDistributionAsync(c.Id, track: false));
        return Results.Ok(new { items = trace, missingSources = trace.Count(t => !t.HasSource), rule = "كل رقم بوسم مصدره: رسمي، بنكي، نظام التمويل، داخلي." });
    }

    private static async Task<IResult> Overview(string reference, CaseAccess access, ClosureService svc, RahoonDbContext db, RequestContext rc, CaseWorkflow workflow, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var r = await svc.CurrentReconciliationAsync(c.Id, track: false);
        var d = await svc.CurrentDistributionAsync(c.Id, track: false);
        var trace = await svc.TraceAsync(c, r, d);
        var docs = await db.ClosureDocuments.AsNoTracking().Where(x => x.CaseId == c.Id).OrderBy(x => x.CreatedAt).ToListAsync();
        var request = await db.Set<ClosureRequest>().AsNoTracking().Where(x => x.CaseId == c.Id).OrderByDescending(x => x.RequestedAt).FirstOrDefaultAsync();
        var blockers = c.Status == CaseStatus.AwaitingReconciliation
            ? (await svc.CloseBlockersAsync(c, r, d, trace)).Concat(await workflow.EvaluateGuardsAsync(c, CaseWorkflow.Def("close"))).Distinct().ToList()
            : [$"الحالة «{CaseStatusInfo.Of(c.Status).LabelAr}»."];
        var readOnlyDays = await Analytics.OperationalSettings.DaysAsync(db, c.OrganizationId, Analytics.OperationalSettings.OwnerReadOnlyDays, 90);
        var names = request is null ? new Dictionary<Guid, string>() : await NamesAsync(db, request.RequestedByUserId, request.DecidedByUserId);
        var sod = request is not null && (request.RequestedByUserId == rc.UserId || r?.PreparedByUserId == rc.UserId || r?.ReviewedByUserId == rc.UserId);

        var actions = new List<object>();
        if (rc.Has(P.ReconciliationPrepare))
            actions.Add(new { key = "request_closure", label = "إرسال للتدقيق والاعتماد", enabled = blockers.Count == 0 && request is not { Status: ClosureRequestStatus.Pending }, reasons = request is { Status: ClosureRequestStatus.Pending } ? ["طلب الإغلاق بانتظار القرار."] : blockers });
        if (rc.Has(P.CaseClose))
            actions.Add(new
            {
                key = "decide_closure", label = "اعتماد الإغلاق (رمز تحقق)",
                enabled = request is { Status: ClosureRequestStatus.Pending } && !sod && blockers.Count == 0,
                reasons = request is not { Status: ClosureRequestStatus.Pending } ? ["لا يوجد طلب إغلاق بانتظار القرار."] : sod ? ["شاركت في إعداد أو تدقيق هذه التسوية؛ الاعتماد لشخص آخر."] : blockers,
                requiresStepUp = true,
            });

        return Results.Ok(new
        {
            caseRef = c.Reference, caseStatus = CaseStatusInfo.Key(c.Status), closedAt = c.ClosedAt,
            reconciliation = await ReconciliationViewAsync(db, r),
            distribution = await DistributionViewAsync(db, d),
            documents = docs.Select(DocumentRow),
            trace, missingSources = trace.Count(t => !t.HasSource),
            request = request is null ? null : new
            {
                request.Id, status = request.Status.ToString(), request.Note, request.RequestedAt, requestedBy = names.GetValueOrDefault(request.RequestedByUserId),
                request.DecidedAt, decidedBy = request.DecidedByUserId is { } dId ? names.GetValueOrDefault(dId) : null, request.DecisionReason, request.TraceSha256,
            },
            consequences = new[]
            {
                "تنتقل الحالة إلى «مغلقة» نهائياً ولا يمكن تعديلها.",
                "يستلم المالك المخالصة وخطاب فك الرهن وملخص الحالة في بوابته.",
                $"يبقى وصول المالك للقراءة {CaseDisplay.Days(readOnlyDays)} ثم يُسحب (افتراض).",
                "تبقى الحالة الرسمية الخارجية ظاهرة للقراءة مع آخر إدخال.",
                $"تُغلق كل المهام المفتوحة ({await db.Tasks.CountAsync(t => t.CaseId == c.Id && t.Status == TaskStatus.Open)} حالياً).",
            },
            blockers, actions,
        });
    }

    private static async Task<IResult> RequestClosure(string reference, ClosureRequestBody req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Note) && req.Note.Trim().Length >= 10, "note", "ملاحظة الإغلاق إلزامية (10 أحرف على الأقل).").ThrowIfInvalid();
        var c = await access.GetAsync(reference, track: false);
        if (c.Status != CaseStatus.AwaitingReconciliation) throw new DomainException("not_eligible", "الإغلاق من «بانتظار التسوية المالية» فقط.", StatusCodes.Status409Conflict);
        var r = await svc.CurrentReconciliationAsync(c.Id, track: false);
        var d = await svc.CurrentDistributionAsync(c.Id, track: false);
        var trace = await svc.TraceAsync(c, r, d);
        var blockers = (await svc.CloseBlockersAsync(c, r, d, trace)).Concat(await workflow.EvaluateGuardsAsync(c, CaseWorkflow.Def("close"))).Distinct().ToList();
        if (blockers.Count > 0)
        {
            await audit.RecordBlockedAsync(new AuditEntry("closure.request_blocked", "طلب إغلاق محجوب", c.Id, c.Reference, Detail: "المانع: " + string.Join(" · ", blockers), Blocked: true, OrganizationId: c.OrganizationId));
            throw new DomainException("guard_failed", "لا يمكن طلب الإغلاق الآن.", StatusCodes.Status422UnprocessableEntity, blockers);
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        if (await db.Set<ClosureRequest>().AnyAsync(x => x.CaseId == c.Id && x.Status == ClosureRequestStatus.Pending))
            throw new ConflictException("already_requested", "طلب الإغلاق بانتظار القرار.");
        var json = JsonSerializer.Serialize(trace, JsonOptions.Web);
        var request = new ClosureRequest
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, ReconciliationId = r!.Id, Note = req.Note.Trim(), RequestedByUserId = rc.UserId, RequestedAt = clock.UtcNow,
            TraceJson = json, TraceSha256 = "sha256:" + Tokens.Sha256Hex(json),
        };
        db.Set<ClosureRequest>().Add(request);
        var approvers = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.UserId != rc.UserId
            && m.Roles.Any(x => x.Role!.Permissions.Any(p => p.PermissionKey == P.CaseClose))).Select(m => m.UserId).ToListAsync();
        foreach (var u in approvers.Where(u => u != r.PreparedByUserId && u != r.ReviewedByUserId))
            notifier.Notify(u, c.OrganizationId, "approval", $"طلب إغلاق بانتظار الاعتماد · {c.Reference}", request.Note, $"/cases/{c.Reference}/closure", c.Id, "warn");
        await audit.RecordAsync(new AuditEntry("closure.requested", "إرسال الإغلاق للاعتماد", c.Id, c.Reference, Reason: request.Note,
            Detail: $"تتبع {trace.Count} بنداً · {request.TraceSha256}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { request.Id, status = request.Status.ToString(), request.TraceSha256 });
    }

    private static async Task<IResult> DecideClosure(string reference, ClosureDecisionBody req, CaseAccess access, ClosureService svc, RahoonDbContext db,
        RequestContext rc, IClock clock, CaseWorkflow workflow, AuditLog audit, Notifier notifier, ISmsGateway sms, PiiProtector pii)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "approve" or "reject", "decision", "اختر القرار.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10, "reason", "سبب القرار إلزامي (10 أحرف على الأقل).")
            .Require(decision != "approve" || req.TraceAcknowledged, "traceAcknowledged", "أكّد أنك راجعت تتبع المصادر ولا يوجد رقم دون مصدر.").ThrowIfInvalid();
        EndpointAccess.EnsureStepUp(rc, clock);

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var request = await db.Set<ClosureRequest>().Where(x => x.CaseId == c.Id && x.Status == ClosureRequestStatus.Pending).FirstOrDefaultAsync()
                      ?? throw new ConflictException("no_request", "لا يوجد طلب إغلاق بانتظار القرار.");
        var r = await svc.CurrentReconciliationAsync(c.Id, track: false);
        var d = await svc.CurrentDistributionAsync(c.Id, track: false);
        var reason = req.Reason.Trim();
        if (request.RequestedByUserId == rc.UserId || r?.PreparedByUserId == rc.UserId || r?.ReviewedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("closure.decision_blocked", "محاولة اعتماد الإغلاق من مُعِدّ أو مدقق", c.Id, c.Reference, Reason: reason,
                Detail: "المانع: المعتمد ≠ مقدم الطلب ≠ مُعِدّ التسوية ≠ مدققها.", Blocked: true, OrganizationId: c.OrganizationId));
            throw SeparationOfDuties("شاركت في إعداد أو تدقيق هذا الإغلاق؛ الاعتماد لشخص آخر.");
        }

        if (decision == "reject")
        {
            request.Status = ClosureRequestStatus.Rejected;
            request.DecidedByUserId = rc.UserId;
            request.DecidedAt = clock.UtcNow;
            request.DecisionReason = reason;
            notifier.Notify(request.RequestedByUserId, c.OrganizationId, "approval", $"رُفض طلب الإغلاق · {c.Reference}", reason, $"/cases/{c.Reference}/closure", c.Id, "warn");
            await audit.RecordAsync(new AuditEntry("closure.rejected", "رفض طلب الإغلاق", c.Id, c.Reference, Reason: reason, OrganizationId: c.OrganizationId));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { status = request.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status) });
        }

        var trace = await svc.TraceAsync(c, r, d);
        var json = JsonSerializer.Serialize(trace, JsonOptions.Web);
        if ("sha256:" + Tokens.Sha256Hex(json) != request.TraceSha256)
            throw new ConflictException("trace_changed", "تغيّرت أرقام التتبع منذ إرسال الطلب. أعد إرسال الإغلاق للاعتماد.");
        var extra = await svc.CloseBlockersAsync(c, r, d, trace);
        // Guards (reconciliation approved, blocking documents ready) and step-up are enforced by the transition itself.
        await workflow.TransitionAsync(c, "close", reason, CaseStatus.AwaitingReconciliation, evidence: [$"closure_request:{request.Id}", request.TraceSha256], extraGuardFailures: extra);

        request.Status = ClosureRequestStatus.Approved;
        request.DecidedByUserId = rc.UserId;
        request.DecidedAt = clock.UtcNow;
        request.DecisionReason = reason;
        request.TraceAcknowledged = true;
        var published = 0;
        foreach (var doc in await db.ClosureDocuments.Where(x => x.CaseId == c.Id && x.ShareWithOwner && x.Status == ClosureDocumentStatus.Ready).ToListAsync())
        {
            doc.VisibleToOwner = true;
            published++;
        }
        var openTasks = await db.Tasks.Where(t => t.CaseId == c.Id && t.Status == TaskStatus.Open).ToListAsync();
        foreach (var t in openTasks) { t.Status = TaskStatus.Done; t.CompletedAt = clock.UtcNow; }

        var days = await Analytics.OperationalSettings.DaysAsync(db, c.OrganizationId, Analytics.OperationalSettings.OwnerReadOnlyDays, 90);
        var ownerAccess = await db.OwnerAccesses.AsNoTracking().FirstOrDefaultAsync(o => o.CaseId == c.Id);
        var body = $"أُغلقت حالتك. مستندات الإغلاق (المخالصة وخطاب فك الرهن وملخص حالتك) متاحة في بوابتك للاطلاع والتنزيل لمدة {CaseDisplay.Days(days)}.";
        if (ownerAccess?.UserId is { } ownerUser) notifier.Notify(ownerUser, c.OrganizationId, "closure", "أُغلقت حالتك", body, "/owner/documents/closure", c.Id, "ok");
        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        if (party?.PhoneEnc is not null) await sms.SendAsync(pii.Unprotect(party.PhoneEnc), "رهون: أُغلقت حالتك ومستنداتها متاحة في بوابتك.", c.OrganizationId, c.Id, "TPL-CLOSED-01");

        await audit.RecordAsync(new AuditEntry("closure.approved", "اعتماد الإغلاق بمصادر قابلة للتتبع", c.Id, c.Reference, Reason: reason,
            Detail: $"نُشر للمالك {published} مستندات · أُغلقت {openTasks.Count} مهام · وصول المالك قراءة {CaseDisplay.Days(days)}", Evidence: [request.TraceSha256], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = request.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status), closedAt = c.ClosedAt, publishedToOwner = published });
    }
}
