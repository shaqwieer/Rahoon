using Rahoon.Api.Modules.Market;

namespace Rahoon.Api.Tests;

/// <summary>Acceptance #2: the fields follow the property type and the obligation party, on the server (pure unit tests).</summary>
public sealed class FieldCatalogTests
{
    private static Dictionary<string, string?> D(params (string K, string? V)[] kv) => kv.ToDictionary(x => x.K, x => x.V);

    [Fact]
    public void Land_drops_rooms_bathrooms_and_finishing_instead_of_requiring_them()
    {
        var applied = FieldCatalog.Apply("property", D(("area", "625"), ("bedrooms", "4"), ("bathrooms", "3"), ("finishing", "full"), ("land_use", "residential")), "land", null);
        Assert.Empty(applied.Errors);
        Assert.Equal("625", applied.Answers["area"]);
        Assert.False(applied.Answers.ContainsKey("bedrooms"));
        Assert.False(applied.Answers.ContainsKey("bathrooms"));
        Assert.False(applied.Answers.ContainsKey("finishing"));
        Assert.DoesNotContain(FieldCatalog.PropertyFields("land", applied.Answers), f => f.Key is "bedrooms" or "bathrooms" or "readiness");
    }

    [Fact]
    public void Developer_obligation_has_no_bank_fields_and_bank_has_no_developer_fields()
    {
        var dev = FieldCatalog.ObligationFields("developer", new Dictionary<string, string>()).Select(f => f.Key).ToList();
        Assert.DoesNotContain("payoff_amount", dev);
        Assert.DoesNotContain("current_installment", dev);
        Assert.Contains("paid_approved", dev);

        var fin = FieldCatalog.ObligationFields("financier", new Dictionary<string, string>()).Select(f => f.Key).ToList();
        Assert.DoesNotContain("paid_approved", fin);
        Assert.DoesNotContain("remaining_balance", fin);
        Assert.Contains("payoff_amount", fin);

        // A bank field sent on a developer obligation is dropped, not validated.
        var applied = FieldCatalog.Apply("obligation", D(("paid_approved", "300000"), ("payoff_amount", "abc")), null, "developer");
        Assert.Empty(applied.Errors);
        Assert.False(applied.Answers.ContainsKey("payoff_amount"));
    }

    [Fact]
    public void Switching_a_branch_off_drops_its_dependent_answers()
    {
        var withArrears = FieldCatalog.Apply("obligation", D(("arrears_state", "has"), ("arrears_amount", "20000")), null, "developer");
        Assert.Equal("20000", withArrears.Answers["arrears_amount"]);
        var noArrears = FieldCatalog.Apply("obligation", D(("arrears_state", "none"), ("arrears_amount", "20000")), null, "developer");
        Assert.False(noArrears.Answers.ContainsKey("arrears_amount"));
    }

    [Fact]
    public void No_arrears_and_unknown_are_different_answers()
    {
        var none = FieldCatalog.Apply("obligation", D(("arrears_state", "none")), null, "financier");
        var unknown = FieldCatalog.Apply("obligation", D(("arrears_state", "unknown")), null, "financier");
        Assert.Equal("none", none.Answers["arrears_state"]);
        Assert.Equal("unknown", unknown.Answers["arrears_state"]);
        Assert.True(FieldCatalog.IsKnown(none.Answers, "arrears_state"));
        Assert.False(FieldCatalog.IsKnown(unknown.Answers, "arrears_state"));
    }

    [Fact]
    public void Unknown_is_allowed_only_where_the_catalog_says_so_and_is_never_a_number()
    {
        var ok = FieldCatalog.Apply("obligation", D(("payoff_amount", FieldCatalog.Unknown)), null, "financier");
        Assert.Equal(FieldCatalog.Unknown, ok.Answers["payoff_amount"]);
        Assert.Null(FieldCatalog.Num(ok.Answers, "payoff_amount"));

        var bad = FieldCatalog.Apply("property", D(("bedrooms", FieldCatalog.Unknown)), "apartment", null);
        Assert.True(bad.Errors.ContainsKey("bedrooms"));
    }

    [Fact]
    public void Arabic_and_latin_digits_are_read_the_same_way()
    {
        var a = FieldCatalog.Apply("obligation", D(("paid_approved", "٣٠٠٬٠٠٠")), null, "developer");
        var b = FieldCatalog.Apply("obligation", D(("paid_approved", "300,000")), null, "developer");
        Assert.Equal("300000", a.Answers["paid_approved"]);
        Assert.Equal(a.Answers["paid_approved"], b.Answers["paid_approved"]);
    }

    [Fact]
    public void Negative_amounts_and_bad_dates_are_refused()
    {
        var applied = FieldCatalog.Apply("obligation", D(("paid_approved", "-5"), ("contract_date", "2026-13-40")), null, "developer");
        Assert.True(applied.Errors.ContainsKey("paid_approved"));
        Assert.True(applied.Errors.ContainsKey("contract_date"));
    }

    [Fact]
    public void Developer_paid_amount_cannot_exceed_the_contract_price_but_bank_installments_are_not_checked_that_way()
    {
        var dev = FieldCatalog.CrossCheck("developer", new Dictionary<string, string> { ["paid_approved"] = "1200000", ["original_price"] = "1000000" });
        Assert.True(dev.ContainsKey("paid_approved"));
        var fin = FieldCatalog.CrossCheck("financier", new Dictionary<string, string> { ["paid_approved"] = "1200000", ["original_price"] = "1000000" });
        Assert.Empty(fin);
    }

    [Fact]
    public void Feature_options_of_another_property_type_are_dropped()
    {
        var applied = FieldCatalog.Apply("property", D(("features", "pool,corner,maid_room")), "apartment", null);
        Assert.Equal("maid_room", applied.Answers["features"]);
    }
}
