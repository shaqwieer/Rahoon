"use client";

import { useState, type ReactNode } from "react";
import { CaseHeader } from "@/components/shell/CaseHeader";
import { LocaleSwitch } from "@/components/shell/LocaleSwitch";
import { DebtorNav, DebtorTop } from "@/components/shell/OwnerShell";
import { PlatformSidebar } from "@/components/shell/PlatformShell";
import { SettingsNav } from "@/components/shell/SettingsNav";
import type { ShellUser } from "@/components/shell/types";
import {
  Alert,
  AmountField,
  ApprovalChain,
  AuditTimeline,
  Avatar,
  BarList,
  Button,
  CASE_STATUS_KEYS,
  CaseTable,
  Checkbox,
  DateField,
  DateText,
  DecisionSupportTag,
  Dialog,
  DocumentItem,
  DocumentList,
  Drawer,
  EmptyState,
  ErrorSummary,
  FilterChip,
  IconButton,
  IntegrationStateTag,
  KeyValueList,
  KpiTile,
  Logo,
  MaskedValue,
  Menu,
  Money,
  NextActionCard,
  OtpInput,
  Pagination,
  PercentField,
  RadioCardGroup,
  Ref,
  ReviewScreen,
  SandboxCodeBox,
  Select,
  Skeleton,
  SlaBadge,
  StageProgress,
  StatusChip,
  SubStatusTag,
  SystemState,
  Tabs,
  Tag,
  Textarea,
  TextField,
  useToast,
  VersionDiff,
  type CaseRowData,
} from "@/components/ui";
import { formatDate, formatDateTime, formatHijri, formatMoney, formatPercent, slaText } from "@/lib/format";

/* Design fixtures (fictional canon from 09 Handoff working-notes) — gallery only, never used by screens. */
const TODAY = "2026-09-23T10:12:00+03:00";

const galleryUser: ShellUser = {
  name: "سارة القحطاني",
  initials: "س ق",
  orgName: "مصرف الأفق",
  orgInitials: "أف",
  roleName: "مسؤول عمليات",
  permissions: [
    "platform.ops",
    "platform.institutions",
    "platform.users",
    "platform.temp_access",
    "platform.defaults",
    "platform.complaints",
    "platform.audit",
    "platform.privacy",
    "platform.billing",
    "platform.integrations",
  ],
  memberships: [],
  unread: 4,
  numerals: "latn",
};

const caseRows: CaseRowData[] = [
  {
    id: "1",
    ref: "RH-2026-004172",
    owner: "عبدالله م. · الرياض",
    status: "proposed_solution",
    nextAction: "إرسال الحل v2 للموافقة",
    sla: { tone: "warn", text: "يومان" },
    outstanding: 1284560,
    assignee: "سارة ق.",
    href: "/cases/RH-2026-004172",
  },
  {
    id: "2",
    ref: "RH-2026-003988",
    owner: "منيرة ع. · جدة",
    status: "awaiting_customer",
    nextAction: "متابعة رد المالكة على العرض",
    sla: { tone: "err", text: "متأخر 3 أيام" },
    outstanding: 742118.4,
    assignee: "خالد ز.",
    href: "/cases/RH-2026-003988",
  },
  {
    id: "3",
    ref: "RH-2026-003870",
    owner: "ماجد ت. · الدمام",
    status: "active_settlement",
    nextAction: "مطابقة الدفعة الثالثة",
    sla: { tone: "ok", text: "ضمن المهلة · 6 أيام" },
    outstanding: 612900,
    assignee: "ريم د.",
    href: "/cases/RH-2026-003870",
  },
];

function Section({ id, code, title, children }: { id: string; code: string; title: string; children: ReactNode }) {
  return (
    <section id={id} aria-labelledby={`${id}-h`} className="flex flex-col gap-4 border-t border-line pt-8">
      <div className="flex flex-col gap-1">
        <span dir="ltr" className="font-mono text-12 text-soft">
          {code}
        </span>
        <h2 id={`${id}-h`} className="m-0 text-22 leading-8 font-semibold">
          {title}
        </h2>
      </div>
      {children}
    </section>
  );
}

function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`rounded-lg border border-line bg-white p-5 ${className}`}>{children}</div>;
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <span className="text-12 font-semibold text-muted">{label}</span>
      <div className="flex flex-wrap items-center gap-3">{children}</div>
    </div>
  );
}

