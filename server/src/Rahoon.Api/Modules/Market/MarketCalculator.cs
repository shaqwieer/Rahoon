namespace Rahoon.Api.Modules.Market;

/// <summary>verified (checked by the team against a document) · declared (stated by the owner) · estimated (team estimate).</summary>
public static class FigureStates
{
    public const string Verified = "verified";
    public const string Declared = "declared";
    public const string Estimated = "estimated";
}

/// <summary>The developer obligation (docs/product/product-definition.md §7A). Null = unknown, never 0.</summary>
public sealed record DeveloperTerms
{
    /// <summary>P — the approved amount paid towards the unit price.</summary>
    public decimal? PaidApproved { get; init; }
    /// <summary>D — the remaining balance to the developer.</summary>
    public decimal? RemainingBalance { get; init; }
    /// <summary>none | has | unknown</summary>
    public string ArrearsState { get; init; } = "unknown";
    /// <summary>A — the overdue part due now.</summary>
    public decimal? Arrears { get; init; }
    /// <summary>yes | no | unknown — whether A is already inside D (statement). Ignored unless ArrearsState = has.</summary>
    public string? ArrearsInBalance { get; init; }
    /// <summary>buyer | seller — who carries A at completion.</summary>
    public string ArrearsPayer { get; init; } = "buyer";
    /// <summary>V — the optional reduction the owner accepts on P (0 when none).</summary>
    public decimal Reduction { get; init; }
    public decimal? Installment { get; init; }
    public string? InstallmentFrequency { get; init; }
    public int? RemainingInstallments { get; init; }
    public decimal? ExtraPayment { get; init; }
    /// <summary>once | annual</summary>
    public string? ExtraPaymentRecurrence { get; init; }
    public string? ExtraPaymentDate { get; init; }
}

/// <summary>The bank / financier obligation (§7B). The payoff comes from the financier's letter, never from installments.</summary>
public sealed record FinancierTerms
{
    public decimal? SalePrice { get; init; }
    public decimal? PayoffAmount { get; init; }
    public string? PayoffValidUntil { get; init; }
    /// <summary>none | has | unknown</summary>
    public string ArrearsState { get; init; } = "unknown";
    public decimal? Arrears { get; init; }
    /// <summary>yes | no | unknown — whether the payoff amount already includes the arrears.</summary>
    public string? PayoffIncludesArrears { get; init; }
}

/// <summary>Inputs of one opportunity (or of the public owner calculator). Costs: null = unknown; 0 = confirmed none.</summary>
public sealed record TermsInput
{
    public DeveloperTerms? Developer { get; init; }
    public FinancierTerms? Financier { get; init; }
    public decimal? SellerCosts { get; init; } = 0;
    public decimal? BuyerCostsNow { get; init; } = 0;
    public decimal? BuyerCostsLater { get; init; } = 0;
    /// <summary>The buyer will likely need new financing from their own financing party (shown with its status, never assumed).</summary>
    public bool NeedsNewFinancing { get; init; }
    /// <summary>Per-figure state for display: key → verified | declared | estimated.</summary>
    public Dictionary<string, string> States { get; init; } = new();
    /// <summary>
    /// Optional fees with who carries them (Phase 2 calculators). Absent on every stored terms version, which keeps their results
    /// unchanged; when given, each fee's share is added to the seller's or the buyer's costs (now or later). Example fees typed by
    /// the person are not approved real fees.
    /// </summary>
    public List<FeeItem>? Fees { get; init; }
    /// <summary>The date the figures are as of (provenance), shown with the result.</summary>
    public DateOnly? AsOf { get; init; }
}

/// <param name="Amount">null = unknown (the result becomes incomplete), never 0.</param>
/// <param name="Payer">buyer | seller | split</param>
/// <param name="BuyerSharePercent">With «split»: the buyer's share, 0–100 (the seller carries the rest).</param>
/// <param name="Timing">now | later — when the buyer's share is due (the seller's share is settled at completion).</param>
public sealed record FeeItem(string Label, decimal? Amount, string Payer, decimal? BuyerSharePercent, string Timing);

/// <param name="Payee">seller | developer | financier | other | rahoon</param>
/// <param name="BornBy">buyer | seller</param>
/// <param name="Timing">now | later | info</param>
public sealed record CalcLine(string Key, string Label, decimal? Value, string Payee, string BornBy, string Timing, string? Note = null, string? State = null);

