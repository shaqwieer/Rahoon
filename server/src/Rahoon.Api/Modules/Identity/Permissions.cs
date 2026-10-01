namespace Rahoon.Api.Modules.Identity;

/// <summary>
/// How far a granted permission reaches. <see cref="Assigned"/>: only work assigned to the member (or linked to work
/// assigned to them); <see cref="All"/>: all of the Rahoon team's work. Order matters: All is wider than Assigned.
/// </summary>
public enum GrantScope { Assigned = 0, All = 1 }

public sealed record PermissionArea(string Key, string NameAr, string NameEn);

/// <param name="Scopable">The grant carries a resource scope (assigned / all). Non-scopable grants are always "all".</param>
/// <param name="ReservedFor">Phase that will implement the actions; reserved keys are listed but cannot be granted yet.</param>
public sealed record PermissionDef(string Key, string Area, string NameAr, string NameEn, bool Scopable = false, string? ReservedFor = null)
{
    public bool Reserved => ReservedFor is not null;
}

/// <summary>
/// Permission catalog of the Rahoon team (operator organization). Authorization is always evaluated on the server from
/// the caller's membership; owners and buyers are individuals and hold no permissions. The default role matrix is
/// <see cref="SystemRoles"/> (documented in docs/rahoon/roadmap/phase-1.5-admin-access.md).
/// </summary>
public static class P
{
    public const string DashboardView = "dashboard.view";

    public const string TeamRead = "team.read";
    /// <summary>Invite members, change their roles, suspend, reactivate, remove and reassign their work.</summary>
    public const string TeamManage = "team.manage";
    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";

    /// <summary>Read sale/buyer requests, opportunities and interests (lists, details, counts).</summary>
    public const string MarketView = "market.view";
    /// <summary>Permitted case edits: correct a field with a reason, internal notes.</summary>
    public const string MarketEdit = "market.edit";
    /// <summary>Review: start review, request information (completion), external approvals log, buyer capacity review.</summary>
    public const string MarketReview = "market.review";
    /// <summary>Verify a figure against its source; record a buyer's financing approval.</summary>
    public const string MarketVerify = "market.verify";
    /// <summary>Approve or reject a sale or buyer request.</summary>
    public const string MarketDecide = "market.decide";
    /// <summary>Assign work to another member of the team.</summary>
    public const string MarketAssign = "market.assign";
    public const string MarketPrepare = "market.prepare";
    public const string MarketPublish = "market.publish";
    /// <summary>Follow interests, contact customers (reveal their mobile), handle contact messages.</summary>
    public const string MarketFollow = "market.follow";

    /// <summary>Open/download private documents (identity, contracts, statements).</summary>
    public const string DocumentsRead = "documents.read";
    /// <summary>Accept or reject documents and listing photos.</summary>
    public const string DocumentsReview = "documents.review";

    public const string DirectoryRead = "directory.read";
    /// <summary>Manage the Saudi organization directory (developers, banks, finance companies).</summary>
    public const string DirectoryManage = "directory.manage";
    public const string DirectoryImport = "directory.import";

    public const string AuditRead = "audit.read";
    public const string SettingsManage = "settings.manage";
    public const string ReportsExport = "reports.export";

    public const string OffersManage = "offers.manage";
    public const string ReservationsManage = "reservations.manage";
    public const string ClosingManage = "closing.manage";
    public const string PaymentEvidence = "payments.evidence";

    public static readonly IReadOnlyList<PermissionArea> Areas =
    [
        new("dashboard", "لوحة التشغيل", "Dashboard"),
        new("team", "الفريق", "Team"),
        new("roles", "الأدوار والصلاحيات", "Roles and permissions"),
        new("requests", "طلبات البيع والشراء", "Sale and buyer requests"),
        new("opportunities", "الفرص", "Opportunities"),
        new("followup", "المتابعة والتواصل", "Follow-up and contact"),
        new("documents", "المستندات", "Documents"),
        new("directory", "دليل الجهات", "Organization directory"),
        new("audit", "السجل", "Audit"),
        new("settings", "الإعدادات", "Settings"),
        new("reports", "التقارير", "Reports"),
        new("transactions", "العروض والإتمام", "Offers and completion"),
    ];

