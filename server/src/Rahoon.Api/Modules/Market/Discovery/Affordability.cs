using System.Globalization;

namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>One payment the buyer would face, in the order they come: now, then the schedule.</summary>
/// <param name="When">now | schedule | extra | financing</param>
public sealed record PaymentItem(string Key, string Label, decimal? Amount, string When, string? Note);

public static class AffordabilityOutcome
{
    public const string Fits = "fits";
    public const string DoesNotFit = "does_not_fit";
    public const string Incomplete = "incomplete";
}

/// <summary>
/// The answer to «can I afford this one?» for a declared capacity. <see cref="Outcome"/> is fits / does_not_fit / incomplete;
/// it is never a financing approval. <see cref="Fits"/> and <see cref="Comparable"/> are kept for the Phase 1 card.
/// </summary>
public sealed record AffordabilityResult(
    string Outcome, string Headline, List<string> Reasons, List<string> Limits, List<string> Unknowns, List<string> Caveats,
    List<PaymentItem> NextPayments, decimal? CashLeftAfterNow, decimal? AnnualCommitment, decimal? ComfortAnnual)
{
    public bool Fits => Outcome == AffordabilityOutcome.Fits;
    public bool Comparable => Outcome != AffordabilityOutcome.Incomplete;
}

/// <summary>
/// The only affordability classifier (Phase 2). It reads the typed terms snapshot only, exactly like its SQL twin in
/// <see cref="DiscoveryQuery.Budget"/>; a test runs both over the same rows. Rules, for the parts of the capacity given:
/// cash now ≥ due now; ceiling ≥ total; installment comfort ≥ the monthly equivalent and, with an annual extra payment, a year's
/// installments plus that payment within a year of comfort; the largest extra payment within the cash left after paying now;
/// remaining term within the limit. An unknown figure makes the rule unknown, never a pass: the result is then «incomplete».
/// </summary>
public static class Affordability
{
    private static string Sar(decimal d) => d.ToString("#,0", CultureInfo.InvariantCulture) + " ر.س";

    public static AffordabilityResult Classify(CapacityProfile cap, OpportunityTerms t)
    {
        var reasons = new List<string>();
        var limits = new List<string>();
        var unknowns = new List<string>();
        bool fail = false, unknown = false;

        if (cap.AvailableNow is { } a)
        {
            if (t.DueNow is not { } d) { unknown = true; unknowns.Add("المبلغ المطلوب الآن غير مكتمل بعد، فلا يمكن مقارنته بما لديك."); }
            else if (d <= a) reasons.Add($"المطلوب الآن ({Sar(d)}) ضمن المبلغ المتاح لديك ({Sar(a)}).");
            else { fail = true; limits.Add($"المطلوب الآن ({Sar(d)}) أعلى من المبلغ المتاح لديك ({Sar(a)})."); }

            // The largest extra payment must fit in what is left after paying now (Phase 1 rule, kept).
            if (t.LargestExtraPayment is { } extra && t.DueNow is { } d2 && extra > a - d2 && d2 <= a)
            {
                fail = true;
                limits.Add($"توجد دفعة إضافية ({Sar(extra)}) تتجاوز ما يتبقى لديك بعد الدفع الآن ({Sar(Math.Max(0, a - d2))}).");
            }
        }

        if (cap.MaxTotal is { } max)
        {
            if (t.PurchaseTotal is not { } total) { unknown = true; unknowns.Add("إجمالي الالتزام غير مكتمل للمقارنة بحدك الأقصى."); }
            else if (total <= max) reasons.Add($"إجمالي الالتزام ({Sar(total)}) ضمن حدك الأقصى.");
            else { fail = true; limits.Add($"إجمالي الالتزام ({Sar(total)}) أعلى من حدك الأقصى ({Sar(max)})."); }
        }

        decimal? annual = null, comfortAnnual = null;
        if (cap.ComfortMonthly is { } cm)
        {
            comfortAnnual = cm * 12;
            if (t.FutureBalance == 0) reasons.Add("لا توجد أقساط مستقبلية على هذه الفرصة عند الشراء النقدي.");
            else if (t.InstallmentMonthlyEquivalent is { } im)
            {
                if (im > cm) { fail = true; limits.Add($"مكافئ القسط الشهري ({Sar(im)}) أعلى من الحد المريح لك ({Sar(cm)} شهريًا)."); }
                else if (!t.ScheduleKnown) { unknown = true; unknowns.Add("الدفعة الإضافية معروفة القيمة لكن تكرارها غير مسجل، فلا يمكن تأكيد قدرتك على الأقساط."); }
                else
                {
                    annual = im * 12 + (t.AnnualExtraPayment ?? 0);
                    if (t.AnnualExtraPayment is > 0 && annual > comfortAnnual)
                    {
                        fail = true;
                        limits.Add($"الدفعة السنوية ({Sar(t.AnnualExtraPayment.Value)}) تجعل التزامك في السنة ({Sar(annual.Value)}) أعلى مما يريحك ({Sar(comfortAnnual.Value)})، رغم أن المتوسط الشهري مريح.");
                    }
                    else reasons.Add(t.AnnualExtraPayment is > 0
                        ? $"الأقساط مع الدفعة السنوية ({Sar(annual.Value)} في السنة) ضمن ما يريحك."
                        : "مكافئ القسط الشهري ضمن الحد المريح لك (القسط الفعلي ودوريته في التفاصيل).");
                }
            }
            else { unknown = true; unknowns.Add("القسط أو دوريته غير معروفين بعد، فلا يمكن مقارنتهما بالحد المريح لك."); }
        }

        if (cap.MaxTermMonths is { } term)
        {
            if (t.FutureBalance == 0) { }
            else if (t.RemainingMonths is not { } rm) { unknown = true; unknowns.Add("المدة المتبقية للأقساط غير معروفة."); }
            else if (rm <= term) reasons.Add($"المدة المتبقية ({rm} شهرًا) ضمن ما حددته.");
            else { fail = true; limits.Add($"المدة المتبقية ({rm} شهرًا) أطول مما حددته ({term} شهرًا)."); }
        }

        var outcome = fail ? AffordabilityOutcome.DoesNotFit : unknown ? AffordabilityOutcome.Incomplete : AffordabilityOutcome.Fits;
        var headline = outcome switch
        {
            AffordabilityOutcome.Fits => cap.Any ? "تناسب الأرقام التي حددتها" : "لم تحدد قدرتك بعد",
            AffordabilityOutcome.DoesNotFit => "لا تناسب كل ما حددته",
            _ => "المقارنة غير مكتملة: بعض الأرقام غير معروفة",
        };
        decimal? left = cap.AvailableNow is { } av && t.DueNow is { } dn ? av - dn : null;
        return new AffordabilityResult(outcome, headline, reasons, limits, unknowns, Caveats(t), NextPayments(t), left, annual, comfortAnnual);
    }

