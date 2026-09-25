/** L11/L12 DTOs (mirrors server/src/Rahoon.Api/Modules/Assessment/{ValuationEndpoints,AnalysisEndpoints}.cs). */

export interface ValuationReportDto {
  id: string;
  versionNo: number;
  title: string;
  valuer: string;
  marketValue: number;
  range: { low: number; high: number; label: string } | null;
  methodology: string | null;
  comparablesCount: number | null;
  inspectionDate: string | null;
  reportDate: string;
  validUntil: string;
  validityDays: number;
  daysLeft: number;
  validityLabel: string;
  validityTone: "ok" | "warn" | "err";
  status: "Accepted" | "UnderReview" | "Returned" | "Superseded";
  statusLabel: string;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewNote: string | null;
  documentVersionId: string | null;
  assignment: string | null;
  canReview: boolean;
}

export interface TimelineItem {
  type: string;
  icon: string;
  tone: "ink" | "info" | "ok" | "warn" | "err" | "muted";
  title: string;
  meta: string;
  at: string;
  derived: boolean;
}

export interface ValuationData {
  current: ValuationReportDto | null;
  underReview: ValuationReportDto[];
  history: ValuationReportDto[];
  ltv: { value: number | null; label: string; caption: string; outstanding: number | null };
  revaluation: {
    canRequest: boolean;
    eligible: boolean;
    eligibleFrom: string | null;
    windowDays: number;
    requiresReason: boolean;
    pending: boolean;
    disabledReason: string | null;
  };
  assignment: {
    reference: string;
    provider: string | null;
    title: string;
    status: string;
    createdAt: string;
    dueOn: string;
    deliveredAt: string | null;
    providerAccess: { expiresAt: string | null; expired: boolean; label: string };
    timeline: TimelineItem[];
  } | null;
}

export interface AnalysisIndicator {
  icon: string | null;
  text: string;
}

export interface AnalysisOption {
  kind: string | null;
  title: string;
  feasible: boolean | null;
  note: string | null;
}

export interface AnalysisData {
  exists: boolean;
  /** xmin row version — send back on PUT (409 `concurrency` when stale). */
  version: number | null;
  completed: boolean;
  preparedBy: string | null;
  affordability: {
    netMonthlyIncome: number | null;
    incomeSource: { documentVersionId: string; documentId: string; label: string; verified: boolean; verifiedOn: string | null } | null;
    otherObligations: number;
    currentInstallment: number | null;
    currentDsr: number | null;
    proposed: { version: number; installment: number; dsr: number | null; status: string } | null;
    dsrLimit: number;
    limitIsAssumption: boolean;
    dsrIncludingObligations: { current: number | null; proposed: number | null };
    withinLimit: boolean | null;
    rows: Array<{ k: string; v: string; strong: boolean }>;
    formula: string;
  };
  circumstanceIndicators: AnalysisIndicator[];
  indicatorsCaption: string;
  options: AnalysisOption[];
  optionsCaption: string;
  missingForCompletion: string[];
  canEdit: boolean;
}

/** Document types the API accepts as a verified income source. */
export const INCOME_DOCUMENT_TYPES = ["salary_statement", "bank_statement", "salary_certificate"];