export function Gallery() {
  const { toast } = useToast();
  const [otp, setOtp] = useState("481");
  const [otpErr, setOtpErr] = useState("481903");
  const [amount, setAmount] = useState<number | null>(1284560);
  const [rate, setRate] = useState<number | null>(22.5);
  const [date, setDate] = useState("2026-11-01");
  const [solution, setSolution] = useState<string | null>("reschedule");
  const [tab, setTab] = useState("overview");
  const [selected, setSelected] = useState<Set<string>>(new Set(["2"]));
  const [dialog, setDialog] = useState(false);
  const [drawer, setDrawer] = useState<"end" | "bottom" | null>(null);
  const [reason, setReason] = useState("");
  const [attested, setAttested] = useState(false);
  const [page, setPage] = useState(1);

  return (
    <main id="main" className="mx-auto flex max-w-[1320px] flex-col gap-10 px-4 py-10 md:px-6">
      <header className="flex flex-wrap items-end gap-4">
        <div className="flex flex-1 flex-col gap-2">
          <span dir="ltr" className="font-mono text-13 text-soft">
            dev/ui · every component in every state · not available in production
          </span>
          <h1 className="m-0 text-40 leading-[56px] font-bold">معرض المكونات</h1>
          <p className="m-0 max-w-[72ch] text-16 text-charcoal">
            مرجع بصري للتحقق الذاتي مقابل 02 Components. بدّل اللغة لرؤية الانعكاس LTR: الأسهم والتقدم تنعكس، والساعة والتحقق والعملة لا تنعكس.
          </p>
        </div>
        <LocaleSwitch />
      </header>

      <Section id="brand" code="Brand · Logo" title="الشعار">
        <Card className="flex flex-wrap items-end gap-8">
          <Logo variant="horizontal" width={172} />
          <Logo variant="stacked" width={120} />
          <Logo variant="symbol" width={28} />
          <Logo variant="symbol" width={48} />
          <Logo variant="app-icon" width={72} />
        </Card>
        <div className="surface-dark flex flex-wrap items-end gap-8 rounded-lg bg-inv p-5">
          <Logo variant="horizontal-dark" width={172} />
          <Logo variant="stacked-dark" width={120} />
          <Logo variant="app-icon" width={48} />
        </div>
      </Section>

      <Section id="c01" code="C01 · Button / IconButton" title="الأزرار">
        <Card className="flex flex-col gap-5">
          {(["primary", "secondary", "text", "sensitive", "strong"] as const).map((v) => (
            <Row key={v} label={v}>
              <Button variant={v} icon={v === "sensitive" ? "cancel" : undefined} review={v === "sensitive"}>
                {v === "sensitive" ? "إلغاء الحالة" : v === "text" ? "عرض السجل" : v === "secondary" ? "حفظ كمسودة" : "إرسال للموافقة"}
              </Button>
              {v !== "text" && v !== "sensitive" ? (
                <Button variant={v} loading loadingLabel={v === "secondary" ? "جارٍ الحفظ" : "جارٍ الإرسال"}>
                  {v === "secondary" ? "حفظ كمسودة" : "إرسال للموافقة"}
                </Button>
              ) : null}
              <Button variant={v} disabled>
                معطّل
              </Button>
              <Button variant={v} softDisabled aria-describedby="soft-why">
                معطّل مع سبب
              </Button>
            </Row>
          ))}
          <span id="soft-why" className="text-12 text-muted">
            aria-disabled يبقي الزر قابلاً للتركيز حتى يُقرأ السبب.
          </span>
          <Row label="sizes md 40 · lg 48 · xl 54">
            <Button size="md">مؤسسي 40</Button>
            <Button size="lg">المالك / الجوال 48</Button>
            <Button size="xl">أساسي المالك 54</Button>
            <Button size="md" review>
              مراجعة وإرسال
            </Button>
            <Button size="md" variant="secondary" iconEnd="arrow_back" iconEndMirror>
              التالي
            </Button>
            <Button href="/dev/ui#c02" variant="secondary">
              رابط كزر
            </Button>
          </Row>
          <Row label="IconButton">
            <IconButton label="بحث" icon="search" />
            <IconButton label="الإشعارات، 4 غير مقروءة" icon="notifications" variant="outline" badge={4} />
            <IconButton label="الإشعارات، 1 جديدة" icon="notifications" size={44} dot />
            <IconButton label="رجوع" icon="arrow_forward" mirror size={44} />
            <IconButton label="معطّل" icon="visibility" disabled />
          </Row>
          <p className="m-0 text-13 text-muted">«…» تعني أن الزر يفتح شاشة مراجعة وليس تنفيذاً فورياً. لا زر برتقالي بنص أبيض.</p>
        </Card>
      </Section>

      <Section id="c02" code="C02 · StatusChip / SubStatusTag / SlaBadge / IntegrationStateTag" title="الحالات والشارات">
        <Card className="flex flex-col gap-4">
          <Row label="الحالة العامة — 16 حالة">
            {CASE_STATUS_KEYS.map((k) => (
              <StatusChip key={k} status={k} showEnglish />
            ))}
          </Row>
          <Row label="sm (جداول)">
            {CASE_STATUS_KEYS.slice(0, 6).map((k) => (
              <StatusChip key={k} status={k} size="sm" />
            ))}
          </Row>
          <Row label="الحالات الفرعية">
            <SubStatusTag kind="task" label="مفتوحة" icon="radio_button_unchecked" tone="neutral" />
            <SubStatusTag kind="approval" label="بانتظار المعتمد" icon="hourglass_top" tone="warn" />
            <SubStatusTag kind="approval" label="مرفوضة — أعيدت" icon="undo" tone="err" />
            <SubStatusTag kind="document" label="مطلوب" icon="upload_file" tone="info" />
            <SubStatusTag kind="document" label="منتهي الصلاحية" icon="event_busy" tone="err" />
            <SubStatusTag kind="payment" label="مسجلة — بانتظار المطابقة" icon="pending" tone="warn" />
            <SubStatusTag kind="payment" label="مطابقة" icon="check_circle" tone="ok" />
            <SubStatusTag kind="external" label="الحالة الرسمية: غير متاحة" icon="cloud_off" tone="neutral" />
          </Row>
          <Row label="SLA">
            <SlaBadge tone="ok" plain>
              {slaText({ dueAt: "2026-09-29", now: TODAY }).text}
            </SlaBadge>
            <SlaBadge tone="warn">{slaText({ dueAt: "2026-09-25", now: TODAY }).text}</SlaBadge>
            <SlaBadge tone="err">{slaText({ dueAt: "2026-09-20", now: TODAY }).text}</SlaBadge>
            <SlaBadge tone="paused">{slaText({ paused: true, pausedReason: "بانتظار طرف خارجي" }).text}</SlaBadge>
            <SlaBadge tone="info">بانتظار المقيّم</SlaBadge>
          </Row>
          <Row label="IntegrationState">
            {(["enabled", "simulated", "pending", "unavailable", "failed"] as const).map((s) => (
              <IntegrationStateTag key={s} state={s} showHint />
            ))}
          </Row>
          <Row label="Tag">
            <Tag tone="info">بانتظار ردك</Tag>
            <Tag tone="ok" icon="check_circle">
              مغلق · حُل
            </Tag>
            <Tag tone="warn" icon="schedule">
              تنتهي 2026-09-30
            </Tag>
          </Row>
        </Card>
      </Section>

      <Section id="c03" code="C03 · StageProgress" title="شريط المراحل">
        <Card>
          <StageProgress variant="full" current={3} meta={["2026-08-14", "2026-08-29", "2026-09-10", "الحالية · منذ 13 يوماً", "—", "—", "—"]} />
        </Card>
        <div className="grid gap-4 md:grid-cols-2">
          <Card>
            <StageProgress variant="header" current={3} />
          </Card>
          <Card className="max-w-[358px]">
            <StageProgress variant="compact" current={2} />
          </Card>
        </div>
      </Section>

      <Section id="c04" code="C04 · NextActionCard" title="بطاقة الإجراء التالي">
        <div className="grid gap-4 md:grid-cols-3">
          <NextActionCard
            state="available"
            title="إرسال الحل v2 للموافقة الداخلية"
            sla={{ tone: "warn", text: "متبقٍ يومان" }}
            checklist={[{ label: "تقييم معتمد", meta: <DateText value="2026-09-10" /> }, { label: "تحليل القدرة على السداد" }, { label: "مبرر التنازل عن الغرامات" }]}
            after="يراجعه المعتمد نورة الشهري. لا يصل للمالك قبل الاعتماد."
            action={{ label: "مراجعة وإرسال", review: true, onClick: () => toast({ message: "فتح شاشة المراجعة" }) }}
          />
          <NextActionCard
            state="blocked"
            title="إرسال الحل v2 للموافقة الداخلية"
            blocked={{ title: "ينقص مستند واحد", reasons: ["تقرير التقييم منتهي الصلاحية (أكثر من 90 يوماً)"], fix: { label: "طلب تقييم محدّث", href: "#c07" } }}
            action={{ label: "مراجعة وإرسال", review: true }}
          />
          <NextActionCard
            state="not-yours"
            title="اعتماد الحل v2"
            sla={{ tone: "info", text: "بانتظار المعتمد" }}
            owner={{ name: "نورة الشهري", initials: "ن ش", detail: "معتمدة · حد حتى 2,000,000 ر.س" }}
            note={
              <>
                أُرسل <DateText value={TODAY} mode="datetime" /> · المهلة <DateText value="2026-09-25" />. الاعتماد مخفي عنك لأنك مُعِدّة الطلب (فصل المهام).
              </>
            }
            secondaryAction={{ label: "إرسال تذكير", onClick: () => toast({ message: "أُرسل التذكير", tone: "ok" }) }}
          />
        </div>
      </Section>

      <Section id="c05" code="C05 · Alert / Toast" title="التنبيهات والإشعارات العابرة">
        <div className="flex flex-col gap-2.5">
          <Alert tone="info" title="بانتظار تقرير المقيّم المكلّف" action={<a href="#">فتح التكليف</a>}>
            مكتب تقييم معتمد «ب» · التسليم المتوقع <DateText value="2026-09-26" />
          </Alert>
          <Alert tone="ok" title="تم التحقق من صك الملكية" action={<a href="#">عرض الدليل</a>}>
            طابق الصك بيانات العقار في <DateText value="2026-08-29" />.
          </Alert>
          <Alert tone="warn" title="تقترب مهلة الموافقة الداخلية" action={<a href="#">إرسال تذكير</a>}>
            متبقٍ يومان. التأخير يؤخر العرض على المالك.
          </Alert>
          <Alert tone="err" title="تعذّر حفظ جدول السداد" action={<a href="#">مراجعة الجدول</a>}>
            مجموع الأقساط لا يساوي المبلغ المعاد جدولته (فرق 12.40 ر.س).
          </Alert>
          <Alert tone="neutral" compact role="none">
            ملاحظة ثابتة داخل البطاقة (بلا إعلان).
          </Alert>
          <SandboxCodeBox title="بيئة تجريبية — الرسائل النصية محاكاة ولا تُرسل: الرمز" code="481903" note="يظهر هذا الصندوق في بيئة التطوير فقط." />
          <div className="flex flex-wrap gap-3">
            <Button variant="secondary" onClick={() => toast({ message: "حُفظت المسودة · 10:42", tone: "ok", action: { label: "تراجع", onClick: () => toast({ message: "تم التراجع", tone: "info" }) } })}>
              إشعار عابر مع تراجع
            </Button>
            <Button variant="secondary" onClick={() => toast({ message: "انقطع الاتصال. سنحفظ عند العودة.", tone: "offline" })}>
              إشعار دون اتصال
            </Button>
          </div>
        </div>
      </Section>

      <Section id="c06" code="C06 · Field family" title="الحقول">
        <ErrorSummary
          errors={[
            { fieldId: "g-id", message: "رقم الهوية غير مكتمل" },
            { fieldId: "g-rate", message: "نسبة التنازل تتجاوز حدّك (15%)" },
          ]}
          focusKey="gallery"
        />
        <Card className="grid gap-x-6 gap-y-5 md:grid-cols-2 xl:grid-cols-3">
          <TextField label="اسم المالك كما في الصك" defaultValue="عبدالله محمد" help="افتراضي" />
          <TextField label="رقم عقد التمويل" defaultValue="MF-88-331" ltr mono help="يُفحص التكرار عند الخروج من الحقل" />
          <TextField id="g-id" label="رقم الهوية الوطنية" defaultValue="1••••••" ltr mono error="10 أرقام مطلوبة، أُدخل 7" />
          <AmountField label="المبلغ القائم" value={amount} onValueChange={setAmount} help={<>المصدر: نظام التمويل · <DateText value="2026-09-22" /></>} />
          <PercentField id="g-rate" label="نسبة التنازل عن الغرامات" value={rate} onValueChange={setRate} error={rate !== null && rate > 15 ? "حدّك 15%. للأعلى: يتطلب معتمداً أعلى" : undefined} />
          <DateField label="تاريخ استحقاق أول قسط" value={date} onValueChange={setDate} />
          <TextField label="المنشأة المموّلة" defaultValue="مصرف الأفق" disabled help="معطّل: محدد من منشأتك الحالية" />
          <TextField label="الجوال" prefix="+966" ltr mono placeholder="5x xxx xxxx" optionalMark />
          <Select label="نوع المنشأة" requiredMark placeholder="اختر" defaultValue="" options={[{ value: "bank", label: "بنك" }, { value: "finance", label: "شركة تمويل" }]} />
          <MaskedValue label="رقم الهوية (مخفي)" masked="1•••••••42" onReveal={() => new Promise((r) => setTimeout(() => r("1098765442"), 600))} help="الكشف يُسجَّل في السجل" />
          <Textarea label="سبب القرار" requiredMark maxLength={500} defaultValue="دخل المالك الحالي يسمح بقسط حتى 16,000 ر.س وفق التحليل." containerClassName="md:col-span-2" />
          <div className="flex flex-col gap-3">
            <Checkbox label="أوافق على استخدام بياناتي للتواصل بشأن هذا الطلب." defaultChecked />
            <Checkbox label="تذكّر اختياري على هذا الجهاز" />
            <Checkbox label="إقرار إلزامي" error="يجب تأكيد الإقرار" />
            <Checkbox label="معطّل" disabled />
          </div>
        </Card>
        <Card>
          <RadioCardGroup
            legend="نوع الحل"
            name="solution"
            value={solution}
            onChange={setSolution}
            options={[
              { value: "reschedule", label: "إعادة جدولة", description: "تمديد المدة وتعديل القسط" },
              { value: "discount", label: "سداد مخفض", description: "دفعة واحدة بخصم معتمد" },
              { value: "grace", label: "فترة سماح", description: "تأجيل مؤقت ثم استئناف" },
              { value: "sale", label: "بيع طوعي", disabled: true, disabledReason: "يتطلب موافقة المالك · مسار المرحلة 2" },
            ]}
          />
        </Card>
        <Card className="grid gap-6 md:grid-cols-2">
          <OtpInput label="رمز التحقق (افتراضي)" value={otp} onChange={setOtp} />
          <OtpInput label="رمز التحقق (خطأ)" value={otpErr} onChange={setOtpErr} error boxHeight={52} />
        </Card>
      </Section>

      <Section id="c07" code="C07 · DocumentItem / UploadDropzone" title="المستندات">
        <DocumentList>
          <DocumentItem icon="description" name="صك الملكية" version="v1" meta="رفعه المصرف · 2026-08-15 · يراه: فريق الحالة، القانونية" status="verified" action={{ label: "عرض" }} />
          <DocumentItem icon="request_quote" name="كشف الراتب لآخر 3 أشهر" version="v2" meta="مطلوب من المالك · المهلة 2026-09-28 · v1 رُفض: غير مقروء" status="returned" action={{ label: "عرض v1" }} />
          <DocumentItem icon="analytics" name="تقرير التقييم" version="v1" meta="مكتب تقييم معتمد «ب» · 2026-09-10 · صالح حتى 2026-12-09" status="valid" statusLabel="صالح · 77 يوماً" action={{ label: "معاينة" }} />
          <DocumentItem icon="badge" name="صورة الهوية الوطنية" version="v1" meta="رفعها المالك · 2026-08-20 · مخفية عن مقدمي الخدمة" status="expiring" statusLabel="تنتهي خلال 12 يوماً" action={{ label: "طلب تحديث" }} />
          <DocumentItem icon="upload_file" name="كشف حساب بنكي" meta="مطلوب من المالك" status="required" />
          <DocumentItem icon="pending" name="إيصال السداد" meta="رُفع اليوم" status="under_review" />
          <DocumentItem icon="event_busy" name="شهادة الراتب" meta="انتهت 2026-09-01" status="expired" />
          <DocumentItem icon="receipt_long" name="إشعار السداد — الدفعة الأولى" meta="مطلوب من المالك بعد الاتفاق" status="not_requested" />
        </DocumentList>
      </Section>

      <Section id="c08" code="C08 · ApprovalChain + VersionDiff" title="سلسلة الموافقة والفروق">
        <div className="grid gap-4 lg:grid-cols-2">
          <ApprovalChain
            steps={[
              { role: "المُعِدّ", who: "فهد العتيبي · محلل ائتمان", status: "prepared", state: "أعدّ الإصدار v2", meta: "2026-09-22 16:05 · «تعديل المدة بناءً على كشف الراتب v2»" },
              { role: "المراجِع", who: "سارة القحطاني · مديرة الحالة", status: "reviewed", state: "راجعت وأرسلت", meta: "2026-09-23 10:12 · مرفق: التحليل، التقييم" },
              { role: "المعتمِد (حتى 2,000,000)", who: "نورة الشهري", status: "pending", state: "بانتظار القرار · متبقٍ يومان", meta: "المهلة 2026-09-25 · لا يمكن للمُعِدّ أو المراجِع الاعتماد" },
              { role: "إشعار الامتثال", who: "آلي", status: "notice", state: "يُنشأ بعد الاعتماد", meta: "لأن التنازل > 1% من القائم (قاعدة قابلة للتهيئة — افتراض)" },
            ]}
          />
          <VersionDiff
            fromLabel="v1"
            toLabel="v2"
            rows={[
              { label: "المدة", before: "60 شهراً", after: "84 شهراً" },
              { label: "القسط الشهري", before: <Money value={21409.33} unit={false} />, after: <Money value={15074.52} /> },
              { label: "التنازل عن الغرامات", before: "0", after: <Money value={18300} /> },
              { label: "أصل الدين", unchanged: <Money value={1284560} unit={false} /> },
            ]}
          />
        </div>
      </Section>

      <Section id="c09" code="C09 · CaseTable (grid ≥768) / CaseCard (<768)" title="صف الحالة وبطاقة الجوال">
        <CaseTable label="حالاتي" rows={caseRows} selectable selected={selected} onSelectedChange={setSelected} />
        <Pagination page={page} pageSize={10} total={38} onPageChange={setPage} note="مرتبة حسب أقرب مهلة" />
      </Section>

      <Section id="c10" code="C10 · AuditTimeline" title="سجل التدقيق">
        <AuditTimeline
          events={[
            { id: "a1", at: "2026-09-23T10:12:00+03:00", kind: "transition", title: "حل مقترح ← موافقة داخلية", actor: "سارة القحطاني · مديرة حالات", detail: "مرفق: الحل v2، التحليل، التقييم · السبب: «مكتمل وفق القائمة»" },
            { id: "a2", at: "2026-09-22T16:05:00+03:00", kind: "create", title: "إنشاء الحل v2", actor: "فهد العتيبي · محلل ائتمان", detail: "تغييرات: المدة، القسط، التنازل (3)" },
            { id: "a3", at: "2026-09-18T09:30:00+03:00", kind: "blocked", title: "محاولة انتقال محجوبة: تقييم ← حل مقترح", actor: "فهد العتيبي", detail: "المانع: كشف الراتب v1 مرفوض. لم يُنفذ الانتقال." },
            { id: "a4", at: "2026-09-10T13:44:00+03:00", kind: "upload", title: "استلام تقرير التقييم v1", actor: "مكتب تقييم معتمد «ب» · مقدم خدمة", detail: "القيمة السوقية 1,650,000.00 ر.س · أُغلق وصول المقدم بعد 7 أيام" },
            { id: "a5", at: "2026-09-02T11:20:00+03:00", kind: "reveal", title: "كشف رقم الهوية كاملاً", actor: "سارة القحطاني", detail: "السبب: «مطابقة مع الصك» · مدة الكشف 60 ثانية" },
          ]}
        />
      </Section>

      <Section id="c11" code="C11 · SystemState / EmptyState / Skeleton" title="حالات النظام">
        <div className="grid gap-4 [grid-template-columns:repeat(auto-fill,minmax(min(100%,300px),1fr))]">
          <SystemState kind="loading" showKind />
          <SystemState kind="empty" showKind action={{}} />
          <SystemState kind="error" showKind errorRef="ERR-7F2A" action={{}} />
          <SystemState kind="offline" showKind action={{}} />
          <SystemState kind="forbidden" showKind action={{}} />
          <SystemState kind="success" showKind title="أُرسل الحل للموافقة" body="المعتمدة نورة الشهري · المهلة 2026-09-25. سنشعرك بالقرار." action={{ label: "العودة للحالة" }} />
        </div>
        <EmptyState icon="construction" title="هذه الشاشة قيد التنفيذ" body="الإطار والتنقل جاهزان. سيُضاف محتوى هذه الشاشة في مرحلة لاحقة." />
        <Card className="flex max-w-md flex-col gap-2">
          <Skeleton width="60%" />
          <Skeleton width="85%" height={28} />
          <Skeleton width="40%" />
        </Card>
      </Section>

      <Section id="c12" code="C12 · Menu (org switcher) / Tabs / FilterChip" title="مبدّل المنشأة، التبويبات، المرشحات">
        <div className="flex flex-wrap items-start gap-6">
          <Menu
            label="المنشأة والدور"
            header="منشآتك وأدوارك"
            footer="البيانات معزولة لكل منشأة؛ التبديل يعيد فتح الجلسة."
            minWidth={300}
            triggerClassName="flex min-h-[52px] w-[240px] items-center gap-2.5 rounded-md border border-line bg-warm px-2.5 text-start"
            trigger={
              <>
                <Avatar initials="أف" shape="tile" tone="ink" />
                <span className="flex flex-1 flex-col">
                  <strong className="text-14">مصرف الأفق</strong>
                  <span className="text-12 text-muted">مديرة حالات</span>
                </span>
              </>
            }
            items={[
              { key: "a", label: "مصرف الأفق", description: "مديرة حالات", checked: true, leading: <Avatar initials="أف" shape="tile" tone="ink" /> },
              { key: "b", label: "شركة السنبلة للتمويل", description: "محللة ائتمان · قراءة فقط", checked: false, leading: <Avatar initials="سن" shape="tile" />, onSelect: () => toast({ message: "تبديل المنشأة يعيد تحميل مساحة العمل" }) },
            ]}
          />
          <div className="flex min-w-0 flex-1 flex-col gap-4">
            <Tabs
              label="أقسام الحالة"
              active={tab}
              onChange={setTab}
              tabs={[
                { key: "overview", label: "نظرة عامة" },
                { key: "documents", label: "المستندات", count: 1, countTone: "err" },
                { key: "solutions", label: "الحلول" },
                { key: "audit", label: "السجل" },
                { key: "locked", label: "معطّل", disabled: true },
              ]}
            />
            <div className="flex flex-wrap gap-2">
              <FilterChip variant="active" label="الحالة: تحقق، تقييم" onRemove={() => toast({ message: "أُزيل المرشح" })} />
              <FilterChip label="المهلة" />
              <FilterChip label="المسؤول" />
              <FilterChip variant="action" icon="bookmark_add" label="حفظ العرض" />
            </div>
          </div>
        </div>
      </Section>

      <Section id="data" code="KpiTile · BarList (C14) · KeyValueList · Values" title="المؤشرات والرسوم والقيم">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <KpiTile icon="folder_open" label="الحالات النشطة" value="1,248" sub="+36 هذا الشهر · 147 أُغلقت خلال 90 يوماً" />
          <KpiTile icon="task_alt" iconTone="rust" label="بحاجة لإجرائك" value="7" unit="مهام" sub="3 منها خلال يومين" />
          <KpiTile icon="alarm_off" iconTone="err" label="تجاوزت المهلة" value="42" unit="حالة" delta={{ text: "انخفاض 5 عن الأسبوع الماضي", good: true }} />
          <KpiTile icon="account_balance_wallet" label="المديونية القائمة" value="1.62" unit="مليار ر.س" sub="المصدر: نظام التمويل · 06:00" />
        </div>
        <div className="grid gap-4 lg:grid-cols-2">
          <BarList
            title="توزيع الحالات النشطة حسب الحالة"
            unit="حالة"
            columns={{ label: "الحالة", value: "عدد الحالات" }}
            footnote="المصدر: سجل الانتقالات · 2026-09-23 06:00 · القيم غير مقربة"
            items={[
              { key: "v", label: "تحقق", value: 312 },
              { key: "val", label: "تقييم", value: 204 },
              { key: "p", label: "حل مقترح", value: 158, highlight: true },
              { key: "a", label: "موافقة داخلية", value: 96 },
              { key: "c", label: "بانتظار العميل", value: 121 },
            ]}
          />
          <Card className="flex flex-col gap-4">
            <KeyValueList
              dense
              rows={[
                { key: "النوع", value: "إعادة جدولة" },
                { key: "المبلغ المعاد جدولته", value: <Money value={1266260} /> },
                { key: "المدة", value: "84 شهراً" },
                { key: "القسط الشهري", value: <Money value={15074.52} />, strong: true },
                { key: "نسبة الاستقطاع", value: <bdi dir="ltr">{formatPercent(46.5)}</bdi> },
              ]}
            />
            <div className="flex flex-col gap-1 text-14">
              <span>
                Ref: <Ref>RH-2026-004172</Ref> · Money: <Money value={1284560} /> · compact: <Money value={1620000000} compact />
              </span>
              <span>
                Date: <DateText value={TODAY} /> · DateTime: <DateText value={TODAY} mode="datetime" /> · Hijri: <DateText value={TODAY} mode="hijri" />
              </span>
              <span dir="ltr" className="font-mono text-12 text-muted">
                formatMoney → {formatMoney(1284560, { withUnit: true })} · {formatDate(TODAY)} · {formatDateTime(TODAY)} · {formatHijri(TODAY)} · {formatPercent(46.5)}
              </span>
            </div>
            <div className="flex items-center gap-2">
              <Avatar initials="س ق" />
              <Avatar initials="ن ش" size={36} />
              <Avatar initials="أف" size={44} shape="tile" tone="ink" />
              <Avatar initials="رهـ" size={44} shape="tile" tone="rust" />
            </div>
          </Card>
        </div>
      </Section>

      <Section id="ds" code="DecisionSupportTag" title="وسم دعم القرار">
        <div className="grid gap-4 lg:grid-cols-2">
          <DecisionSupportTag
            label="احتمال الالتزام بالقسط خلال 12 شهراً"
            range="62–74%"
            confidence="medium"
            factors={[
              { label: "دخل متحقق من كشف الراتب v2", direction: "up" },
              { label: "7 أقساط متأخرة منذ 2026-02", direction: "down" },
              { label: "تواصل منتظم مع مسؤول الحالة", direction: "neutral" },
            ]}
            source="كشف الراتب v2 · سجل الأقساط 24 شهراً"
            modelVersion="ADH-v1.2"
            dataAsOf="2026-09-23 06:00"
            limitations="لا يشمل الالتزامات خارج المصرف؛ لا يُستخدم وحده في قرار ائتماني."
            opinion={{ value: "override", reason: "الدخل الإضافي موثّق بعقد جديد", by: "فهد العتيبي", at: "2026-09-22" }}
          />
          <DecisionSupportTag label="تقدير مدة التقييم" range="5–8 أيام" confidence="low" source="سجل التكليفات" modelVersion="SLA-est-0.3" limitations="عينة صغيرة لهذه المدينة." />
        </div>
      </Section>

      <Section id="review" code="ReviewScreen" title="شاشة المراجعة عالية الأثر">
        <div className="relative overflow-hidden rounded-lg border border-line bg-warm px-4 pt-6 md:px-10">
          <ReviewScreen
            title="مراجعة قبل إرسال الحل v2 للموافقة الداخلية"
            whatHappens={[
              { icon: "swap_horiz", content: <>تنتقل الحالة من <strong>حل مقترح</strong> إلى <strong>موافقة داخلية</strong>.</> },
              { icon: "edit_off", content: "يُقفل الإصدار v2 للتعديل. أي تغيير لاحق ينشئ v3 ويعيد السلسلة." },
              { icon: "person", content: <>يُسند إلى <strong>نورة الشهري</strong> · المهلة 3 أيام: <DateText value="2026-09-26" />.</> },
              { icon: "visibility_off", content: "لن يرى المالك أي عرض قبل الاعتماد. لا يُرسل له إشعار الآن." },
            ]}
            evidence={[
              { name: "الحل v2 (نسخة مقفلة)", meta: "16:05 · فهد العتيبي", href: "#", linkLabel: "معاينة" },
              { name: "تحليل القدرة على السداد", meta: "46.5% استقطاع", href: "#", linkLabel: "معاينة" },
            ]}
            reason={
              <div className="flex flex-col gap-3">
                <Textarea label="ملاحظتك للمعتمد" requiredMark value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} />
                <Checkbox label="أؤكد أنني راجعت شروط الحل والأدلة، وأنني لست مُعِدّة هذا الإصدار." checked={attested} onChange={(e) => setAttested(e.target.checked)} />
              </div>
            }
            aside={
              <Card>
                <KeyValueList dense rows={[{ key: "القسط الشهري", value: <Money value={15074.52} />, strong: true }]} />
              </Card>
            }
            back={{ label: "رجوع للحالة", onClick: () => toast({ message: "رجوع" }) }}
            recordCaption="سيُسجل: الفاعل، الوقت، الملاحظة، نسخة v2"
            primary={{ label: "إرسال للموافقة", onClick: () => toast({ message: "أُرسل الحل للموافقة", tone: "ok" }) }}
            complete={reason.trim().length > 0 && attested}
          />
        </div>
      </Section>

      <Section id="overlays" code="Dialog / Drawer" title="الحوارات واللوحات">
        <div className="flex flex-wrap gap-3">
          <Button variant="secondary" onClick={() => setDialog(true)}>
            فتح حوار
          </Button>
          <Button variant="secondary" onClick={() => setDrawer("end")}>
            لوحة جانبية
          </Button>
          <Button variant="secondary" onClick={() => setDrawer("bottom")}>
            ورقة سفلية
          </Button>
        </div>
        <Dialog
          open={dialog}
          onClose={() => setDialog(false)}
          title="تصدير 38 حالة"
          description="الإخفاء افتراضي. الملف بعلامة مائية ويُسجّل."
          footer={
            <>
              <Button variant="secondary" size="lg" onClick={() => setDialog(false)}>
                إلغاء
              </Button>
              <Button size="lg" className="flex-1" onClick={() => setDialog(false)}>
                تصدير
              </Button>
            </>
          }
        >
          <div className="p-5">
            <TextField label="سبب الكشف" requiredMark />
          </div>
        </Dialog>
        <Drawer open={drawer !== null} onClose={() => setDrawer(null)} title="المزيد" placement={drawer ?? "end"}>
          <p className="m-0 p-5 text-14">Esc يغلق اللوحة ويعيد التركيز إلى الزر الذي فتحها.</p>
        </Drawer>
      </Section>

      <Section id="shells" code="Shell pieces · CaseHeader / DebtorTop / DebtorNav / SettingsNav / PlatformSidebar" title="أجزاء الإطارات">
        <div className="overflow-hidden rounded-lg border border-line">
          <CaseHeader
            bleed={false}
            caseRef="RH-2026-004172"
            productLabel="تمويل سكني · مرابحة"
            lenderName="مصرف الأفق"
            title="عبدالله م. — فيلا سكنية، حي النرجس، الرياض"
            statusKey="proposed_solution"
            slaTone="warn"
            slaText="مهلة المرحلة: يومان · 2026-09-25"
            ownerName="سارة القحطاني"
            stage={3}
            activeTab="overview"
            tabs={[
              { key: "overview", label: "نظرة عامة", href: "/dev/ui#shells" },
              { key: "parties", label: "الأطراف", href: "/dev/ui#shells" },
              { key: "finance", label: "التمويل والمديونية", href: "/dev/ui#shells" },
              { key: "documents", label: "المستندات", href: "/dev/ui#shells", count: 1, countTone: "err" },
              { key: "solutions", label: "الحلول", href: "/dev/ui#shells" },
              { key: "audit", label: "السجل", href: "/dev/ui#shells" },
            ]}
            extraTabs={[{ key: "sale", label: "البيع الطوعي", href: "/dev/ui#shells" }]}
            actions={[
              { key: "pause", label: "إيقاف الحالة…", icon: "pause_circle" },
              { key: "cancel", label: "إلغاء الحالة…", icon: "cancel", tone: "danger" },
            ]}
          />
        </div>
        <div className="flex flex-wrap items-start gap-6">
          <div className="w-[390px] max-w-full overflow-hidden rounded-lg border border-line">
            <DebtorTop title="أهلاً عبدالله" sub="حالتك مع مصرف الأفق" unread={1} className="static" />
            <div className="h-24 bg-warm" />
            <DebtorNav active="home" />
          </div>
          <div className="w-[390px] max-w-full overflow-hidden rounded-lg border border-line">
            <DebtorTop title="التحقق من هويتك" sub="خطوة 1 من 2" back showBell={false} className="static" />
          </div>
          <Card>
            <SettingsNav orgName="مصرف الأفق" />
          </Card>
          <div className="h-[760px] overflow-hidden rounded-lg">
            <PlatformSidebar user={galleryUser} className="h-full" />
          </div>
        </div>
      </Section>
    </main>
  );
}