public sealed record CommissionLine(bool PolicyApproved, decimal? Amount, string Text);

public sealed record TermsResult
{
    public required bool Complete { get; init; }
    /// <summary>complete_verified | complete_estimate | incomplete</summary>
    public required string Quality { get; init; }
    public required string QualityText { get; init; }
    public required List<string> Missing { get; init; }
    public required List<CalcLine> Lines { get; init; }
    public required List<string> Notes { get; init; }
    /// <summary>The seller's figures don't cover what is due (negative net): shown as a gap to review, never as 0.</summary>
    public bool Gap { get; init; }
    public decimal? OwnerAmount { get; init; }
    public decimal? SellerNet { get; init; }
    public decimal? DueNow { get; init; }
    public decimal? FutureBalance { get; init; }
    public decimal? BuyerTotal { get; init; }
    public decimal? Installment { get; init; }
    public string? InstallmentFrequency { get; init; }
    public decimal? InstallmentMonthlyEquivalent { get; init; }
    public decimal? LargestExtraPayment { get; init; }
    public int? RemainingMonths { get; init; }
    public bool NeedsNewFinancing { get; init; }
    public required CommissionLine Commission { get; init; }
    /// <summary>How each fee was allocated (only when fees were given).</summary>
    public List<CalcLine>? FeeLines { get; init; }
    /// <summary>What the result assumes: figure states, the as-of date, example fees (only from Phase 2 calculators).</summary>
    public List<string>? Assumptions { get; init; }
}

/// <summary>Commission policy (Market:Commission). Not approved → never computed, never shown as 0 (§8).</summary>
public sealed record CommissionPolicy
{
    public bool Approved { get; init; }
    /// <summary>Fraction, e.g. 0.02 for 2%. Only used when approved.</summary>
    public decimal? Rate { get; init; }
    /// <summary>purchase_total | due_now</summary>
    public string Basis { get; init; } = "purchase_total";
    public string Payer { get; init; } = "buyer";
    public string Timing { get; init; } = "at_completion";
    public decimal? VatRate { get; init; }

    public static CommissionPolicy From(IConfiguration config) => config.GetSection("Market:Commission").Get<CommissionPolicy>() ?? new CommissionPolicy();
}

/// <summary>Buyer capacity (§5). Declared figures only; never a financing decision.</summary>
public sealed record CapacityInput(decimal? AvailableNow, decimal? InstallmentComfort, string? InstallmentFrequency, decimal? MaxPrice);

public sealed record FitResult(bool Fits, bool Comparable, List<string> Reasons, List<string> Limits);

/// <summary>
/// The single calculation engine for every screen (owner calculator, team preparation, opportunity page, search snapshot).
/// Money in decimal, rounded to halalas. Unknown inputs make the result «تقدير غير مكتمل» with the missing list; a value is
/// never treated as 0 to make a result appear. These are scenario formulas, not a promise that every deal splits this way.
/// </summary>
public static class MarketCalculator
{
    public static decimal? MonthlyEquivalent(decimal? amount, string? frequency) => amount is not { } a ? null : frequency switch
    {
        "monthly" => R(a),
        "quarterly" => R(a / 3m),
        "semiannual" => R(a / 6m),
        "annual" => R(a / 12m),
        _ => null,
    };

    public static int? MonthsPerPeriod(string? frequency) => frequency switch
    {
        "monthly" => 1, "quarterly" => 3, "semiannual" => 6, "annual" => 12, _ => null,
    };

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    private static decimal? R(decimal? v) => v is { } x ? R(x) : null;

