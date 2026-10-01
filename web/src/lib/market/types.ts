/** Shapes of the market API (server/src/Rahoon.Api/Modules/Market). Kept in step with the C# projections. */

export type FieldType = "integer" | "decimal" | "money" | "text" | "longText" | "select" | "multiSelect" | "boolean" | "date" | "month";

export interface FieldOption {
  value: string;
  label: string;
  propertyTypes?: string[] | null;
}

export interface FieldDef {
  key: string;
  scope: "property" | "obligation";
  label: string;
  type: FieldType;
  propertyTypes?: string[] | null;
  obligationKinds?: string[] | null;
  submitAnswer: boolean;
  publishKnown: boolean;
  initial: boolean;
  allowUnknown: boolean;
  min?: number | null;
  max?: number | null;
  unit?: string | null;
  help?: string | null;
  options?: FieldOption[] | null;
  when?: { key: string; values: string[] } | null;
}

export interface DocumentDef {
  key: string;
  label: string;
  obligationKinds: string[];
  publishRequired: boolean;
  help?: string | null;
}

export interface CityDef {
  key: string;
  label: string;
  lat: number;
  lng: number;
  districts: string[];
}

export interface Party {
  id: string;
  kind: "developer" | "financier";
  name: string;
  isDemo: boolean;
}

export interface Catalog {
  propertyTypes: FieldOption[];
  obligationModes: FieldOption[];
  obligationKinds: FieldOption[];
  frequencies: FieldOption[];
  fields: FieldDef[];
  documents: DocumentDef[];
  cities: CityDef[];
  unknown: string;
  parties: Party[];
  commission: { approved: boolean; text: string };
}

export type Answers = Record<string, string>;

export interface CalcLine {
  key: string;
  label: string;
  value: number | null;
  payee: string;
  bornBy: string;
  timing: "now" | "later" | "info";
  note?: string | null;
  state?: string | null;
}

export interface TermsResult {
  complete: boolean;
  quality: "complete_verified" | "complete_estimate" | "incomplete";
  qualityText: string;
  missing: string[];
  lines: CalcLine[];
  notes: string[];
  gap: boolean;
  ownerAmount: number | null;
  sellerNet: number | null;
  dueNow: number | null;
  futureBalance: number | null;
  buyerTotal: number | null;
  installment: number | null;
  installmentFrequency: string | null;
  installmentMonthlyEquivalent: number | null;
  largestExtraPayment: number | null;
  remainingMonths: number | null;
  needsNewFinancing: boolean;
  commission: { policyApproved: boolean; amount: number | null; text: string };
}

export interface MissingItem {
  key: string;
  label: string;
}

export interface CompletenessGroup {
  key: "property" | "figures" | "media" | "review";
  label: string;
  done: boolean;
  missing: MissingItem[];
}

export interface MarketEvent {
  id: string;
  kind: string;
  title: string;
  body: string | null;
  reason: string | null;
  at: string;
  actor: string;
  actorLabel: string | null;
  fromStatus: string | null;
  toStatus: string | null;
  visible: boolean;
}

export interface Obligation {
  id: string;
  kind: "developer" | "financier";
  partyId: string | null;
  partyOtherName: string | null;
  partyName: string;
  relationNote: string | null;
  answers: Answers;
  verified: { key: string; value: string; source: string; sourceDate: string }[];
}

export type FileReview = "pending" | "accepted" | "rejected";

export interface PrivateDoc {
  id: string;
  kind: string;
  kindLabel: string;
  obligationId: string | null;
  fileName: string;
  sizeBytes: number;
  contentType: string;
  reviewStatus: FileReview;
  reviewNote: string | null;
  uploadedAt: string;
  source: string;
  url: string;
}

export interface Photo {
  id: string;
  url: string;
  isCover: boolean;
  sortOrder: number;
  reviewStatus: FileReview;
  reviewNote: string | null;
}

export type SaleStatus = "draft" | "submitted" | "underReview" | "needsCompletion" | "approvedForListing" | "rejected" | "withdrawn";

export interface SaleFile {
  reference: string;
  status: SaleStatus;
  statusLabel: string;
  nextStep: string;
  editable: boolean;
  canWithdraw: boolean;
  filesEditable: boolean;
  createdAt: string;
  updatedAt: string;
  submittedAt: string | null;
  isDemo: boolean;
  propertyType: string | null;
  city: string | null;
  district: string | null;
  project: string | null;
  obligationMode: string | null;
  answers: Answers;
  obligations: Obligation[];
  location: { lat: number | null; lng: number | null; label: string | null; displayWish: string | null };
  contact: { name: string | null; email: string | null; relationship: string | null };
  completeness: CompletenessGroup[];
  openCompletion: { id: string; items: MissingItem[]; note: string; requestedAt: string; requestedByLabel: string; answeredAt: string | null } | null;
  documents: PrivateDoc[];
  photos: Photo[];
  events: MarketEvent[];
  estimate: TermsResult;
  opportunity: { reference: string; status: string; statusLabel: string; awaitingYou: boolean } | null;
}

