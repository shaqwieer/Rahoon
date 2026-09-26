namespace Rahoon.Api.Modules.Identity;

public enum Sensitivity { Normal, Medium, High }
public enum PermissionScope { Institution, Case, Platform }

public sealed record PermissionDef(
    string Key, string NameAr, string NameEn, string Group, Sensitivity Sensitivity,
    PermissionScope Scope, string? ConditionAr = null);

/// <summary>
/// Permission catalog (PA05). Keys shown in the design are used verbatim
/// (case.create, solution.approve, pii.reveal, payment.record, referral.initiate,
/// platform.temp_access, template.publish); the rest complete the 62-key catalog.
/// Authorization is always evaluated on the server from the caller's membership.
/// </summary>
public static class P
{
    // Portfolio & cases
    public const string PortfolioView = "portfolio.view";
    public const string CaseView = "case.view";
    public const string CaseViewAll = "case.view_all";
    public const string CaseCreate = "case.create";
    public const string CaseEdit = "case.edit";
    public const string CaseImport = "case.import";
    public const string CaseAssign = "case.assign";
    public const string CaseExport = "case.export";
    public const string CasePause = "case.pause";
    public const string CaseCancel = "case.cancel";
    public const string CaseCancelApprove = "case.cancel_approve";
    public const string CaseTransition = "case.transition";
    public const string PiiReveal = "pii.reveal";

    // Documents
    public const string DocumentRequest = "document.request";
    public const string DocumentUpload = "document.upload";
    public const string DocumentReview = "document.review";
    public const string DocumentDownload = "document.download";

    // Assessment & valuation
    public const string ValuationAssign = "valuation.assign";
    public const string ValuationReview = "valuation.review";
    public const string AnalysisEdit = "analysis.edit";

    // Solutions & approvals
    public const string SolutionPrepare = "solution.prepare";
    public const string SolutionReview = "solution.review";
    public const string SolutionApprove = "solution.approve";
    public const string OfferSend = "offer.send";
    public const string NegotiationManage = "negotiation.manage";
    public const string AgreementPrepare = "agreement.prepare";
    public const string AgreementActivate = "agreement.activate";

    // Payments & finance
    public const string PaymentRecord = "payment.record";
    public const string PaymentMatch = "payment.match";
    public const string BreachManage = "breach.manage";
    public const string ReconciliationPrepare = "reconciliation.prepare";
    public const string ReconciliationApprove = "reconciliation.approve";
    public const string DistributionApprove = "distribution.approve";
    public const string CaseClose = "case.close";

    // Communications & complaints
    public const string CommsSend = "comms.send";
    public const string TaskManage = "task.manage";
    public const string ComplaintView = "complaint.view";
    public const string ComplaintHandle = "complaint.handle";
    public const string AuditView = "audit.view";

    // Paths: voluntary sale & judicial referral
    public const string SaleManage = "sale.manage";
    public const string SaleApprove = "sale.approve";
    public const string ReferralInitiate = "referral.initiate";
    public const string ReferralApprove = "referral.approve";
    public const string ReferralExternalUpdate = "referral.external_update";

    // Providers
    public const string ProviderAssign = "provider.assign";
    public const string AssignmentWork = "assignment.work";
    public const string InvoiceSubmit = "invoice.submit";
    public const string InvoiceApprove = "invoice.approve";

    // Judicial agent
    public const string AgentWork = "agent.work";

    // Institution administration
    public const string OrgSettings = "org.settings";
    public const string UserManage = "user.manage";
    public const string RoleChangeApprove = "role.change_approve";
    public const string LimitsManage = "limits.manage";
    public const string TemplateEdit = "template.edit";
    public const string TemplatePublish = "template.publish";
    public const string ReportsView = "reports.view";
    public const string AnalyticsView = "analytics.view";

    // Platform
    public const string PlatformOps = "platform.ops";
    public const string PlatformInstitutions = "platform.institutions";
    public const string PlatformUsers = "platform.users";
    public const string PlatformTempAccess = "platform.temp_access";
    public const string PlatformTempAccessApprove = "platform.temp_access_approve";
    public const string PlatformDefaults = "platform.defaults";
    public const string PlatformAudit = "platform.audit";
    public const string PlatformPrivacy = "platform.privacy";
    public const string PlatformBilling = "platform.billing";
    public const string PlatformIntegrations = "platform.integrations";
    public const string PlatformComplaints = "platform.complaints";

