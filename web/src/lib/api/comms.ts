/** L22 case communications DTOs (mirrors WorkspaceEndpoints.CaseComms in server/src/Rahoon.Api/Modules/Communications). */

/** `MessageChannel.ToString()` on the server. */
export type MessageChannelKey = "Portal" | "Sms" | "Email" | "Call" | "Internal";

export interface CaseMessageDto {
  id: string;
  channel: MessageChannelKey;
  /** lender | owner | system */
  authorType: string;
  author: string;
  body: string;
  at: string;
  internalOnly: boolean;
  templateKey: string | null;
  readByOwner: boolean;
}

export interface AppointmentDto {
  id: string;
  /** call | visit | inspection */
  type: string;
  startsAt: string;
  /** Proposed | Confirmed | Rescheduled | Cancelled | Done */
  status: string;
  attendees: string | null;
  /** lender | owner */
  proposedBy: string;
}

export interface CaseTaskDto {
  id: string;
  title: string;
  dueOn: string | null;
  /** Open | Done | Cancelled */
  status: string;
  assignee: string | null;
}

export interface CaseCommsData {
  messages: CaseMessageDto[];
  appointments: AppointmentDto[];
  tasks: CaseTaskDto[];
  preferences: { contactHours: string | null; channels: string[] | null; invitation: string | null };
  hardship: { reasonKey: string | null; createdAt: string; status: string } | null;
  openComplaints: number;
  templates: Array<{ code: string; title: string; bodyAr: string }>;
}

/** `GET /api/org/members` row (task assignee picker). */
export interface OrgMember {
  id: string;
  name: string;
  title: string | null;
  team: string | null;
  roles: string[];
}

export const CHANNEL_LABEL: Record<MessageChannelKey, string> = {
  Portal: "البوابة",
  Sms: "رسالة نصية",
  Email: "البريد الإلكتروني",
  Call: "مكالمة هاتفية",
  Internal: "داخلي",
};

/** Owner contact-preference channel keys (CasePartyEndpoints.ChannelLabels). */
export const PREF_CHANNEL_LABEL: Record<string, string> = {
  platform: "المنصة",
  sms: "رسائل نصية",
  email: "البريد الإلكتروني",
  call: "مكالمة هاتفية",
};

export const APPOINTMENT_TYPE_LABEL: Record<string, string> = { call: "مكالمة", visit: "زيارة", inspection: "معاينة" };

export const APPOINTMENT_STATUS_LABEL: Record<string, string> = {
  Proposed: "موعد مقترح · بانتظار تأكيد المالك",
  Confirmed: "أكّده المالك",
  Rescheduled: "أُعيدت جدولته",
  Cancelled: "ملغى",
  Done: "تم",
};

/** Owner hardship reasons as worded in the owner journey (B6). */
export const HARDSHIP_REASON_LABEL: Record<string, string> = {
  income_loss: "فقدت عملي أو انخفض دخلي",
  health: "ظرف صحي",
  family: "تغيّر في الأسرة",
  other: "سبب آخر",
  prefer_not_say: "أفضل ألا أذكر السبب",
};

export const HARDSHIP_STATUS_LABEL: Record<string, string> = { open: "مفتوح", contacted: "تم التواصل", closed: "مغلق" };

export const INVITATION_LABEL: Record<string, string> = {
  NotSent: "لم تُرسل",
  Sent: "دعوة مرسلة",
  Accepted: "مقبولة",
  Expired: "منتهية",
  Revoked: "ملغاة",
};
