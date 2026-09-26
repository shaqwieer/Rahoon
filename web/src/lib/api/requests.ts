import type { ArrearsKey, DocKindKey, PreferenceKey, RequestStatusKey, WaitingKey } from "@/components/individual/copy";

/** Shapes of /api/my/requests (server/src/Rahoon.Api/Modules/Requests/MyRequestEndpoints.cs). */

export interface Institution {
  id: string;
  nameAr: string;
  nameEn: string | null;
  kind: "bank" | "finance_company";
}

export interface MyRequestListItem {
  reference: string;
  status: RequestStatusKey;
  waitingOn: WaitingKey;
  institutionName: string;
  createdAt: string;
  submittedAt: string | null;
  statusChangedAt: string;
}

export interface MyRequestDocument {
  id: string;
  kind: DocKindKey | "lender_letter";
  name: string;
  addedAfterSubmit: boolean;
  versionId: string | null;
  fileName: string | null;
  uploadedAt: string | null;
  sizeBytes: number | null;
  scan: string | null;
}

export interface MyRequestTimelineEntry {
  kind: string;
  title: string;
  body: string | null;
  at: string;
  authorKind: "applicant" | "team" | "system";
}

export interface MyRequestDetail {
  reference: string;
  status: RequestStatusKey;
  statusLabel: string;
  waitingOn: WaitingKey;
  nextStepText: string | null;
  createdAt: string;
  submittedAt: string | null;
  statusChangedAt: string;
  institution: { id: string; nameAr: string; nameEn: string | null } | null;
  institutionOtherName: string | null;
  institutionName: string;
  fields: {
    applicantFullName: string | null;
    contractNumber: string | null;
    monthlyInstallment: number | null;
    arrearsDuration: ArrearsKey | null;
    propertyCity: string | null;
    pathPreference: PreferenceKey | null;
    affordableMonthly: number | null;
    situationText: string | null;
  };
  consent: { textVersion: string; textSnapshot: string; recipientName: string; recordedAt: string } | null;
  consentText: { version: string; text: string };
  documents: MyRequestDocument[];
  timeline: MyRequestTimelineEntry[];
  duplicate: { reference: string; status: RequestStatusKey } | null;
  duplicateAcknowledged: boolean;
  missing: string[];
  outcome: { code: string | null; summary: string | null } | null;
  notEligibleReason: string | null;
  canEdit: boolean;
  canAddInfo: boolean;
  canWithdraw: boolean;
  concerns: Array<{ reference: string; kind: "objection" | "complaint"; subject: string; text: string; status: "open" | "answered"; outcome: string | null; responseText: string | null; respondedAt: string | null; createdAt: string }>;
  referrals: Array<{ specialistType: string; specialistName: string; applicantText: string; at: string }>;
  canRaiseConcern: boolean;
  offer: MyOffer | null;
  canRespond: boolean;
  offerAcceptText: { version: string; text: string } | null;
  responses: Array<{ reference: string; kind: "accept" | "decline" | "question" | "counter"; text: string | null; at: string; relayed: boolean; consentTextSnapshot: string | null }>;
}

export interface MyOffer {
  id: string;
  versionNo: number;
  path: "p1" | "p2" | "p3";
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
  publishedAt: string | null;
  letterVersionId: string | null;
  verified: boolean;
}