    public static readonly IReadOnlyList<PermissionDef> Catalog =
    [
        new(DashboardView, "dashboard", "عرض لوحة التشغيل والملخص", "View the operations dashboard"),

        new(TeamRead, "team", "عرض أعضاء الفريق وصلاحياتهم", "View team members and their access"),
        new(TeamManage, "team", "دعوة الأعضاء وتعديل أدوارهم وإيقافهم وإعادة إسناد أعمالهم", "Invite members, change roles, suspend, reassign work"),
        new(RolesRead, "roles", "عرض الأدوار ومصفوفة الصلاحيات", "View roles and the permission matrix"),
        new(RolesManage, "roles", "إنشاء الأدوار المخصصة وتعديلها وأرشفتها", "Create, edit and archive custom roles"),

        new(MarketView, "requests", "عرض طلبات البيع والشراء والفرص والاهتمامات", "View sale/buyer requests, opportunities and interests", Scopable: true),
        new(MarketEdit, "requests", "تصحيح بيانات الطلب بسبب وكتابة الملاحظات الداخلية", "Correct request data with a reason, internal notes", Scopable: true),
        new(MarketReview, "requests", "مراجعة الطلبات وطلب الاستكمال وتسجيل موافقات الجهات", "Review requests, request information, log external approvals", Scopable: true),
        new(MarketVerify, "requests", "التحقق من الأرقام من مصدرها وتسجيل موافقة تمويل المشتري", "Verify figures from their source, record buyer financing approval", Scopable: true),
        new(MarketDecide, "requests", "قبول الطلب أو رفضه", "Approve or reject a request", Scopable: true),
        new(MarketAssign, "requests", "إسناد العمل إلى عضو آخر في الفريق", "Assign work to another team member"),

        new(MarketPrepare, "opportunities", "تجهيز الفرصة وإرسالها للمالك", "Prepare an opportunity and send it to the owner", Scopable: true),
        new(MarketPublish, "opportunities", "نشر الفرصة وإيقافها وسحبها", "Publish, pause or withdraw an opportunity", Scopable: true),

        new(MarketFollow, "followup", "متابعة الاهتمامات والتواصل مع العملاء ورسائل التواصل", "Follow interests, contact customers, contact messages", Scopable: true),

        new(DocumentsRead, "documents", "فتح المستندات الخاصة وتنزيلها", "Open and download private documents", Scopable: true),
        new(DocumentsReview, "documents", "قبول المستندات والصور أو رفضها", "Accept or reject documents and photos", Scopable: true),

        new(DirectoryRead, "directory", "عرض دليل الجهات", "View the organization directory"),
        new(DirectoryManage, "directory", "إدارة دليل الجهات", "Manage the organization directory"),
        new(DirectoryImport, "directory", "استيراد الدليل من المصادر الرسمية من الواجهة", "Import the directory from official sources in the console", ReservedFor: "4"),

        new(AuditRead, "audit", "عرض سجل الإدارة والدخول", "Read the administration and sign-in log"),
        new(SettingsManage, "settings", "إدارة إعدادات المنصة والسياسات", "Manage platform settings and policies", ReservedFor: "4"),
        new(ReportsExport, "reports", "التقارير والتصدير", "Reports and exports", ReservedFor: "4"),

        new(OffersManage, "transactions", "إدارة العروض والتفاوض", "Manage offers and negotiation", Scopable: true, ReservedFor: "3A"),
        new(ReservationsManage, "transactions", "إدارة الحجز المبدئي", "Manage provisional reservations", Scopable: true, ReservedFor: "3A"),
        new(ClosingManage, "transactions", "إدارة ملفات الإتمام والنقل", "Manage closing and transfer cases", Scopable: true, ReservedFor: "3B"),
        new(PaymentEvidence, "transactions", "تسجيل إثباتات الدفع والرسوم", "Record payment evidence and fees", Scopable: true, ReservedFor: "3B"),
    ];

    public static readonly IReadOnlySet<string> AllKeys = Catalog.Select(c => c.Key).ToHashSet();
    /// <summary>Keys that can be granted today (not reserved for a later phase).</summary>
    public static readonly IReadOnlySet<string> GrantableKeys = Catalog.Where(c => !c.Reserved).Select(c => c.Key).ToHashSet();

    public static PermissionDef? Find(string key) => Catalog.FirstOrDefault(c => c.Key == key);
    public static bool IsScopable(string key) => Find(key)?.Scopable == true;
}
