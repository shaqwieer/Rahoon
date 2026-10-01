namespace Rahoon.Api.Modules.Identity;

/// <summary>Operator = «فريق رهون», the only organization kind: the Rahoon team that reviews and publishes.</summary>
public enum OrganizationKind { Operator }

public sealed record RoleTemplate(string Key, string NameAr, string NameEn, IReadOnlyList<string> Permissions);

/// <summary>Role templates of the Rahoon team. Separation of duties is enforced on the server regardless.</summary>
public static class SystemRoles
{
    public const string TeamCoordinator = "team_coordinator";
    public const string TeamVerifier = "team_verifier";
    public const string TeamLead = "team_lead";

    public static readonly IReadOnlyList<RoleTemplate> Templates =
    [
        new(TeamCoordinator, "منسق الطلبات", "Request coordinator", [P.MarketView, P.MarketReview, P.MarketPrepare, P.MarketFollow]),
        new(TeamVerifier, "مراجِع النشر", "Publication reviewer", [P.MarketView, P.MarketReview, P.MarketPublish]),
        new(TeamLead, "قائد الفريق", "Team lead",
            [P.MarketView, P.MarketAssign, P.MarketReview, P.MarketPrepare, P.MarketPublish, P.MarketFollow, P.DirectoryManage]),
    ];

    public static RoleTemplate? Find(string key) => Templates.FirstOrDefault(t => t.Key == key);
}
