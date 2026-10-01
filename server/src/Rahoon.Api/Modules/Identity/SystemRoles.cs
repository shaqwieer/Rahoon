namespace Rahoon.Api.Modules.Identity;

/// <summary>Operator = «فريق رهون», the only organization kind: the Rahoon team that reviews and publishes.</summary>
public enum OrganizationKind { Operator }

public sealed record TemplateGrant(string Key, GrantScope Scope);

public sealed record RoleTemplate(string Key, string NameAr, string NameEn, string DescriptionAr, IReadOnlyList<TemplateGrant> Grants)
{
    public IEnumerable<string> Permissions => Grants.Select(g => g.Key);
}

/// <summary>
/// Protected system roles of the Rahoon team (the default role-permission matrix). Their grants are fixed here and
/// re-applied by <see cref="SystemRoleSync"/> on every migration; a different mix is a custom role. Separation of
/// duties is enforced on the server regardless of the UI.
/// </summary>
public static class SystemRoles
{
    public const string PlatformOwner = "platform_owner";
    public const string OperationsManager = "operations_manager";
    public const string CaseManager = "case_manager";
    public const string DocumentReviewer = "document_reviewer";
    public const string Publisher = "publisher";
    public const string FinanceOfficer = "finance_officer";
    public const string SupportAgent = "support_agent";
    public const string Auditor = "auditor";

    /// <summary>Role keys before Phase 1.5, renamed in place so existing memberships keep their meaning.</summary>
    public static readonly IReadOnlyDictionary<string, string> Renamed = new Dictionary<string, string>
    {
        ["team_lead"] = PlatformOwner,
        ["team_coordinator"] = CaseManager,
        ["team_verifier"] = Publisher,
    };

    private static TemplateGrant A(string key) => new(key, GrantScope.All);
    private static TemplateGrant S(string key) => new(key, GrantScope.Assigned);

    public static readonly IReadOnlyList<RoleTemplate> Templates =
    [
        new(PlatformOwner, "مالك المنصة", "Platform owner",
            "الفريق والأدوار والإشراف الكامل على التشغيل. لا يمكن إيقاف آخر مالك نشط.",
            P.Catalog.Where(p => !p.Reserved).Select(p => A(p.Key)).ToList()),
        new(OperationsManager, "مدير العمليات", "Operations manager",
            "كل قوائم العمل والإسناد وقرارات المراجعة. لا يدير الفريق ولا الأدوار ولا ينشر إلا بمنح صريح.",
            [A(P.DashboardView), A(P.TeamRead), A(P.RolesRead), A(P.MarketView), A(P.MarketEdit), A(P.MarketReview), A(P.MarketVerify),
             A(P.MarketDecide), A(P.MarketAssign), A(P.MarketPrepare), A(P.MarketFollow), A(P.DocumentsRead), A(P.DocumentsReview),
             A(P.DirectoryRead), A(P.AuditRead)]),
        new(CaseManager, "مسؤول ملفات", "Case manager",
            "الطلبات المسندة إليه ومتابعتها وتعديلاتها المسموحة. لا ينشر ولا يتحقق من الأرقام المالية.",
            [A(P.DashboardView), S(P.MarketView), S(P.MarketEdit), S(P.MarketReview), S(P.MarketDecide), S(P.MarketPrepare), S(P.MarketFollow),
             S(P.DocumentsRead), S(P.DocumentsReview), A(P.DirectoryRead)]),
        new(DocumentReviewer, "مراجِع مستندات", "Document reviewer",
            "مراجعة مستندات الملفات المسندة إليه والتحقق من الأرقام. لا يقبل الطلب ولا ينشر.",
            [A(P.DashboardView), S(P.MarketView), S(P.MarketReview), S(P.MarketVerify), S(P.DocumentsRead), S(P.DocumentsReview)]),
        new(Publisher, "مسؤول النشر", "Publisher",
            "جاهزية النشر ونشر الفرص وإيقافها. لا يقبل عرضًا نيابة عن المالك ولا يفتح المستندات الخاصة.",
            [A(P.DashboardView), A(P.MarketView), A(P.MarketPublish), A(P.DirectoryRead)]),
        new(FinanceOfficer, "مسؤول مالي", "Finance officer",
            "الأرقام المالية للملفات المسندة إليه، ولاحقًا إثباتات الدفع والرسوم. لا يدير الأدوار ولا يكمل النقل.",
            [A(P.DashboardView), S(P.MarketView), S(P.MarketVerify), S(P.DocumentsRead)]),
        new(SupportAgent, "دعم العملاء", "Support agent",
            "رسائل التواصل وحالة الطلبات المسندة إليه. لا يفتح مستندات الهوية والتمويل.",
            [A(P.DashboardView), S(P.MarketView), A(P.MarketFollow)]),
        new(Auditor, "مدقق", "Auditor",
            "قراءة السجل والملخصات والأعمال. للقراءة فقط، ولا يفتح محتوى المستندات الخاصة.",
            [A(P.DashboardView), A(P.MarketView), A(P.TeamRead), A(P.RolesRead), A(P.AuditRead), A(P.DirectoryRead)]),
    ];

    public static RoleTemplate? Find(string key) => Templates.FirstOrDefault(t => t.Key == key);
    public static bool IsSystem(string key) => Find(key) is not null;
}