    // Rahoon team (operator tenant, ADR 0001 §4.2)
    public const string RequestViewAssigned = "request.view_assigned";
    public const string RequestViewAll = "request.view_all";
    public const string RequestAssign = "request.assign";
    public const string RequestReview = "request.review";
    public const string RequestRequestInfo = "request.request_info";
    public const string RequestCoordinate = "request.coordinate";
    public const string RequestMessage = "request.message";
    public const string RequestOfferRecord = "request.offer_record";
    public const string RequestOfferVerify = "request.offer_verify";
    public const string RequestResponseRelay = "request.response_relay";
    public const string RequestClose = "request.close";
    public const string RequestObjectionHandle = "request.objection_handle";

    public static readonly IReadOnlyList<PermissionDef> Catalog =
    [
        new(PortfolioView, "عرض المحفظة", "View portfolio", "الحالات", Sensitivity.Normal, PermissionScope.Institution),
        new(CaseView, "عرض الحالات المسندة", "View assigned cases", "الحالات", Sensitivity.Normal, PermissionScope.Case),
        new(CaseViewAll, "عرض كل حالات المنشأة", "View all institution cases", "الحالات", Sensitivity.Medium, PermissionScope.Institution),
        new(CaseCreate, "إنشاء حالة", "Create case", "الحالات", Sensitivity.Normal, PermissionScope.Institution),
        new(CaseEdit, "تعديل بيانات الحالة", "Edit case data", "الحالات", Sensitivity.Normal, PermissionScope.Case),
        new(CaseImport, "استيراد جماعي", "Bulk import", "الحالات", Sensitivity.Medium, PermissionScope.Institution),
        new(CaseAssign, "إعادة إسناد الحالات", "Reassign cases", "الحالات", Sensitivity.Normal, PermissionScope.Institution),
        new(CaseExport, "تصدير الحالات", "Export cases", "الحالات", Sensitivity.Medium, PermissionScope.Institution, "مخفي افتراضياً · الكشف بسبب"),
        new(CasePause, "إيقاف الحالة مؤقتاً", "Pause case", "الحالات", Sensitivity.Medium, PermissionScope.Case, "سبب إلزامي"),
        new(CaseCancel, "طلب إلغاء الحالة", "Request case cancellation", "الحالات", Sensitivity.High, PermissionScope.Case, "سبب · MFA · اعتماد ثانٍ"),
        new(CaseCancelApprove, "اعتماد إلغاء الحالة", "Approve case cancellation", "الحالات", Sensitivity.High, PermissionScope.Case, "ليس مقدم الطلب · MFA"),
        new(CaseTransition, "نقل الحالة بين المراحل", "Move case between stages", "الحالات", Sensitivity.Medium, PermissionScope.Case, "وفق الحواجز"),
        new(PiiReveal, "كشف بيانات شخصية", "Reveal personal data", "الحالات", Sensitivity.High, PermissionScope.Case, "سبب إلزامي · 60 ثانية · مسجل"),

        new(DocumentRequest, "طلب مستندات", "Request documents", "المستندات", Sensitivity.Normal, PermissionScope.Case),
        new(DocumentUpload, "رفع مستندات", "Upload documents", "المستندات", Sensitivity.Normal, PermissionScope.Case),
        new(DocumentReview, "مراجعة المستندات", "Review documents", "المستندات", Sensitivity.Medium, PermissionScope.Case),
        new(DocumentDownload, "تنزيل المستندات", "Download documents", "المستندات", Sensitivity.Medium, PermissionScope.Case, "بعلامة مائية · مسجل"),

        new(ValuationAssign, "تكليف مقيّم", "Assign valuer", "التقييم", Sensitivity.Normal, PermissionScope.Case),
        new(ValuationReview, "مراجعة تقرير التقييم", "Review valuation report", "التقييم", Sensitivity.Medium, PermissionScope.Case),
        new(AnalysisEdit, "إعداد التحليل", "Prepare analysis", "التقييم", Sensitivity.Normal, PermissionScope.Case),

        new(SolutionPrepare, "إعداد حل", "Prepare solution", "الحلول", Sensitivity.Normal, PermissionScope.Case),
        new(SolutionReview, "مراجعة الحل وإرساله للموافقة", "Review & submit solution", "الحلول", Sensitivity.Medium, PermissionScope.Case, "ليس المُعِدّ"),
        new(SolutionApprove, "اعتماد حل", "Approve solution", "الحلول", Sensitivity.High, PermissionScope.Institution, "ضمن الحد · ليس المُعِدّ · MFA"),
        new(OfferSend, "إرسال العرض للمالك", "Send offer to owner", "الحلول", Sensitivity.Medium, PermissionScope.Case, "بعد الاعتماد فقط"),
        new(NegotiationManage, "إدارة التفاوض", "Manage negotiation", "الحلول", Sensitivity.Normal, PermissionScope.Case),
        new(AgreementPrepare, "إعداد الاتفاق", "Prepare agreement", "الاتفاق", Sensitivity.Medium, PermissionScope.Case),
        new(AgreementActivate, "تفعيل الاتفاق", "Activate agreement", "الاتفاق", Sensitivity.High, PermissionScope.Case, "بعد سجل الموافقة والتوقيع"),

        new(PaymentRecord, "تسجيل دفعة", "Record payment", "المالية", Sensitivity.Medium, PermissionScope.Case, "مدقق مختلف للمطابقة"),
        new(PaymentMatch, "مطابقة دفعة", "Match payment", "المالية", Sensitivity.Medium, PermissionScope.Case, "ليس من سجّلها"),
        new(BreachManage, "معالجة الإخلال", "Handle breach", "المالية", Sensitivity.Medium, PermissionScope.Case, "لا إحالة تلقائية"),
        new(ReconciliationPrepare, "إعداد التسوية المالية", "Prepare reconciliation", "المالية", Sensitivity.Medium, PermissionScope.Case),
        new(ReconciliationApprove, "اعتماد التسوية المالية", "Approve reconciliation", "المالية", Sensitivity.High, PermissionScope.Case, "ليس المُعِدّ · فرق صفري أو مفسَّر"),
        new(DistributionApprove, "اعتماد التوزيعات", "Approve distributions", "المالية", Sensitivity.High, PermissionScope.Case, "المالية + معتمد"),
        new(CaseClose, "إغلاق الحالة", "Close case", "المالية", Sensitivity.High, PermissionScope.Case, "موافقتان دائماً"),

        new(CommsSend, "مراسلة المالك", "Message owner", "التواصل", Sensitivity.Normal, PermissionScope.Case, "بقوالب معتمدة"),
        new(TaskManage, "إدارة المهام", "Manage tasks", "التواصل", Sensitivity.Normal, PermissionScope.Case),
        new(ComplaintView, "عرض الشكاوى", "View complaints", "التواصل", Sensitivity.Medium, PermissionScope.Institution),
        new(ComplaintHandle, "معالجة الشكاوى", "Handle complaints", "التواصل", Sensitivity.High, PermissionScope.Institution, "مراجع مستقل عن فريق الحالة"),
        new(AuditView, "عرض سجل التدقيق", "View audit log", "التواصل", Sensitivity.Medium, PermissionScope.Case),

        new(SaleManage, "إدارة البيع الطوعي", "Manage voluntary sale", "المسارات", Sensitivity.Medium, PermissionScope.Case, "بموافقة المالك"),
        new(SaleApprove, "اعتماد البيع", "Approve sale", "المسارات", Sensitivity.High, PermissionScope.Case, "ليس المُعِدّ · MFA"),
        new(ReferralInitiate, "بدء الإحالة", "Initiate referral", "المسارات", Sensitivity.High, PermissionScope.Case, "القانونية فقط · موافقتان"),
        new(ReferralApprove, "اعتماد الإحالة", "Approve referral", "المسارات", Sensitivity.High, PermissionScope.Case, "ليس المُعِدّ · MFA"),
        new(ReferralExternalUpdate, "تحديث المرجع الخارجي", "Update external reference", "المسارات", Sensitivity.Medium, PermissionScope.Case, "يُحفظ حرفياً مع المصدر"),

        new(ProviderAssign, "تكليف مقدم خدمة", "Assign provider", "مقدمو الخدمة", Sensitivity.Normal, PermissionScope.Case),
        new(AssignmentWork, "العمل على التكليف", "Work on assignment", "مقدمو الخدمة", Sensitivity.Normal, PermissionScope.Case, "مدة التكليف + 7 أيام قراءة"),
        new(InvoiceSubmit, "إصدار فاتورة", "Submit invoice", "مقدمو الخدمة", Sensitivity.Normal, PermissionScope.Institution),
        new(InvoiceApprove, "اعتماد فواتير المقدمين", "Approve provider invoices", "مقدمو الخدمة", Sensitivity.Medium, PermissionScope.Institution),
        new(AgentWork, "العمل على الإحالة المكلفة", "Work on assigned referral", "الوكيل", Sensitivity.Medium, PermissionScope.Case, "بعد اعتماد القناة"),

        new(OrgSettings, "إعدادات المنشأة", "Institution settings", "الإدارة", Sensitivity.Medium, PermissionScope.Institution),
        new(UserManage, "إدارة المستخدمين", "Manage users", "الإدارة", Sensitivity.High, PermissionScope.Institution, "تغيير الأدوار بموافقة ثانية"),
        new(RoleChangeApprove, "اعتماد تغيير الأدوار", "Approve role changes", "الإدارة", Sensitivity.High, PermissionScope.Institution, "ليس مقدم الطلب"),
        new(LimitsManage, "حدود الموافقة", "Approval limits", "الإدارة", Sensitivity.High, PermissionScope.Institution, "موافقة المسؤول الثاني"),
        new(TemplateEdit, "تحرير القوالب", "Edit templates", "الإدارة", Sensitivity.Normal, PermissionScope.Institution),
        new(TemplatePublish, "نشر قالب", "Publish template", "الإدارة", Sensitivity.Medium, PermissionScope.Institution, "اعتماد الامتثال"),
        new(ReportsView, "التقارير التشغيلية", "Operational reports", "الإدارة", Sensitivity.Normal, PermissionScope.Institution),
        new(AnalyticsView, "التحليلات ودعم القرار", "Analytics & decision support", "الإدارة", Sensitivity.Normal, PermissionScope.Institution),

        new(RequestViewAssigned, "عرض الطلبات المسندة", "View assigned requests", "فريق رهون", Sensitivity.Medium, PermissionScope.Case),
        new(RequestViewAll, "عرض كل الطلبات", "View all requests", "فريق رهون", Sensitivity.High, PermissionScope.Institution, "قائد الفريق"),
        new(RequestAssign, "إسناد الطلبات", "Assign requests", "فريق رهون", Sensitivity.Medium, PermissionScope.Institution),
        new(RequestReview, "دراسة الطلب", "Review request", "فريق رهون", Sensitivity.Medium, PermissionScope.Case),
        new(RequestRequestInfo, "طلب استكمال من العميل", "Request information", "فريق رهون", Sensitivity.Normal, PermissionScope.Case),
        new(RequestCoordinate, "سجل التنسيق مع الجهة", "Coordination log", "فريق رهون", Sensitivity.Medium, PermissionScope.Case, "بموافقة العميل الموثقة"),
        new(RequestMessage, "مراسلة العميل", "Message the individual", "فريق رهون", Sensitivity.Normal, PermissionScope.Case),
        new(RequestOfferRecord, "تسجيل عرض الجهة", "Record lender offer", "فريق رهون", Sensitivity.High, PermissionScope.Case, "مع خطاب الجهة"),
        new(RequestOfferVerify, "التحقق من العرض ونشره", "Verify and publish offer", "فريق رهون", Sensitivity.High, PermissionScope.Case, "ليس المسجِّل · MFA"),
        new(RequestResponseRelay, "نقل رد العميل للجهة", "Relay response", "فريق رهون", Sensitivity.Medium, PermissionScope.Case),
        new(RequestClose, "إغلاق الطلب", "Close request", "فريق رهون", Sensitivity.Medium, PermissionScope.Case),
        new(RequestObjectionHandle, "معالجة الاعتراضات", "Handle objections", "فريق رهون", Sensitivity.Medium, PermissionScope.Case),

        new(PlatformOps, "لوحة تشغيل المنصة", "Platform operations", "المنصة", Sensitivity.Medium, PermissionScope.Platform),
        new(PlatformInstitutions, "المنشآت والطلبات", "Institutions & applications", "المنصة", Sensitivity.High, PermissionScope.Platform),
        new(PlatformUsers, "مستخدمو المنصة", "Platform users", "المنصة", Sensitivity.High, PermissionScope.Platform),
        new(PlatformTempAccess, "وصول دعم مؤقت", "Temporary support access", "المنصة", Sensitivity.High, PermissionScope.Case, "موافقة المنشأة + المدقق · مدة محددة"),
        new(PlatformTempAccessApprove, "اعتماد الوصول المؤقت (مدقق)", "Approve temporary access (auditor)", "المنصة", Sensitivity.High, PermissionScope.Platform),
        new(PlatformDefaults, "الإعدادات الافتراضية", "Platform defaults", "المنصة", Sensitivity.High, PermissionScope.Platform),
        new(PlatformAudit, "التدقيق العام", "General audit", "المنصة", Sensitivity.High, PermissionScope.Platform),
        new(PlatformPrivacy, "الخصوصية والاحتفاظ", "Privacy & retention", "المنصة", Sensitivity.High, PermissionScope.Platform),
        new(PlatformBilling, "التسعير والفوترة", "Pricing & billing", "المنصة", Sensitivity.Medium, PermissionScope.Platform),
        new(PlatformIntegrations, "التكاملات", "Integrations", "المنصة", Sensitivity.High, PermissionScope.Platform),
        new(PlatformComplaints, "الشكاوى والتصعيد", "Complaints & escalations", "المنصة", Sensitivity.Medium, PermissionScope.Platform),
    ];

    public static readonly IReadOnlySet<string> AllKeys = Catalog.Select(c => c.Key).ToHashSet();
}
