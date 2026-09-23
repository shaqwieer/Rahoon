using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Assessment;

public sealed record IndicatorInput(string? Icon, string? Text);
public sealed record OptionInput(string? Kind, string? Title, bool? Feasible, string? Note);
public sealed record SaveAnalysisRequest(uint? Version, decimal? NetMonthlyIncome, Guid? IncomeDocumentVersionId, decimal? OtherObligations,
    List<IndicatorInput>? CircumstanceIndicators, List<OptionInput>? Options, bool? Completed);

/// <summary>
/// L12 affordability analysis. Income only from a verified document version; DSR computed on the server as
/// installment ÷ verified net monthly income (same definition as <see cref="SolutionCalculator"/> and the
/// 46.5% design canon); other obligations are reported beside it. Optimistic concurrency via the row version.
/// Indicators and options are stored as JSON strings in the existing text[] columns (plain legacy strings still read).
/// </summary>
public static class AnalysisEndpoints
{
    private static readonly string[] IncomeDocumentTypes = ["salary_statement", "bank_statement", "salary_certificate"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequirePermission(P.CaseView);
        g.MapGet("/analysis", Get);
        g.MapPut("/analysis", Save).RequirePermission(P.AnalysisEdit);
    }

    public sealed record Indicator(string? Icon, string Text);
    public sealed record OptionNote(string? Kind, string Title, bool? Feasible, string? Note);

    public static Indicator ParseIndicator(string raw)
    {
        if (raw.StartsWith('{'))
            try { if (JsonSerializer.Deserialize<Indicator>(raw, Json) is { Text.Length: > 0 } i) return i; } catch (JsonException) { }
        return new Indicator(null, raw);
    }

