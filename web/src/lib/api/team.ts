import type { TeamStatusKey } from "@/components/team/copy";

/** Shapes of /api/team (server/src/Rahoon.Api/Modules/Requests/TeamRequestEndpoints.cs). */

export type WaitingKey = "team" | "applicant" | "lender" | "none";
export interface TeamTimer {
  daysInStatus: number;
  daysSinceSubmitted: number | null;
  level: "ok" | "warn" | "alert" | "none";
}

export interface TeamQueue {
  tab: "mine" | "unassigned" | "all" | "closed";
  canViewAll: boolean;
  counts: { mine: number; unassigned: number; all: number };
  items: Array<{
    reference: string;
    applicantName: string;
    institutionName: string;
    status: TeamStatusKey;
    waitingOn: WaitingKey;
    submittedAt: string | null;
    lastUpdate: string;
    assignedTo: string | null;
    timer: TeamTimer;
  }>;
}

export interface TeamMember {
  userId: string;
  name: string;
  role: string | null;
  title: string | null;
}

export interface TeamAction {
  key: string;
  label: string;
  enabled: boolean;
  reasons: string[];
  requiresReason: boolean;
}

export interface TeamDocument {
  id: string;
  kind: string;
  name: string;
  source: "applicant" | "team";
  visibility: "team_only" | "applicant_and_team";
  addedAfterSubmit: boolean;
  versionId: string | null;
  fileName: string | null;
  uploadedAt: string | null;
  uploadedBy: string | null;
  scan: string | null;
  sizeBytes: number | null;
}

export interface CoordinationItem {
  id: string;
  kind: string;
  channel: string;
  occurredAt: string;
  counterpart: string;
  summary: string;
  visibleToApplicant: boolean;
  applicantText: string | null;
  correctsEntryId: string | null;
  recordedBy: string;
  recordedAt: string;
  evidence: Array<{ id: string; name: string; versionId: string | null }>;
}

export interface TeamRequestDetail {
  reference: string;
  status: TeamStatusKey;
  statusLabel: string;
  waitingOn: WaitingKey;
  nextStepText: string | null;
  createdAt: string;
  submittedAt: string | null;
  statusChangedAt: string;
  assigned: { userId: string; name: string | null; at: string | null } | null;
  assignedToMe: boolean;
  applicant: {
    name: string | null;
    idMasked: string;
    idType: string;
    phoneMasked: string;
    identityAssurance: string;
    identityCheck: { at: string; by: string | null; note: string | null } | null;
  };
  institutionName: string;
  institutionListed: boolean;
  fields: {
    contractNumber: string | null;
    monthlyInstallment: number | null;
    arrearsDuration: string | null;
    propertyCity: string | null;
    pathPreference: string | null;
    affordableMonthly: number | null;
    situationText: string | null;
  };
  consentActive: boolean;
  consents: Array<{
    id: string;
    textVersion: string;
    textSnapshot: string;
    recipientName: string;
    recordedAt: string;
    withdrawnAt: string | null;
    withdrawnReason: string | null;
    dataCategories: string[];
    ipMasked: string | null;
  }>;
  documents: TeamDocument[];
  timeline: Array<{ kind: string; title: string; body: string | null; at: string; authorKind: string; authorLabel: string | null; visible: boolean }>;
  coordination: CoordinationItem[];
  messages: Array<{ id: string; authorKind: "applicant" | "team"; authorLabel: string; body: string; isInternal: boolean; at: string }>;
  duplicateOf: string | null;
  notEligibleReason: string | null;
  outcome: { code: string | null; summary: string | null } | null;
  actions: TeamAction[];
  timer: TeamTimer;
  can: {
    assign: boolean;
    take: boolean;
    identityCheck: boolean;
    coordinate: boolean;
    message: boolean;
    note: boolean;
    recordOffer: boolean;
    verify: boolean;
    relay: boolean;
    close: boolean;
    recordExecution: boolean;
    verifyExecution: boolean;
    refer: boolean;
    answerConcerns: boolean;
  };
  execution: TeamExecution;
  concerns: Array<{ id: string; reference: string; kind: string; subject: string; text: string; status: "open" | "answered"; outcome: string | null; responseText: string | null; respondedByLabel: string | null; respondedAt: string | null; createdAt: string }>;
  referrals: Array<{ specialistType: string; specialistName: string; note: string | null; applicantText: string; recordedByLabel: string; at: string }>;
  offers: TeamOffer[];
  responses: Array<{ id: string; reference: string; kind: string; text: string | null; at: string; relayedAt: string | null; consentTextSnapshot: string | null; otpVerifiedAt: string | null }>;
}

