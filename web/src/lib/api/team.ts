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
  };
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
