using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Tests;

/// <summary>Pure B7 rules: S05 password policy, S02/A01 e-mail domains, A05 template variables and tone check.</summary>
public sealed class B7UnitTests
{
    [Fact]
    public void Password_policy_matches_the_s05_requirements()
    {
        var email = "f.alotaibi@alufuq.example";
        Assert.True(PasswordPolicy.IsValid("Kestrel-Harbor-7741!", email, "فهد العتيبي"));
        var shortOne = PasswordPolicy.Evaluate("Ab1!", email, "فهد العتيبي");
        Assert.False(shortOne.Single(r => r.Key == "length").Met);
        Assert.False(PasswordPolicy.Evaluate("password1234", email, null).Single(r => r.Key == "not_breached").Met);
        Assert.False(PasswordPolicy.Evaluate("my-alotaibi-2026!", email, null).Single(r => r.Key == "no_personal").Met);
        Assert.False(PasswordPolicy.Evaluate("العتيبي-كلمة-سر-1", email, "فهد العتيبي").Single(r => r.Key == "no_personal").Met);
    }

    [Theory]
    [InlineData("someone@gmail.com", true)]
    [InlineData("SOMEONE@Hotmail.com", true)]
    [InlineData("l.alghamdi@alufuq.example", false)]
    public void Personal_email_domains_are_detected(string email, bool personal) => Assert.Equal(personal, EmailDomains.IsPersonal(email));

    [Fact]
    public void Template_variables_must_be_declared_and_english_uses_aliases()
    {
        string[] declared = ["{القسط}", "{المدة}", "{تاريخ_الصلاحية}"];
        Assert.Empty(TemplateRules.UnknownVariables(declared, "قسط {القسط} لمدة {المدة} حتى {تاريخ_الصلاحية}", "Installment {installment} for {term}", null));
        var errors = TemplateRules.UnknownVariables(declared, "رقم هويتك {رقم_الهوية}", "Your {national_id}", "{الرصيد}");
        Assert.Equal(["bodyAr", "bodySms", "bodyEn"], errors.Keys.ToArray());
    }

    [Fact]
    public void Tone_check_blocks_threats_and_flags_missing_deadline_and_length()
    {
        var good = TemplateRules.Tone("أرسلنا لك عرضاً جديداً. يمكنك مراجعته والرد حتى {تاريخ_الصلاحية}، وإن كان لديك سؤال فاكتب لنا.", "We sent you a new offer.", null, null);
        Assert.All(good, c => Assert.True(c.Ok, c.Label));

        var threat = TemplateRules.Tone("يجب عليك السداد فوراً وإلا سنضطر لاتخاذ إجراءات قانونية.", null, null, null);
        Assert.False(threat.Single(c => c.Key == "no_threat").Ok);
        Assert.Equal("block", threat.Single(c => c.Key == "no_threat").Level);
        Assert.False(threat.Single(c => c.Key == "deadline_and_help").Ok);
        Assert.False(threat.Single(c => c.Key == "english_updated").Ok);

        var longText = string.Join(" ", Enumerable.Repeat("كلمة", 61)) + " حتى {المهلة} اكتب لنا";
        Assert.False(TemplateRules.Tone(longText, "x", null, null).Single(c => c.Key == "under_60_words").Ok);
        // Arabic changed but English left identical to the published version → not updated.
        Assert.False(TemplateRules.Tone("نص جديد حتى {المهلة} اكتب لنا", "Old english", "نص قديم", "Old english").Single(c => c.Key == "english_updated").Ok);
    }
}