export interface SaveResponse {
  saved: boolean;
  invalid: Record<string, string>;
  file: SaleFile;
}

export interface Spec {
  key: string;
  label: string;
  value: string;
}

export interface OpportunityCard {
  reference: string;
  title: string;
  city: string;
  cityLabel: string;
  district: string | null;
  project: string | null;
  propertyType: string;
  propertyTypeLabel: string;
  track: "developer" | "financier" | "mixed";
  readiness: string | null;
  readinessLabel: string | null;
  deliveryMonth: string | null;
  area: number | null;
  bedrooms: number | null;
  bathrooms: number | null;
  specs: Spec[];
  coverUrl: string | null;
  photoCount: number;
  dueNow: number | null;
  buyerTotal: number | null;
  futureBalance: number | null;
  installment: number | null;
  installmentFrequency: string | null;
  installmentFrequencyLabel: string | null;
  installmentMonthlyEquivalent: number | null;
  largestExtraPayment: number | null;
  needsNewFinancing: boolean;
  quality: TermsResult["quality"];
  complete: boolean;
  verifiedOn: string | null;
  publishedAt: string | null;
  saved: boolean;
  isDemo: boolean;
  location: { lat: number; lng: number; precision: "exact" | "approximate" } | null;
}

export interface Fit {
  fits: boolean;
  comparable: boolean;
  reasons: string[];
  limits: string[];
}

export interface SearchResult {
  items: { card: OpportunityCard; fit: Fit | null }[];
  total: number;
  page: number;
  pageSize: number;
  pages: number;
  excludedIncomplete: number;
  sort: string;
}

export interface TermsView extends Omit<TermsResult, "commission"> {
  id: string;
  versionNo: number;
  status: string;
  track: string;
  transferConditions: string | null;
  verificationScope: string | null;
  verifiedOn: string | null;
  installmentFrequencyLabel: string | null;
  commission: TermsResult["commission"];
  sentToOwnerAt: string | null;
  ownerDecidedAt: string | null;
  ownerNote: string | null;
}

export interface OpportunityContent {
  title: string;
  description: string | null;
  propertyType: string;
  propertyTypeLabel: string;
  city: string;
  cityLabel: string;
  district: string | null;
  project: string | null;
  track: string;
  trackLabel: string;
  area: number | null;
  bedrooms: number | null;
  bathrooms: number | null;
  readiness: string | null;
  readinessLabel: string | null;
  deliveryMonth: string | null;
  specs: Spec[];
  features: string[];
}

export interface OpportunityDetail {
  card: OpportunityCard;
  content: OpportunityContent;
  photos: { id: string; url: string }[];
  terms: TermsView;
  location: { lat: number; lng: number; precision: "exact" | "approximate"; googleMapsUrl: string } | null;
  approvals: { party: string; status: string; statusLabel: string; conditions: string | null; expiresOn: string | null }[];
  fit: { fit: Fit; hasBuyerRequest: boolean } | null;
  myInterest: { reference: string; status: string; statusLabel: string } | null;
  status: string;
  statusLabel: string;
}

export interface BuyerRequestView {
  reference: string;
  status: "draft" | "submitted" | "underReview" | "needsCompletion" | "approvedForMatching" | "rejected" | "withdrawn";
  statusLabel: string;
  nextStep: string;
  editable: boolean;
  availableNow: number | null;
  installmentComfort: number | null;
  installmentFrequency: string | null;
  maxPrice: number | null;
  purchaseMode: string | null;
  cities: string[];
  areasText: string | null;
  propertyTypes: string[];
  areaMin: number | null;
  areaMax: number | null;
  bedroomsMin: number | null;
  readiness: string | null;
  deliveryBy: string | null;
  contactName: string | null;
  submittedAt: string | null;
  createdAt: string;
  updatedAt: string;
  decisionReason: string | null;
  isDemo: boolean;
  installmentMonthlyEquivalent: number | null;
  capacity: {
    declared: { availableNow: number | null; installmentComfort: number | null; installmentFrequency: string | null; maxPrice: number | null };
    reviewed: { amount: number | null; note: string | null; at: string; by: string | null } | null;
    financeApproval: { status: string; statusLabel: string; source: string | null; date: string | null; amount: number | null };
  };
  openCompletion: { note: string; requestedAt: string; items: string[] } | null;
}

export interface AccountSummary {
  saleRequests: {
    reference: string;
    status: SaleStatus;
    statusLabel: string;
    nextStep: string;
    propertyTypeLabel: string | null;
    cityLabel: string | null;
    district: string | null;
    updatedAt: string;
    submittedAt: string | null;
    opportunity: { reference: string; status: string; statusLabel: string; awaitingYou: boolean } | null;
  }[];
  buyerRequest: { reference: string; status: string; statusLabel: string; nextStep: string } | null;
  interests: number;
  saved: number;
  unread: number;
  notifications: { id: string; title: string; body: string | null; reason: string | null; at: string; read: boolean; link: string }[];
}