    public static TermsResult Compute(TermsInput input, CommissionPolicy commission)
    {
        var missing = new List<string>();
        var notes = new List<string>();
        var lines = new List<CalcLine>();
        string? St(string key) => input.States.TryGetValue(key, out var s) ? s : null;
        void Need(bool ok, string label) { if (!ok && !missing.Contains(label)) missing.Add(label); }

        var dev = input.Developer;
        var fin = input.Financier;
        if (dev is null && fin is null) Need(false, "جهة الالتزام");

        // ── Fees (optional): each fee's share goes to the seller's costs or the buyer's costs now/later ──
        List<CalcLine>? feeLines = null;
        if (input.Fees is { Count: > 0 } fees)
        {
            feeLines = [];
            decimal? sellerAdd = 0, buyerNowAdd = 0, buyerLaterAdd = 0;
            foreach (var (fee, i) in fees.Select((f, i) => (f, i)))
            {
                var buyerShare = fee.Payer switch { "buyer" => 1m, "seller" => 0m, _ => Math.Clamp(fee.BuyerSharePercent ?? 50m, 0m, 100m) / 100m };
                var later = fee.Timing == "later";
                if (fee.Amount is not { } amount)
                {
                    Need(false, $"قيمة الرسم: {fee.Label}");
                    if (buyerShare < 1) sellerAdd = null;
                    if (buyerShare > 0) { if (later) buyerLaterAdd = null; else buyerNowAdd = null; }
                    feeLines.Add(new CalcLine($"fee:{i}", fee.Label, null, "other", fee.Payer, later ? "later" : "now", "القيمة غير معروفة"));
                    continue;
                }
                var b = R(amount * buyerShare);
                var sPart = R(amount - b);
                if (sPart > 0) sellerAdd += sPart;
                if (b > 0) { if (later) buyerLaterAdd += b; else buyerNowAdd += b; }
                var who = fee.Payer switch { "buyer" => "يتحمله المشتري", "seller" => "يتحمله صاحب العقار", _ => $"مقسوم: المشتري {b.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture)} وصاحب العقار {sPart.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture)}" };
                feeLines.Add(new CalcLine($"fee:{i}", fee.Label, R(amount), "other", fee.Payer, later ? "later" : "now", who + (later ? " لاحقًا" : " عند الإتمام")));
            }
            input = input with
            {
                SellerCosts = input.SellerCosts is { } sc0 && sellerAdd is { } sa ? sc0 + sa : null,
                BuyerCostsNow = input.BuyerCostsNow is { } bn0 && buyerNowAdd is { } bna ? bn0 + bna : null,
                BuyerCostsLater = input.BuyerCostsLater is { } bl0 && buyerLaterAdd is { } bla ? bl0 + bla : null,
            };
        }

        // ── Developer part ──
        decimal? devArrears = null, devTotalBalance = null;
        var devArrearsBuyer = dev?.ArrearsPayer != "seller";
        if (dev is not null)
        {
            Need(dev.PaidApproved is not null, "المدفوع المعتمد من ثمن الوحدة");
            Need(dev.RemainingBalance is not null, "الرصيد المتبقي للمطور");
            switch (dev.ArrearsState)
            {
                case "none": devArrears = 0; break;
                case "has":
                    Need(dev.Arrears is not null, "إجمالي المتأخرات لدى المطور");
                    devArrears = dev.Arrears;
                    Need(dev.ArrearsInBalance is "yes" or "no", "هل المتأخرات ضمن رصيد المطور");
                    break;
                default: Need(false, "وجود متأخرات لدى المطور"); break;
            }
            // A inside D (statement says so) → D already holds it; A outside D → the developer is owed D + A. Never counted twice.
            if (dev.RemainingBalance is { } d && devArrears is { } a)
                devTotalBalance = dev.ArrearsState == "has" && dev.ArrearsInBalance == "no" ? d + a : d;
            if (dev.Reduction < 0) Need(false, "تخفيض صحيح");
            if (dev.PaidApproved is { } p && dev.Reduction > p) notes.Add("التخفيض أكبر من المدفوع المعتمد؛ يحتاج مراجعة.");
        }

        // ── Financier part ──
        decimal? dueToFinancier = null;
        if (fin is not null)
        {
            Need(fin.SalePrice is not null, "سعر البيع المقترح");
            Need(fin.PayoffAmount is not null, "مبلغ السداد المطلوب من الجهة (من خطابها)");
            decimal? finArrearsExtra = null;
            switch (fin.ArrearsState)
            {
                case "none": finArrearsExtra = 0; break;
                case "has":
                    Need(fin.PayoffIncludesArrears is "yes" or "no", "هل يشمل مبلغ السداد المتأخرات");
                    if (fin.PayoffIncludesArrears == "yes") finArrearsExtra = 0;
                    else if (fin.PayoffIncludesArrears == "no") { Need(fin.Arrears is not null, "إجمالي المتأخرات لدى الجهة"); finArrearsExtra = fin.Arrears; }
                    break;
                default: Need(false, "وجود متأخرات لدى الجهة"); break;
            }
            if (fin.PayoffAmount is { } po && finArrearsExtra is { } fx) dueToFinancier = po + fx;
            if (fin.PayoffValidUntil is { Length: > 0 } until) notes.Add($"مبلغ السداد بحسب خطاب الجهة وصالح حتى {until}.");
            notes.Add("لا نفترض أن المشتري سيكمل قسط التمويل الحالي، ولا نحسب مبلغ السداد من الأقساط المتبقية.");
        }

        Need(input.SellerCosts is not null, "تكاليف صاحب العقار");
        Need(input.BuyerCostsNow is not null, "تكاليف المشتري المستحقة الآن");
        Need(input.BuyerCostsLater is not null, "تكاليف المشتري اللاحقة");

        // ── Owner amount (before the owner's own costs) ──
        decimal? ownerAmount = null;
        if (fin is not null)
        {
            if (fin.SalePrice is { } sp && dueToFinancier is { } df)
            {
                ownerAmount = sp - df;
                if (dev is not null && !devArrearsBuyer && devArrears is { } da) ownerAmount -= da;
            }
        }
        else if (dev?.PaidApproved is { } paid) ownerAmount = paid - dev.Reduction;

        decimal? sellerNet = null;
        if (ownerAmount is { } oa && input.SellerCosts is { } sc)
        {
            sellerNet = oa - sc;
            // Developer-only, seller carries A: it comes out of the owner's amount.
            if (fin is null && dev is not null && !devArrearsBuyer) sellerNet = devArrears is { } da ? sellerNet - da : null;
        }

        // ── Buyer ──
        decimal? dueNow = null;
        if (ownerAmount is { } owner && input.BuyerCostsNow is { } bcn)
        {
            dueNow = owner + bcn;
            if (fin is not null) dueNow = dueToFinancier is { } df2 ? dueNow + df2 : null;
            if (dev is not null)
            {
                if (devArrearsBuyer) dueNow = devArrears is { } da ? dueNow + da : null;
                else if (fin is not null) dueNow = devArrears is { } da2 ? dueNow + da2 : null; // mixed: A was taken from the owner amount, still paid now
            }
        }

        decimal? future = dev is null ? 0 : devTotalBalance is { } tb && devArrears is { } a2 ? tb - a2 : null;
        decimal? buyerTotal = dueNow is { } n && future is { } f && input.BuyerCostsLater is { } bcl ? n + f + bcl : null;

        var gap = sellerNet is < 0 || ownerAmount is < 0;
        if (gap) notes.Add("توجد فجوة: ما يجب سداده للجهات والتكاليف أكبر من المبلغ المقترح. الأرقام تحتاج مراجعة الفريق.");

        // ── Lines ──
        lines.Add(new CalcLine("owner_amount", fin is null ? "المبلغ المقترح لصاحب العقار" : "ما يتبقى لصاحب العقار قبل تكاليفه", R(ownerAmount), "seller", "buyer", "now",
            fin is null ? "المدفوع المعتمد ناقص التخفيض الاختياري." : "سعر البيع ناقص ما يُسدد للجهة عند الإتمام.", St(fin is null ? "paid_approved" : "sale_price")));
        if (dev is not null && devArrears is > 0)
            lines.Add(new CalcLine("developer_arrears", "متأخرات تُسدد للمطور عند الإتمام", R(devArrears), "developer", devArrearsBuyer ? "buyer" : "seller", "now",
                devArrearsBuyer ? "يتحملها المشتري الآن، وهي جزء من رصيد المطور فلا تُحسب مرتين." : "تُخصم من مستحقات صاحب العقار.", St("arrears")));
        if (fin is not null)
            lines.Add(new CalcLine("financier_payoff", "يُسدد لجهة التمويل عند الإتمام", R(dueToFinancier), "financier", "buyer", "now",
                "بحسب خطاب الجهة وما يشمله.", St("payoff_amount")));
        lines.Add(new CalcLine("seller_costs", "تكاليف يتحملها صاحب العقار", R(input.SellerCosts), "other", "seller", "now", null, St("seller_costs")));
        lines.Add(new CalcLine("seller_net", "صافي صاحب العقار المبدئي", R(sellerNet), "seller", "seller", "info", gap ? "فجوة تحتاج مراجعة." : null));
        lines.Add(new CalcLine("buyer_costs_now", "تكاليف المشتري المستحقة الآن", R(input.BuyerCostsNow), "other", "buyer", "now", null, St("buyer_costs_now")));
        lines.Add(new CalcLine("due_now", "المطلوب من المشتري الآن", R(dueNow), "", "buyer", "now",
            fin is not null && input.NeedsNewFinancing ? "للشراء النقدي. عند تمويل جديد يعتمد المبلغ الآن على شروط جهة تمويلك وموافقتها." : null));
        if (dev is not null)
        {
            lines.Add(new CalcLine("future_balance", "الرصيد المستقبلي للمطور", R(future), "developer", "buyer", "later", "يُدفع وفق جدول المطور بعد النقل.", St("remaining_balance")));
            if (dev.Installment is { } inst)
                lines.Add(new CalcLine("installment", "القسط", R(inst), "developer", "buyer", "later",
                    $"{FieldCatalog.Label(FieldCatalog.Frequencies, dev.InstallmentFrequency)}{(dev.RemainingInstallments is { } ri ? $" · {ri} قسطًا متبقيًا" : "")}", St("installment_amount")));
            if (dev.ExtraPayment is { } extra)
                lines.Add(new CalcLine("extra_payment", "دفعة إضافية", R(extra), "developer", "buyer", "later",
                    $"{(dev.ExtraPaymentRecurrence == "annual" ? "سنويًا" : "مرة واحدة")}{(dev.ExtraPaymentDate is { Length: > 0 } ed ? $" · القادمة {ed}" : "")} — ضمن الرصيد وليست جزءًا من القسط.", St("extra_payment_amount")));
        }
        lines.Add(new CalcLine("buyer_costs_later", "تكاليف المشتري اللاحقة", R(input.BuyerCostsLater), "other", "buyer", "later"));
        lines.Add(new CalcLine("buyer_total", "إجمالي التزام المشتري", R(buyerTotal), "", "buyer", "info", "المطلوب الآن + الرصيد المستقبلي + التكاليف اللاحقة."));

        // ── Commission (policy) ──
        CommissionLine commissionLine;
        if (!commission.Approved || commission.Rate is not { } rate)
            commissionLine = new CommissionLine(false, null, "عمولة رهون تُحدد وفق السياسة والعقد المعتمدين، ولم تُعتمد بعد. لا تشملها الإجماليات أعلاه.");
        else
        {
            var basis = commission.Basis == "due_now" ? dueNow : buyerTotal;
            decimal? amount = basis is { } b ? R(b * rate * (1 + (commission.VatRate ?? 0))) : null;
            commissionLine = new CommissionLine(true, amount,
                $"عمولة رهون {rate * 100:0.##}% من {(commission.Basis == "due_now" ? "المطلوب الآن" : "إجمالي الالتزام")}{(commission.VatRate is > 0 ? " شاملة ضريبة القيمة المضافة" : "")}، يتحملها {(commission.Payer == "buyer" ? "المشتري" : "صاحب العقار")} عند الإتمام.");
        }

        var complete = missing.Count == 0;
        var allVerified = complete && input.States.Count > 0 && input.States.Values.All(s => s == FigureStates.Verified);
        var quality = !complete ? "incomplete" : allVerified ? "complete_verified" : "complete_estimate";
        var qualityText = quality switch
        {
            "incomplete" => "تقدير غير مكتمل — ينقصه: " + string.Join("، ", missing) + ".",
            "complete_verified" => "أرقام راجعها فريق رهون من مستنداتها.",
            _ => "تقدير مبدئي يحتاج مراجعة؛ لم يتحقق الفريق من كل الأرقام من مستنداتها بعد.",
        };

        var monthly = dev is null ? null : MonthlyEquivalent(dev.Installment, dev.InstallmentFrequency);
        int? remainingMonths = dev?.RemainingInstallments is { } cnt && MonthsPerPeriod(dev.InstallmentFrequency) is { } per ? cnt * per : null;
        return new TermsResult
        {
            Complete = complete, Quality = quality, QualityText = qualityText, Missing = missing, Lines = lines, Notes = notes, Gap = gap,
            OwnerAmount = R(ownerAmount), SellerNet = R(sellerNet), DueNow = R(dueNow), FutureBalance = R(future), BuyerTotal = R(buyerTotal),
            Installment = dev?.Installment, InstallmentFrequency = dev?.InstallmentFrequency, InstallmentMonthlyEquivalent = monthly,
            LargestExtraPayment = dev?.ExtraPayment, RemainingMonths = remainingMonths,
            NeedsNewFinancing = input.NeedsNewFinancing, Commission = commissionLine,
            FeeLines = feeLines, Assumptions = input.Fees is null && input.AsOf is null ? null : Assumptions(input),
        };
    }

