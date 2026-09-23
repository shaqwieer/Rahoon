/**
 * Colour triplets (fg, bg, border) from 02 Components `T` and the case-state table.
 * Colour is always supported by an icon + text — never the only carrier of meaning.
 */
export type Tone = "neutral" | "info" | "warn" | "ok" | "err" | "sel" | "dark" | "ext";

export const TONES: Record<Tone, { fg: string; bg: string; bd: string }> = {
  neutral: { fg: "#22262A", bg: "#F2F1ED", bd: "#CBCAC6" },
  info: { fg: "#1D5A8C", bg: "#EAF2F9", bd: "#9DC0DE" },
  warn: { fg: "#8A5300", bg: "#FBF2DE", bd: "#E2C27A" },
  ok: { fg: "#1E6A45", bg: "#EAF4EE", bd: "#9CCBB0" },
  err: { fg: "#B3261E", bg: "#FCECEA", bd: "#EFA59C" },
  sel: { fg: "#8E3920", bg: "#FDF0EB", bd: "#F0B8A6" },
  dark: { fg: "#FFFFFF", bg: "#22262A", bd: "#22262A" },
  ext: { fg: "#22262A", bg: "#FFFFFF", bd: "#85847F" },
};

/** The 16 case states (09 Handoff `CaseState` enum) with their C02 icon and tone. Labels live in the dictionaries. */
export const CASE_STATES = {
  draft: { icon: "edit_note", tone: "neutral" },
  awaiting_data: { icon: "hourglass_top", tone: "info" },
  verification: { icon: "fact_check", tone: "info" },
  valuation: { icon: "query_stats", tone: "info" },
  proposed_solution: { icon: "tips_and_updates", tone: "sel" },
  internal_approval: { icon: "approval", tone: "warn" },
  awaiting_customer: { icon: "hourglass_empty", tone: "info" },
  negotiation: { icon: "forum", tone: "sel" },
  active_settlement: { icon: "handshake", tone: "ok" },
  voluntary_sale: { icon: "sell", tone: "ok" },
  judicial_referral: { icon: "outbound", tone: "ext" },
  external_judicial_sale: { icon: "open_in_new", tone: "ext" },
  awaiting_reconciliation: { icon: "calculate", tone: "warn" },
  closed: { icon: "check_circle", tone: "dark" },
  paused: { icon: "pause_circle", tone: "neutral" },
  cancelled: { icon: "cancel", tone: "neutral" },
} as const satisfies Record<string, { icon: string; tone: Tone }>;

export type CaseStatusKey = keyof typeof CASE_STATES;
export const CASE_STATUS_KEYS = Object.keys(CASE_STATES) as CaseStatusKey[];

export function isCaseStatusKey(v: unknown): v is CaseStatusKey {
  return typeof v === "string" && v in CASE_STATES;
}
