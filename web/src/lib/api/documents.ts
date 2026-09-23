/** L10 document DTOs (mirrors server/src/Rahoon.Api/Modules/Documents/{DocumentEndpoints,DocumentRequestPreviewEndpoints}.cs). */

export type DocumentFilter = "all" | "requested" | "in_review" | "expiring";

export interface CaseDocumentItem {
  id: string;
  name: string;
  documentTypeKey: string;
  icon: string;
  /** "v2" or "—" when nothing was uploaded yet. */
  version: string;
  versionCount: number;
  source: "Owner" | "Lender" | "CoreSystem" | "Provider" | "Legal" | string;
  statusText: string;
  tone: "ok" | "warn" | "err" | "info" | "neutral" | string;
  statusIcon: string;
  rawStatus: "Requested" | "Uploaded" | "InReview" | "Verified" | "Rejected" | "Expired" | string;
  validUntil: string | null;
  visibleTo: string[];
  visibleToOwner: boolean;
  meta: string;
  currentVersionId: string | null;
  /** Review status of the current version: Pending | Verified | Rejected. */
  currentReview: "Pending" | "Verified" | "Rejected" | null;
  scan: string | null;
  request: { id: string; dueOn: string; ownerMessage: string } | null;
}

export interface DocumentListData {
  items: CaseDocumentItem[];
  counts: { all: number; requested: number; inReview: number; expiring: number };
}

export interface DocumentVersionDto {
  id: string;
  versionNo: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByLabel: string;
  uploadedAt: string;
  review: "Pending" | "Verified" | "Rejected";
  reviewNote: string | null;
  ownerFacingReason: string | null;
  scan: string;
  sha256: string;
}

export interface DocumentTypeDto {
  key: string;
  nameAr: string;
  icon: string;
  validityDays: number | null;
}

export interface DocumentRequestPreview {
  ownerMessage: string | null;
  template: { code: string; title: string; versionNo: number } | null;
  caption: string;
  channels: string[];
  dueOn: string;
  dueOnHijri: string;
  daysFromToday: number;
  dueLabel: string;
  rule: { formats: string[]; validityDays: number | null; visibleTo: string[]; uploader: string | null; helper: string };
  saved: false;
}

/** Same-origin file route (GET, audited + watermark logged by the API). */
export const versionFileHref = (reference: string, versionId: string) =>
  `/api/cases/${encodeURIComponent(reference)}/documents/versions/${versionId}/file`;
