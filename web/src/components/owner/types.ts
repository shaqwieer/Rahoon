/**
 * Owner portal response shapes — mirror server/src/Rahoon.Api/Modules/Owner/OwnerEndpoints.cs and
 * Communications/WorkspaceEndpoints.cs (`/api/notifications`). Anonymous-type members are camelCase; values
 * produced with `.ToString()` on C# enums arrive PascalCase ("Sent", "Rejected", "Complaint" …).
 * Text fields (titles, bodies, statuses) are Arabic copy authored by the server.
 */

export type OwnerTone = "ok" | "warn" | "err" | "info" | "neutral";

/* GET /owner/home */
export type NextStepType = "offer" | "document" | "agreement" | "payment" | "closed";
export interface OwnerNextStep {
  type: NextStepType;
  title: string;
  body: string;
  deadline: string | null;
  daysLeft: number | null;
  cta: string;
  route: string;
}
export interface OwnerHome {
  greetingName: string;
  lenderName: string;
  caseRef: string;
  nextStep: OwnerNextStep | null;
  journey: { current: number; total: number; label: string };
  docsSummary: string;
  debtSummary: string;
  caseManager: { firstName: string; initials: string; role: string; nextAppointment: string | null } | null;
  unreadCount: number;
}

/* GET /owner/journey */
export interface OwnerJourney {
  steps: Array<{ title: string; description: string; status: "done" | "current" | "todo"; meta: string }>;
  note: string;
}

/* GET /owner/documents */
export interface OwnerDocument {
  id: string;
  title: string;
  status: string;
  tone: OwnerTone;
  /** DocumentStatus: Requested | Rejected | Uploaded | InReview | Verified | … */
  rawStatus: string;
  reason: string | null;
  help: string | null;
  dueOn: string | null;
  requestedBy: string;
  typeKey: string | null;
  canUpload: boolean;
}
export interface OwnerDocuments {
  items: OwnerDocument[];
  needsAction: number;
}

/* GET /owner/debt */
export interface OwnerDebt {
  total: number | null;
  currency?: string;
  source?: string;
  items?: Array<{ key: string; label: string; amount: number; explanation: string }>;
}

/* GET /owner/options */
export interface OwnerOptions {
  activeOffer: { id: string; eyebrow: string; title: string; body: string } | null;
  otherOptions: Array<{ key: "grace" | "voluntary_sale"; title: string; description: string; link: string }>;
}

/* GET /owner/offers/{id} */
export type OfferStatus = "Sent" | "Expired" | "Countered" | "Accepted" | "Declined" | "Withdrawn";
export type SolutionKind = "Reschedule" | "ReducedPayoff" | "GracePeriod" | "VoluntarySale";
export interface OwnerOffer {
  id: string;
  status: OfferStatus;
  validUntil: string;
  version: number;
  kind: SolutionKind;
  installment: number;
  dueDay: number;
  termMonths: number;
  start: string;
  end: string;
  waiver: number;
  rescheduled: number;
  cureDays: number;
  missedConsecutive: number;
  phoneMasked: string | null;
  summary: string[];
}

/* POST /owner/offers/{id}/consent/otp */
export interface ConsentOtpIssued {
  destination: string;
  resendInSeconds: number;
  sandboxCode?: string | null;
}
/* POST /owner/offers/{id}/consent */
export interface ConsentResult {
  agreementRef: string;
  recordedAt: string;
  hijriDate: string;
  firstDue: string;
  message: string;
}

/* GET /owner/agreement */
export type AgreementStatus = "PendingActivation" | "Active" | "BreachReview" | "Completed" | "Terminated";
export interface OwnerAgreement {
  agreement: {
    number: string;
    status: AgreementStatus;
    versionLabel: string;
    lender: string;
    rescheduledAmount: number;
    waiverAmount: number;
    installmentCount: number;
    installmentAmount: number;
    dueDay: number;
    startDate: string;
    endDate: string;
    startHijri: string;
    breachMissedConsecutive: number;
    breachCureDays: number;
    consentAt: string | null;
    consentHijri: string | null;
    signature: string;
  } | null;
}

/* GET /owner/payments */
export interface OwnerInstallment {
  no: number;
  dueDate: string;
  amount: number;
  status: string;
  tone: OwnerTone;
  meta: string;
  canNotify: boolean;
}
export type OwnerPayments =
  | { active: false; message: string }
  | {
      active: true;
      total: number;
      next: { no: number; amount: number; dueDate: string } | null;
      note: string;
      items: OwnerInstallment[];
      howToPay: { title: string; steps: string[] };
    };

/* GET /owner/messages */
export interface OwnerAppointment {
  id: string;
  type: string;
  startsAt: string;
  /** AppointmentStatus: Proposed | Confirmed | Rescheduled | Done */
  status: string;
  statusText: string;
}
export interface OwnerMessage {
  id: string;
  mine: boolean;
  body: string;
  at: string;
  author: string;
  read: boolean;
}
export interface OwnerMessages {
  with: string | null;
  replyHint: string;
  appointments: OwnerAppointment[];
  messages: OwnerMessage[];
}

/* GET /owner/complaints */
export interface OwnerComplaint {
  reference: string;
  /** ComplaintType: Complaint | Objection | Appeal */
  type: string;
  subject: string;
  /** ComplaintStatus: Received | InReview | AwaitingOwner | Resolved | Escalated | Closed */
  status: string;
  submittedAt: string;
  dueOn: string | null;
  response: string | null;
  respondedAt: string | null;
}
export interface ComplaintSubmitted {
  reference: string;
  dueOn: string;
  message: string;
}

/* GET /owner/closure */
export interface OwnerClosure {
  closed: boolean;
  closedAt: string | null;
  documents: Array<{ title: string; date: string | null; fileVersionId: string | null }>;
  accessExpiresAt: string | null;
}

/* GET /notifications */
export interface OwnerNotification {
  id: string;
  category: string;
  title: string;
  body: string | null;
  link: string | null;
  tone: string;
  createdAt: string;
  read: boolean;
}
export interface OwnerNotifications {
  items: OwnerNotification[];
  unread: number;
}
