/**
 * DTOs for the case information tabs L06 (parties), L07 (finance) and L08/L09 (property & mortgage).
 * Mirrors server/src/Rahoon.Api/Modules/Cases/{CasePartyEndpoints,CaseFinanceEndpoints,CasePropertyEndpoints}.cs.
 */

export type NoteTone = "ok" | "warn" | "info" | "muted" | "err";

export interface PartyFact {
  k: string;
  v: string;
  ltr: boolean;
}

export type PartyRoleKey = "owner_borrower" | "co_borrower" | "guarantor" | "agent" | "legal_representative" | "occupant" | "informal_representative";

export interface PartyDto {
  id: string;
  name: string;
  initials: string;
  avatar: "primary" | "subtle";
  role: PartyRoleKey;
  roleLabel: string;
  kind: "organization" | "individual";
  isPrimary: boolean;
  isContractParty: boolean;
  relation: string | null;
  nationalIdMasked: string | null;
  phoneMasked: string | null;
  emailMasked: string | null;
  language: string;
  employmentStatus: string | null;
  contactAllowed: boolean;
  specialNeeds: string | null;
  notes: string | null;
  identityVerifiedAt: string | null;
  identityVerifiedVia: string | null;
  poaStatus: "none" | "requested" | "uploaded" | "verified";
  poaDueOn: string | null;
  financialVisibility: boolean;
  facts: PartyFact[];
  note: { icon: string; tone: NoteTone; text: string } | null;
  actions: { message: boolean; edit: boolean; requestPoa: boolean; reveal: boolean };
}

export type InvitationStatus = "not_sent" | "sent" | "accepted" | "expired" | "revoked";

export interface ContactPreferencesDto {
  invitation: {
    status: InvitationStatus;
    label: string;
    invitedAt: string | null;
    acceptedAt: string | null;
    expiresAt: string | null;
    canInvite: boolean;
    disabledReason: string | null;
    actionLabel: string;
  };
  identityVerification: { verified: boolean; label: string; at: string | null; method: string | null };
  allowedChannels: string[];
  allowedChannelsLabel: string;
  contactHours: string | null;
  communicationNeeds: string | null;
  lastContactAt: string | null;
  lastContactLabel: string;
  channelOptions: Array<{ key: string; label: string }>;
}

export interface PartiesData {
  count: number;
  items: PartyDto[];
  contact: ContactPreferencesDto;
  revealNote: string;
  canAdd: boolean;
  addableRoles: Array<{ key: PartyRoleKey; label: string }>;
}

export interface InvitationResult {
  status: "sent";
  refreshed: boolean;
  expiresAt: string;
  destination: string;
  channel: "sms";
  smsState: string;
  /** Returned only in development / testing sandboxes — never in production. */
  devLink: string | null;
}

/* ───────── L07 finance ───────── */

export type SyncStatusKey = "ok" | "delayed" | "failed" | "manual";

export interface FinanceData {
  banner: { tone: "info" | "warn" | "err"; icon: string; text: string } | null;
  snapshot: {
    source: string;
    asOf: string;
    syncStatus: SyncStatusKey;
    ageHours: number;
    staleTag: string | null;
    items: Array<{ code: string; label: string; amount: number; source: string | null; asOf: string }>;
    total: number;
    totalSource: string;
    recordedTotal: number;
    totalVerified: boolean;
    totalWarning: string | null;
  } | null;
  contract: {
    contractNumberMasked: string;
    contractDate: string | null;
    productType: string | null;
    lender: string;
    originalAmount: number | null;
    originalTermMonths: number | null;
    remainingTermMonths: number | null;
    originalInstallment: number | null;
    profitType: string | null;
    firstOverdueDate: string | null;
    termLabel: string | null;
    profitLabel: string;
  } | null;
  history: {
    title: string;
    originalInstallment: number | null;
    months: Array<{
      month: string;
      shortLabel: string;
      status: "paid" | "unpaid" | "partial" | "due";
      label: string;
      icon: string;
      amountDue: number;
      amountPaid: number;
      aria: string;
    }>;
    summary: string | null;
  };
  arrears: {
    reportedAmount: number | null;
    reportedInstallments: number | null;
    reportedSince: string | null;
    source: string | null;
    derived: { missedCount: number; partialCount: number; since: string | null; shortfall: number };
  };
  canRequestCorrection: boolean;
  correctionFields: Array<{ key: string; label: string }>;
}

/* ───────── L08/L09 property & mortgage ───────── */

export interface PropertyData {
  property: {
    type: string;
    city: string;
    district: string | null;
    location: string;
    landAreaM2: number | null;
    builtAreaM2: number | null;
    yearBuilt: number | null;
    deedMasked: string | null;
    occupancy: "OwnerFamily" | "Tenant" | "Vacant" | "Unknown" | string;
    occupancyLabel: string;
    occupancyNote: string | null;
    shortLabel: string | null;
    valuationValue: number | null;
    valuationSource: string | null;
    photo: { documentId: string; versionId: string | null; name: string; source: string | null } | null;
    protectionNote: { title: string; body: string } | null;
  } | null;
  mortgage: {
    mortgagee: string;
    rank: number;
    rankLabel: string;
    registeredOn: string | null;
    otherEncumbrances: string;
    insuranceValidUntil: string | null;
    insuranceLabel: string;
    insuranceTone: "ok" | "warn" | "err" | "neutral";
    deedMatched: boolean;
    deedMatchedOn: string | null;
    deedMatchLabel: string;
    verificationSource: string | null;
    legalReview: {
      status: "complete" | "pending";
      label: string;
      note: string | null;
      reviewer: string | null;
      reviewerRole: string | null;
      at: string | null;
      footer: string | null;
    };
  } | null;
  inspection: {
    at: string;
    attendedBy: string | null;
    status: "completed" | "scheduled" | "proposed";
    statusLabel: string;
    text: string;
    providerAssignment: string | null;
  } | null;
  blocksProposal: string | null;
  canEditProperty: boolean;
  canLegalReview: boolean;
}
