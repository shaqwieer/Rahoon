namespace Rahoon.Api.Modules.Identity;

public enum OrganizationKind { Lender, ServiceProvider, JudicialAgent, Platform }

public sealed record RoleTemplate(string Key, string NameAr, string NameEn, OrganizationKind Kind, IReadOnlyList<string> Permissions);

/// <summary>
/// Role templates copied into each organization at onboarding. Institutions may
/// adjust their copies (A02) subject to platform minima (PA07), but server-side
/// separation-of-duties guards apply regardless of role configuration.
/// </summary>
public static class SystemRoles
{
    public const string OrgAdmin = "org_admin";
    public const string CaseManager = "case_manager";
    public const string CaseOfficer = "case_officer";
    public const string CreditAnalyst = "credit_analyst";
    public const string Approver = "approver";
    public const string SeniorApprover = "senior_approver";
    public const string RiskCommittee = "risk_committee";
    public const string Legal = "legal";
    public const string Finance = "finance";
    public const string Compliance = "compliance";
    public const string Auditor = "auditor";

    public const string ProviderAdmin = "provider_admin";
    public const string ProviderAgent = "provider_agent";
    public const string JudicialAgent = "judicial_agent";

    public const string PlatformOps = "platform_ops";
    public const string PlatformSupport = "platform_support";
    public const string PlatformCompliance = "platform_compliance";
    public const string PlatformAuditor = "platform_auditor";

    private static readonly string[] TeamBase =
    [
        P.PortfolioView, P.CaseView, P.DocumentRequest, P.DocumentUpload, P.CommsSend, P.TaskManage, P.AuditView, P.ComplaintView,
    ];

    public static readonly IReadOnlyList<RoleTemplate> Templates =
    [
        new(OrgAdmin, "مسؤول المنشأة", "Institution admin", OrganizationKind.Lender,
        [
            P.PortfolioView, P.CaseView, P.CaseViewAll, P.CaseCreate, P.CaseImport, P.CaseAssign, P.CaseExport,
            P.OrgSettings, P.UserManage, P.RoleChangeApprove, P.LimitsManage, P.TemplateEdit, P.ReportsView, P.AnalyticsView,
            P.AuditView, P.ComplaintView, P.CaseCancelApprove, P.InvoiceApprove, P.ProviderAssign,
        ]),
        new(CaseManager, "مدير حالات", "Case manager", OrganizationKind.Lender,
        [
            .. TeamBase, P.CaseCreate, P.CaseEdit, P.CaseImport, P.CaseAssign, P.CaseExport, P.CasePause, P.CaseCancel, P.CaseTransition,
            P.PiiReveal, P.DocumentReview, P.DocumentDownload, P.ValuationAssign, P.SolutionPrepare, P.SolutionReview, P.OfferSend,
            P.NegotiationManage, P.AgreementPrepare, P.BreachManage, P.SaleManage, P.ProviderAssign, P.ReportsView,
        ]),
        new(CaseOfficer, "موظف حالة", "Case officer", OrganizationKind.Lender,
        [
            .. TeamBase, P.CaseEdit, P.DocumentReview, P.NegotiationManage,
        ]),
        new(CreditAnalyst, "محلل ائتمان", "Credit analyst", OrganizationKind.Lender,
        [
            .. TeamBase, P.PiiReveal, P.DocumentReview, P.DocumentDownload, P.ValuationAssign, P.ValuationReview, P.AnalysisEdit,
            P.SolutionPrepare, P.CaseTransition, P.AnalyticsView,
        ]),
        new(Approver, "معتمد", "Approver", OrganizationKind.Lender,
        [
            P.PortfolioView, P.CaseView, P.CaseViewAll, P.AuditView, P.PiiReveal, P.SolutionApprove, P.SaleApprove,
            P.ReferralApprove, P.ReconciliationApprove, P.DistributionApprove, P.CaseClose, P.CaseCancelApprove, P.ReportsView,
        ]),
        new(SeniorApprover, "معتمد أول", "Senior approver", OrganizationKind.Lender,
        [
            P.PortfolioView, P.CaseView, P.CaseViewAll, P.AuditView, P.PiiReveal, P.SolutionApprove, P.SaleApprove,
            P.ReferralApprove, P.ReconciliationApprove, P.DistributionApprove, P.CaseClose, P.CaseCancelApprove, P.ReportsView, P.AnalyticsView,
        ]),
        new(RiskCommittee, "لجنة المخاطر", "Risk committee", OrganizationKind.Lender,
        [
            P.PortfolioView, P.CaseView, P.CaseViewAll, P.AuditView, P.SolutionApprove, P.SaleApprove, P.ReportsView, P.AnalyticsView,
        ]),
        new(Legal, "القانونية", "Legal", OrganizationKind.Lender,
        [
            .. TeamBase, P.PiiReveal, P.DocumentReview, P.DocumentDownload, P.AgreementPrepare, P.AgreementActivate,
            P.ReferralInitiate, P.ReferralExternalUpdate, P.CaseTransition, P.ProviderAssign,
        ]),
        new(Finance, "المالية", "Finance", OrganizationKind.Lender,
        [
            .. TeamBase, P.PaymentRecord, P.PaymentMatch, P.BreachManage, P.ReconciliationPrepare, P.CaseClose, P.DistributionApprove,
            P.InvoiceApprove, P.ReportsView,
        ]),
        new(Compliance, "الامتثال", "Compliance", OrganizationKind.Lender,
        [
            P.PortfolioView, P.CaseView, P.CaseViewAll, P.AuditView, P.ComplaintView, P.ComplaintHandle, P.TemplatePublish, P.ReportsView,
        ]),
        new(Auditor, "مدقق", "Auditor", OrganizationKind.Lender,
        [
            P.PortfolioView, P.CaseView, P.CaseViewAll, P.AuditView, P.ComplaintView, P.ReportsView,
        ]),

        new(ProviderAdmin, "مسؤول مقدم الخدمة", "Provider admin", OrganizationKind.ServiceProvider,
            [P.AssignmentWork, P.InvoiceSubmit, P.UserManage, P.OrgSettings]),
        new(ProviderAgent, "مقيّم / وسيط", "Valuer / broker", OrganizationKind.ServiceProvider,
            [P.AssignmentWork]),
        new(JudicialAgent, "وكيل البيع القضائي", "Judicial sale agent", OrganizationKind.JudicialAgent,
            [P.AgentWork]),

        new(PlatformOps, "مسؤول عمليات", "Operations", OrganizationKind.Platform,
            [P.PlatformOps, P.PlatformInstitutions, P.PlatformUsers, P.PlatformDefaults, P.PlatformBilling, P.PlatformIntegrations]),
        new(PlatformSupport, "دعم تقني", "Technical support", OrganizationKind.Platform,
            [P.PlatformOps, P.PlatformTempAccess]),
        new(PlatformCompliance, "الامتثال", "Compliance", OrganizationKind.Platform,
            [P.PlatformOps, P.PlatformComplaints, P.PlatformPrivacy, P.PlatformDefaults, P.PlatformAudit]),
        new(PlatformAuditor, "مدقق", "Auditor", OrganizationKind.Platform,
            [P.PlatformOps, P.PlatformAudit, P.PlatformTempAccessApprove]),
    ];

    public static RoleTemplate? Find(string key) => Templates.FirstOrDefault(t => t.Key == key);
}