    public static OptionNote ParseOption(string raw)
    {
        if (raw.StartsWith('{'))
            try { if (JsonSerializer.Deserialize<OptionNote>(raw, Json) is { Title.Length: > 0 } o) return o; } catch (JsonException) { }
        return new OptionNote(null, raw, null, null);
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static async Task<IResult> Get(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc)
    {
        var c = await access.GetAsync(reference, track: false);
        return Results.Ok(await PayloadAsync(db, rc, c));
    }

    private static async Task<object> PayloadAsync(RahoonDbContext db, RequestContext rc, Case c)
    {
        var a = await db.Analyses.AsNoTracking().FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var f = await db.FinancingContracts.AsNoTracking().FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var solution = await db.Solutions.AsNoTracking()
            .Where(s => s.CaseId == c.Id && s.Status != SolutionStatus.Rejected && s.Status != SolutionStatus.Superseded && s.Status != SolutionStatus.Returned
                        && s.Status != SolutionStatus.Declined && s.Status != SolutionStatus.Expired)
            .OrderByDescending(s => s.VersionNo).FirstOrDefaultAsync();

        object? incomeSource = null;
        var incomeVerified = false;
        if (a?.IncomeDocumentVersionId is { } vid)
        {
            var v = await db.DocumentVersions.AsNoTracking().Where(x => x.Id == vid && x.CaseId == c.Id)
                .Select(x => new { x.Id, x.DocumentId, x.VersionNo, x.ReviewStatus, x.ReviewedAt }).FirstOrDefaultAsync();
            var docName = v is null ? null : await db.Documents.Where(d => d.Id == v.DocumentId).Select(d => d.Name).FirstOrDefaultAsync();
            incomeVerified = v?.ReviewStatus == ReviewStatus.Verified;
            incomeSource = v is null ? null : new
            {
                documentVersionId = v.Id, documentId = v.DocumentId, label = $"{docName} v{v.VersionNo}", verified = incomeVerified,
                verifiedOn = v.ReviewedAt is { } ra ? DateOnly.FromDateTime(ra.ToOffset(TimeSpan.FromHours(3)).DateTime) : (DateOnly?)null,
            };
        }

        var income = a?.NetMonthlyIncome;
        decimal? Ratio(decimal? x) => x is null || income is not > 0 ? null : Math.Round(x.Value / income.Value, 4);
        var limit = a?.DsrLimit ?? 0.55m;
        var currentInstallment = f?.OriginalInstallment;
        var dsrCurrent = Ratio(currentInstallment);
        var dsrProposed = Ratio(solution?.InstallmentAmount);
        var obligations = a?.OtherObligations ?? 0;
        var preparedBy = a?.PreparedByUserId is { } pid ? await db.Users.Where(u => u.Id == pid).Select(u => u.FullName).FirstOrDefaultAsync() : null;

        var missing = new List<string>();
        if (income is not > 0) missing.Add("صافي الدخل الشهري");
        if (a?.IncomeDocumentVersionId is null || !incomeVerified) missing.Add("مستند دخل متحقق منه");

        static string Pct(decimal? r) => r is null ? "—" : $"{r * 100:0.#}%";
        return new
        {
            exists = a is not null,
            version = a?.Version,
            completed = a?.Completed ?? false,
            preparedBy,
            affordability = new
            {
                netMonthlyIncome = income, incomeSource, otherObligations = obligations,
                currentInstallment, currentDsr = dsrCurrent,
                proposed = solution is null ? null : new { version = solution.VersionNo, installment = solution.InstallmentAmount, dsr = dsrProposed, status = solution.Status.ToString() },
                dsrLimit = limit, limitIsAssumption = true,
                dsrIncludingObligations = new { current = Ratio(currentInstallment + obligations), proposed = solution is null ? null : Ratio(solution.InstallmentAmount + obligations) },
                withinLimit = dsrProposed is null ? (bool?)null : dsrProposed <= limit,
                rows = new object[]
                {
                    new { k = "صافي الدخل الشهري المتحقق", v = income?.ToString("N2") ?? "—", strong = false },
                    new { k = "التزامات أخرى", v = obligations.ToString("N2"), strong = false },
                    new { k = "القسط الحالي / الدخل", v = Pct(dsrCurrent), strong = false },
                    new { k = solution is null ? "القسط المقترح / الدخل" : $"القسط المقترح v{solution.VersionNo} / الدخل", v = Pct(dsrProposed), strong = true },
                    new { k = "حد السياسة (افتراض)", v = Pct(limit), strong = false },
                },
                formula = "نسبة الاستقطاع = القسط الشهري ÷ صافي الدخل الشهري المتحقق (الالتزامات الأخرى تُعرض منفصلة).",
            },
            circumstanceIndicators = (a?.CircumstanceIndicators ?? []).Select(ParseIndicator),
            indicatorsCaption = "مدخلة من المحلل · تُستخدم لاختيار الحل، لا لتقييم الشخص",
            options = (a?.OptionNotes ?? []).Select(ParseOption),
            optionsCaption = "قائمة مرجعية للمحلل؛ القرار بشري ويمر بالموافقات.",
            missingForCompletion = missing,
            canEdit = rc.Has(P.AnalysisEdit) && !CaseStatusInfo.IsTerminal(c.Status),
        };
    }

    private static async Task<IResult> Save(string reference, SaveAnalysisRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        var v = new Validator();
        v.Require(req.NetMonthlyIncome is null or (> 0 and < 100_000_000), "netMonthlyIncome", "صافي الدخل يجب أن يكون أكبر من صفر.");
        v.Require(req.NetMonthlyIncome is null || req.IncomeDocumentVersionId is not null, "incomeDocumentVersionId", "الدخل يُعتمد من مستند متحقق منه فقط؛ اختر إصدار المستند.");
        v.Require(req.OtherObligations is null or (>= 0 and < 100_000_000), "otherObligations", "الالتزامات لا تكون سالبة.");
        v.Require(req.CircumstanceIndicators is null || req.CircumstanceIndicators.Count <= 10, "circumstanceIndicators", "حتى 10 مؤشرات.");
        v.Require(req.CircumstanceIndicators is null || req.CircumstanceIndicators.All(i => i.Text?.Trim().Length is >= 3 and <= 200), "circumstanceIndicators", "نص المؤشر من 3 إلى 200 حرف.");
        v.Require(req.CircumstanceIndicators is null || req.CircumstanceIndicators.All(i => i.Icon is null || i.Icon.Length <= 40), "circumstanceIndicators", "اسم الأيقونة غير صالح.");
        v.Require(req.Options is null || req.Options.Count <= 10, "options", "حتى 10 خيارات.");
        v.Require(req.Options is null || req.Options.All(o => o.Title?.Trim().Length is >= 2 and <= 100 && (o.Note?.Length ?? 0) <= 300 && (o.Kind?.Length ?? 0) <= 40), "options", "عنوان الخيار من 2 إلى 100 حرف والوصف حتى 300.");
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c);
        var a = await db.Analyses.FirstOrDefaultAsync(x => x.CaseId == c.Id);
        if (a is not null)
        {
            if (req.Version is null) Validate.Throw("version", "أرسل رقم إصدار التحليل الذي فتحته.");
            if (req.Version != a.Version) throw new ConflictException("concurrency", "تغيّرت البيانات منذ فتحها من مستخدم آخر. حدّث الصفحة ثم أعد المحاولة.");
            // Also enforce at save time (xmin), so a concurrent writer between read and save still gets 409.
            db.Entry(a).Property(x => x.Version).OriginalValue = req.Version!.Value;
        }
        else
        {
            a = new AffordabilityAnalysis { OrganizationId = c.OrganizationId, CaseId = c.Id, PreparedByUserId = rc.UserId };
            db.Analyses.Add(a);
        }

        var v2 = new Validator();
        if (req.IncomeDocumentVersionId is { } vid)
        {
            var ver = await db.DocumentVersions.AsNoTracking().Where(x => x.Id == vid && x.CaseId == c.Id)
                .Select(x => new { x.ReviewStatus, x.ReviewedAt, x.VersionNo, x.DocumentId }).FirstOrDefaultAsync();
            var doc = ver is null ? null : await db.Documents.AsNoTracking().Where(d => d.Id == ver.DocumentId).Select(d => new { d.Name, d.DocumentTypeKey }).FirstOrDefaultAsync();
            v2.Require(ver is not null && doc is not null && IncomeDocumentTypes.Contains(doc.DocumentTypeKey), "incomeDocumentVersionId", "اختر إصدار كشف راتب أو كشف حساب من مستندات الحالة.");
            v2.Require(ver is null || ver.ReviewStatus == ReviewStatus.Verified, "incomeDocumentVersionId", "إصدار المستند غير متحقق منه؛ الأرقام تُعتمد من مستندات متحقق منها فقط.");
            if (ver is { ReviewStatus: ReviewStatus.Verified } && doc is not null)
            {
                a.IncomeDocumentVersionId = vid;
                a.IncomeSourceLabel = $"{doc.Name} v{ver.VersionNo}";
                a.IncomeVerifiedOn = ver.ReviewedAt is { } ra ? DateOnly.FromDateTime(ra.ToOffset(TimeSpan.FromHours(3)).DateTime) : null;
            }
        }
        v2.ThrowIfInvalid();

        var changed = new List<string>();
        if (req.NetMonthlyIncome is { } inc && inc != a.NetMonthlyIncome) { a.NetMonthlyIncome = Math.Round(inc, 2); changed.Add("صافي الدخل"); }
        if (req.OtherObligations is { } ob && ob != a.OtherObligations) { a.OtherObligations = Math.Round(ob, 2); changed.Add("الالتزامات الأخرى"); }
        if (req.CircumstanceIndicators is not null)
        {
            a.CircumstanceIndicators = req.CircumstanceIndicators.Select(i => Serialize(new Indicator(string.IsNullOrWhiteSpace(i.Icon) ? null : i.Icon.Trim(), i.Text!.Trim()))).ToList();
            changed.Add("مؤشرات الظرف");
        }
        if (req.Options is not null)
        {
            a.OptionNotes = req.Options.Select(o => Serialize(new OptionNote(string.IsNullOrWhiteSpace(o.Kind) ? null : o.Kind.Trim(), o.Title!.Trim(), o.Feasible,
                string.IsNullOrWhiteSpace(o.Note) ? null : o.Note.Trim()))).ToList();
            changed.Add("الخيارات الممكنة");
        }
        if (req.IncomeDocumentVersionId is not null) changed.Add("مصدر الدخل");
        var completing = req.Completed == true && !a.Completed;
        if (req.Completed is { } done)
        {
            if (done && (a.NetMonthlyIncome is not > 0 || a.IncomeDocumentVersionId is null))
                Validate.Throw("completed", "لا يكتمل التحليل دون دخل متحقق من مستند.");
            if (done != a.Completed) changed.Add(done ? "اكتمال التحليل" : "إعادة فتح التحليل");
            a.Completed = done;
        }

        if (changed.Count > 0)
        {
            await audit.RecordAsync(new AuditEntry(completing ? "analysis.completed" : "analysis.updated", completing ? "اكتمال تحليل القدرة على السداد" : "تحديث تحليل القدرة على السداد",
                c.Id, c.Reference, Detail: "الحقول: " + string.Join("، ", changed.Distinct()) + (a.IncomeSourceLabel is null ? "" : $" · مصدر الدخل: {a.IncomeSourceLabel}"),
                OrganizationId: c.OrganizationId));
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Results.Ok(await PayloadAsync(db, rc, c));
    }
}