    /// <summary>What a buyer should know whatever their capacity (never part of the outcome).</summary>
    public static List<string> Caveats(OpportunityTerms t)
    {
        var c = new List<string>();
        if (t.Quality == "complete_estimate") c.Add("بعض الأرقام تقديرية أو مصرح بها ولم يتحقق الفريق منها كلها بعد.");
        else if (t.Quality is "incomplete" or null && !t.Complete) c.Add("تقدير غير مكتمل: بعض الأرقام لم تُعرف بعد.");
        if (t.Track is "financier" or "mixed")
            c.Add("القسط الحالي لصاحب العقار لا ينتقل إليك: يُسدَّد تمويله عند الإتمام، وأي تمويل لك منفصل وبشروط جهتك.");
        if (t.NeedsNewFinancing) c.Add("إن احتجت تمويلًا جديدًا فهو يخضع لموافقة جهة تمويلك؛ المطلوب الآن المعروض للشراء النقدي.");
        if (t.FutureBalance != 0 && t.Installment is not null && t.ExtraPaymentRecurrence is null && t.LargestExtraPayment is not null)
            c.Add("تكرار الدفعة الإضافية غير مسجل.");
        return c;
    }

    /// <summary>The next material payments, from what the model records. There is no next-installment date in the model.</summary>
    public static List<PaymentItem> NextPayments(OpportunityTerms t)
    {
        var items = new List<PaymentItem> { new("due_now", "المطلوب الآن عند الإتمام", t.DueNow, "now", t.DueNow is null ? "غير مكتمل بعد" : null) };
        if (t.Installment is { } inst)
        {
            var freq = FieldCatalog.Label(FieldCatalog.Frequencies, t.InstallmentFrequency);
            var note = $"{freq}{(t.RemainingMonths is { } rm ? $" · لمدة نحو {rm} شهرًا" : "")} · موعد القسط القادم غير مسجل لدينا، يحدده جدول المطور";
            items.Add(new PaymentItem("installment", "القسط للمطور", inst, "schedule", note));
        }
        else if (t.FutureBalance is > 0) items.Add(new PaymentItem("installment", "القسط للمطور", null, "schedule", "غير معروف بعد"));
        if (t.LargestExtraPayment is { } extra)
        {
            var rec = t.ExtraPaymentRecurrence switch { "annual" => "سنويًا", "once" => "مرة واحدة", _ => "تكرارها غير مسجل" };
            items.Add(new PaymentItem("extra_payment", "دفعة إضافية", extra, "extra",
                $"{rec}{(t.NextExtraPaymentDate is { Length: > 0 } date ? $" · القادمة {date}" : " · موعدها غير مسجل")}"));
        }
        if (t.Track is "financier" or "mixed" && t.NeedsNewFinancing)
            items.Add(new PaymentItem("buyer_financing", "قسط تمويلك إن موّلت الشراء", null, "financing", "تحدده جهة تمويلك وموافقتها، وليس قسط صاحب العقار"));
        return items;
    }
}
