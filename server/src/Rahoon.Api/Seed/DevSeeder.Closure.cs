using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Documents;

namespace Rahoon.Api.Seed;

/// <summary>
/// L26 canon for RH-2026-003702 (تركي ب.): settlement reconciliation prepared by ريم الدوسري and submitted for review
/// (two transfers = 455,210.75, difference 0.00; the waiver already reflected in the agreed amount is display-only),
/// closure documents partially ready. Nothing is visible to the owner until closure.
/// </summary>
public sealed partial class DevSeeder
{
    private async Task SeedClosureAsync()
    {
        var c = _cases["RH-2026-003702"];
        var org = c.OrganizationId;

        var r = new Reconciliation
        {
            OrganizationId = org, CaseId = c.Id, Basis = "settlement", ExpectedAmount = 455_210.75m,
            ExpectedSource = "الاتفاق AGR-2026-003702-01 · تسوية نقدية مخفضة", PreparedByUserId = UserId("reem"),
            Status = ReconciliationStatus.Submitted, SubmittedAt = At("2026-09-21T16:30:00"), Note = "المطابقة صفرية الفرق. المستندات مكتملة.",
            CreatedAt = At("2026-09-19T10:00:00"),
        };
        r.Lines.Add(new ReconciliationLine { ReconciliationId = r.Id, Kind = "receipt", Label = "المستلم — تحويل 1", Amount = 300_000.00m, Reference = "TRX-88201744", SourceType = "bank", ValueDate = D("2026-09-10") });
        r.Lines.Add(new ReconciliationLine { ReconciliationId = r.Id, Kind = "receipt", Label = "المستلم — تحويل 2", Amount = 155_210.75m, Reference = "TRX-88355102", SourceType = "bank", ValueDate = D("2026-09-18") });
        r.Lines.Add(new ReconciliationLine
        {
            ReconciliationId = r.Id, Kind = "info", Label = "المبلغ المتنازل عنه (معتمد ومحتسب في المبلغ المتفق عليه)", Amount = 38_204.10m,
            Reference = "قرار 2026-06-11 · نورة الشهري", SourceType = "internal", MatchStatus = "n/a",
        });
        ClosureService.Recompute(r);
        db.Reconciliations.Add(r);

        var clearance = await AddDocumentAsync(c, "final_clearance", "خطاب المخالصة النهائية", DocumentSource.Lender,
            [("clearance-3702.pdf", "reem", "2026-09-21T11:00:00", ReviewStatus.Verified, null, null)], visibleTo: ["case_team", "finance"]);
        var release = await AddDocumentAsync(c, "lien_release_letter", "خطاب فك الرهن", DocumentSource.Legal,
            [("lien-release-3702.pdf", "majed", "2026-09-22T12:00:00", ReviewStatus.Verified, null, null)], visibleTo: ["case_team", "legal"]);
        db.ClosureDocuments.AddRange(
            new ClosureDocument
            {
                OrganizationId = org, CaseId = c.Id, Type = "final_clearance", Title = "خطاب المخالصة النهائية", Status = ClosureDocumentStatus.Ready, PreparedByDept = "المالية",
                DocumentVersionId = clearance.CurrentVersionId, BlocksClosure = true, ShareWithOwner = true, VisibleToOwner = false, PreparedOn = D("2026-09-21"),
            },
            new ClosureDocument
            {
                OrganizationId = org, CaseId = c.Id, Type = "lien_release_letter", Title = "خطاب فك الرهن", Status = ClosureDocumentStatus.Ready, PreparedByDept = "القانونية",
                DocumentVersionId = release.CurrentVersionId, BlocksClosure = true, ShareWithOwner = true, VisibleToOwner = false, PreparedOn = D("2026-09-22"),
            },
            new ClosureDocument
            {
                OrganizationId = org, CaseId = c.Id, Type = "lien_release_submission_proof", Title = "إثبات تقديم طلب فك الرهن للجهة المختصة", Status = ClosureDocumentStatus.Pending,
                PreparedByDept = "القانونية", BlocksClosure = false, ShareWithOwner = false, VisibleToOwner = false,
            },
            new ClosureDocument
            {
                OrganizationId = org, CaseId = c.Id, Type = "owner_final_summary", Title = "ملخص الحالة النهائي للمالك", Status = ClosureDocumentStatus.Pending,
                PreparedByDept = "مولَّد", BlocksClosure = true, ShareWithOwner = true, VisibleToOwner = false,
            });

        Audit(org, c, "reconciliation.created", "بدء التسوية المالية (تسوية نقدية)", At("2026-09-19T10:00:00"), "reem", detail: "المتوقع 455,210.75 · الاتفاق AGR-2026-003702-01");
        Audit(org, c, "closure.document_ready", "رفع خطاب المخالصة النهائية", At("2026-09-21T11:00:00"), "reem");
        Audit(org, c, "reconciliation.submitted", "إرسال التسوية المالية للتدقيق", At("2026-09-21T16:30:00"), "reem",
            reason: r.Note, detail: "المتوقع 455,210.75 · المستلم 455,210.75 · الفرق 0.00");
        Audit(org, c, "closure.document_ready", "رفع خطاب فك الرهن", At("2026-09-22T12:00:00"), "majed");
        await db.SaveChangesAsync();
    }
}