export interface TeamOffer {
  id: string;
  versionNo: number;
  path: "p1" | "p2" | "p3";
  status: "pendingverification" | "returned" | "published" | "superseded";
  newInstallment: number | null;
  termMonths: number | null;
  startText: string | null;
  settlementAmount: number | null;
  paymentConditions: string | null;
  remainingText: string | null;
  saleTerms: string | null;
  conditions: string | null;
  effectText: string;
  lenderReference: string;
  lenderLetterDate: string;
  lenderValidityText: string | null;
  letterDocumentId: string;
  shareLetterWithApplicant: boolean;
  recordedByLabel: string;
  recordedAt: string;
  recordedByMe: boolean;
  verifiedByLabel: string | null;
  verifiedAt: string | null;
  verificationChecklist: string[];
  returnReason: string | null;
  publishedAt: string | null;
}

/** Phase 1A-2 (ADR 0002): what the lender sent, recorded by the team and verified by a second member. */
export interface ExecutionRecord {
  id: string;
  kind: "agreement" | "payment_confirmation" | "lender_notice" | "closure_document";
  status: "pending_verification" | "returned" | "published" | "superseded";
  sourceDocumentId: string;
  shareSourceWithApplicant: boolean;
  lenderReference: string;
  lenderDate: string;
  summaryText: string;
  explanationText: string;
  offerId: string | null;
  path: string | null;
  activationDate: string | null;
  newInstallment: number | null;
  termMonths: number | null;
  settlementAmount: number | null;
  termsText: string | null;
  amount: number | null;
  receivedOn: string | null;
  scheduleItemNo: number | null;
  answersReportId: string | null;
  noticeCategory: string | null;
  documentKind: string | null;
  supersedesRecordId: string | null;
  correctionReason: string | null;
  recordedByLabel: string;
  recordedAt: string;
  recordedByMe: boolean;
  verifiedByLabel: string | null;
  verifiedAt: string | null;
  verificationChecklist: string[];
  returnReason: string | null;
  publishedAt: string | null;
  schedule: Array<{ no: number; dueDate: string; amount: number }>;
}

export interface PaymentReport {
  id: string;
  reference: string;
  amount: number;
  transferDate: string;
  bankReference: string | null;
  scheduleItemNo: number | null;
  proofDocumentId: string;
  status: "reported" | "not_confirmed_yet" | "confirmed_by_lender";
  teamNote: string | null;
  confirmationRecordId: string | null;
  at: string;
  answeredAt: string | null;
}

export interface TeamExecution {
  path: string | null;
  relevantClosureKinds: string[];
  records: ExecutionRecord[];
  paymentReports: PaymentReport[];
}

export interface ExecutionVerifyItem {
  reference: string;
  institutionName: string;
  recordId: string;
  kind: ExecutionRecord["kind"];
  recordedByLabel: string;
  recordedAt: string;
  recordedByMe: boolean;
}

export interface VerifyItem {
  reference: string;
  institutionName: string;
  offerId: string;
  versionNo: number;
  path: string;
  recordedByLabel: string;
  recordedAt: string;
  recordedByMe: boolean;
}

export interface ConcernQueue {
  tab: "open" | "answered";
  items: Array<{
    id: string;
    reference: string;
    kind: string;
    subject: string;
    text: string;
    createdAt: string;
    outcome: string | null;
    responseText: string | null;
    respondedByLabel: string | null;
    respondedAt: string | null;
    requestReference: string;
    requestStatus: string;
    institutionName: string;
    applicantName: string;
    mayAnswer: boolean;
    daysOpen: number;
  }>;
}
