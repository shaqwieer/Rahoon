namespace Rahoon.Api.Modules.Identity;

public sealed record PermissionDef(string Key, string NameAr, string NameEn);

/// <summary>
/// Permission catalog of the Rahoon team (operator organization). Authorization is always evaluated on the server
/// from the caller's membership; owners and buyers are individuals and hold no permissions.
/// </summary>
public static class P
{
    public const string MarketView = "market.view";
    public const string MarketAssign = "market.assign";
    public const string MarketReview = "market.review";
    public const string MarketPrepare = "market.prepare";
    public const string MarketPublish = "market.publish";
    public const string MarketFollow = "market.follow";
    /// <summary>Manage the Saudi organization directory (developers, banks, finance companies).</summary>
    public const string DirectoryManage = "directory.manage";

    public static readonly IReadOnlyList<PermissionDef> Catalog =
    [
        new(MarketView, "عرض طلبات البيع والشراء والفرص", "View sale/buyer requests and opportunities"),
        new(MarketAssign, "إسناد المسؤول", "Assign the owner of a file"),
        new(MarketReview, "مراجعة الطلبات والمستندات والأرقام", "Review requests, documents and figures"),
        new(MarketPrepare, "تجهيز الفرصة وإرسالها للمالك", "Prepare an opportunity and send it to the owner"),
        new(MarketPublish, "نشر الفرصة وإيقافها", "Publish or pause an opportunity"),
        new(MarketFollow, "متابعة الاهتمامات والرسائل", "Follow interests and messages"),
        new(DirectoryManage, "إدارة دليل الجهات", "Manage the organization directory"),
    ];

    public static readonly IReadOnlySet<string> AllKeys = Catalog.Select(c => c.Key).ToHashSet();
}