    private static readonly Dictionary<string, string> StateWords = new()
    {
        [FigureStates.Verified] = "تحقق منه الفريق من مستند", [FigureStates.Declared] = "مصرح به", [FigureStates.Estimated] = "تقدير",
    };

    private static readonly Dictionary<string, string> FigureWords = new()
    {
        ["paid_approved"] = "المدفوع المعتمد", ["remaining_balance"] = "الرصيد المتبقي", ["arrears"] = "المتأخرات", ["installment_amount"] = "القسط",
        ["sale_price"] = "سعر البيع", ["payoff_amount"] = "مبلغ السداد", ["seller_costs"] = "تكاليف صاحب العقار", ["buyer_costs_now"] = "تكاليف المشتري الآن",
    };

    private static List<string> Assumptions(TermsInput input)
    {
        var a = new List<string>();
        if (input.AsOf is { } d) a.Add($"الأرقام كما في {d:yyyy-MM-dd}.");
        foreach (var (k, v) in input.States.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            a.Add($"{FigureWords.GetValueOrDefault(k, k)}: {StateWords.GetValueOrDefault(v, v)}.");
        if (input.States.Count == 0) a.Add("كل الأرقام كما أدخلتها، ولم يتحقق منها فريق رهون.");
        if (input.Fees is { Count: > 0 }) a.Add("الرسوم أمثلة أدخلتها أنت لتجربة توزيعها، وليست رسومًا معتمدة.");
        return a;
    }

    /// <summary>
    /// Fits an opportunity snapshot against a buyer's declared capacity. Unknown figures never match (not treated as 0). A low
    /// monthly average does not make a fit when a large extra payment exceeds what remains after the amount due now.
    /// </summary>
    public static FitResult Fit(CapacityInput cap, decimal? dueNow, decimal? purchaseTotal, decimal? installmentMonthly, decimal? largestExtra, bool needsNewFinancing)
    {
        var reasons = new List<string>();
        var limits = new List<string>();
        if (dueNow is null)
            return new FitResult(false, false, reasons, ["المبلغ المطلوب الآن غير مكتمل بعد، فلا يمكن مقارنته بقدرتك."]);
        var fits = true;
        if (cap.AvailableNow is { } avail)
        {
            if (dueNow <= avail) reasons.Add("المبلغ المطلوب الآن ضمن المبلغ المتاح لديك.");
            else { fits = false; limits.Add("المبلغ المطلوب الآن أعلى من المبلغ المتاح لديك."); }
            if (largestExtra is { } extra && extra > Math.Max(0, avail - dueNow.Value))
            {
                fits = false;
                limits.Add("توجد دفعة إضافية كبيرة قد تتجاوز ما يتبقى لديك بعد الدفع الآن؛ راجعها قبل الاهتمام.");
            }
        }
        var comfortMonthly = MonthlyEquivalent(cap.InstallmentComfort, cap.InstallmentFrequency ?? "monthly");
        if (installmentMonthly is { } im && im > 0)
        {
            if (comfortMonthly is { } cm)
            {
                if (im <= cm) reasons.Add("مكافئ القسط الشهري ضمن الحد المريح لك (القسط الفعلي ودوريته في التفاصيل).");
                else { fits = false; limits.Add("القسط أعلى من الحد المريح لك."); }
            }
        }
        if (cap.MaxPrice is { } max)
        {
            if (purchaseTotal is null) limits.Add("إجمالي الالتزام غير مكتمل للمقارنة بحدك الأقصى.");
            else if (purchaseTotal <= max) reasons.Add("إجمالي الالتزام ضمن الحد الأقصى الذي حددته.");
            else { fits = false; limits.Add("إجمالي الالتزام أعلى من الحد الأقصى الذي حددته."); }
        }
        if (needsNewFinancing) limits.Add("قد تحتاج الفرصة تمويلًا جديدًا يخضع لموافقة جهة التمويل.");
        return new FitResult(fits, true, reasons, limits);
    }
}
