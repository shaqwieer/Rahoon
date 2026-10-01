# رهون — برومبتات المراحل التالية

## طريقة الاستخدام

Phase 0 و1 انتهتا بحسب تأكيدك. هذه حزمة مواصفات وبرومبتات، وليست تقريرًا بأن المراحل التالية تم تنفيذها أو بأن كود مشروعك فُحص هنا.

ابدأ بملف **Rahoon_Roadmap_Planner.md** في جلسة يستطيع فيها المساعد قراءة مستودع رهون. مهمته إعداد خطط مفصلة داخل المشروع ومطابقتها بالكود الموجود، دون بدء تنفيذ المراحل الجديدة. أرفق الحزمة معه إن أردت أن يعتمد التفاصيل الجاهزة.

بعد توليد ملفات الخطة داخل المشروع، افتح جلسة جديدة لكل مرحلة بالترتيب أدناه. استخدم ملف المرحلة المستقل كبرومبت كامل، أو اطلب قراءة ملفها الذي ولّده المخطط داخل المستودع. لا يلزم نقل المحادثة السابقة: كل برومبت يتضمن سياق العمل وحدود المرحلة وتقرير التسليم المطلوب.

| الترتيب | المرحلة | النتيجة | الملف |
|---|---|---|---|
| 1 | 1.5 | إدارة الفريق والأدوار والصلاحيات ونطاق الوصول | Rahoon_Phase_1_5_Admin_Access.md |
| 2 | 2 | البحث والخريطة والمطابقة والمقارنة والحاسبات والتنبيهات | Rahoon_Phase_2_Discovery_Matching.md |
| 3 | 3A | العروض والتفاوض وقبول المالك والحجز المبدئي | Rahoon_Phase_3A_Offers_Reservations.md |
| 4 | 3B | الموافقات الخارجية والنقل وإثباتات المبالغ والإتمام | Rahoon_Phase_3B_Transfer_Completion.md |
| 5 | 4 | التشغيل والإعدادات ودليل الجهات والدعم والتقارير | Rahoon_Phase_4_Operations_Reports.md |
| 6 | 5 | تثبيت السلوك والصلاحيات والتزامن والتحقق من جاهزية الإطلاق | Rahoon_Phase_5_Launch_Readiness.md |

حافظنا على معنى Phase 2، وقسمنا Phase 3 السابقة إلى 3A و3B. أضفنا Phase 1.5 كي تُبنى صلاحيات الموظفين قبل تشغيل العروض والإتمام. Phase 4 تكمل إدارة التشغيل والتقارير فوق أساس الإدارة نفسه.

## بداية كل جلسة بعد إعداد الخطة داخل المستودع

انسخ السطر الموافق للمرحلة، مع مسار ملفها من الجدول التالي:

> Read repository instructions, `docs/rahoon/roadmap/README.md`, decisions and the prerequisite handoffs. Read the phase file below. Verify relevant prerequisites from the actual code, implement ONLY this phase, run its acceptance checks, write its completion handoff and update roadmap status. Stop before the next phase.

| المرحلة | ملف الخطة داخل المستودع |
|---|---|
| 1.5 | docs/rahoon/roadmap/phase-1.5-admin-access.md |
| 2 | docs/rahoon/roadmap/phase-2-discovery-matching.md |
| 3A | docs/rahoon/roadmap/phase-3a-offers-reservations.md |
| 3B | docs/rahoon/roadmap/phase-3b-transfer-completion.md |
| 4 | docs/rahoon/roadmap/phase-4-operations-reports.md |
| 5 | docs/rahoon/roadmap/phase-5-launch-readiness.md |

تقرير التسليم داخل المشروع هو حلقة الوصل بين الجلسات: ماذا نفذ، وما فُحص فعلًا، وما بقي، والمهاجرات والصلاحيات والقرارات وحالة Git والنشر. أي جزء ناقص يبقى ظاهرًا ولا يتحول إلى «مكتمل» بمجرد وجود واجهته.

الملفات الإنجليزية مكتوبة لتوجيه Claude أو Codex، مع الحفاظ على واجهات المنتج العربية. الحزمة لا تحتاج تغيير التقنيات المستخدمة أو إعادة تنفيذ Phase 0 و1.
