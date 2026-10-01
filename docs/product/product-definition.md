# رهون — Product definition (source of truth from 2026-10-01)

_Owner: product owner. Engineering records decisions here; it doesn't make them. This file **replaces**
[`product-direction.md`](product-direction.md) (the mortgage-default help model, archived at git tag `legacy-mortgage-final`)._

## 1. What Rahoon is

**رهون / Rahoon** is a platform for **Saudi Arabia**.

1. It helps an **owner of a property tied to obligations** with a **developer** or a **bank / financing institution** to submit a request to exit the contract or sell the property.
2. It helps a **buyer** find an opportunity that fits **the amount they can pay now** and **the obligations they can carry later**.
3. **The Rahoon team** reviews requests, documents and figures. It prepares opportunities and matches them with buyers. It then follows the transfer and completion with the parties and the relevant bodies.

The journey of the owner and the buyer, the request review and the opportunity preparation are inspired by عقار إيكزت. Rahoon uses its own identity and original content for Saudi Arabia.

The owner may be late on payments, or may want to exit before falling behind. Rahoon never requires a default to accept a request, and **never describes a customer publicly as «متعثر»**.

A bank financing on a property is **information inside a sale request**. It is **not** a lending activity of Rahoon.

### What Rahoon is not

Each of these was a description used earlier. Each is **wrong** and has been removed from the product. None is a section, role or path:

- a mortgage platform or a lender;
- a judicial agent, or an auction or judicial-sale operator;
- a consensual-sale («البيع الرضائي») service for defaulted mortgages;
- a debt-settlement or rescheduling service.

Rahoon also isn't any of the following in this version:

- a wallet or escrow;
- a lending gateway;
- a debt-collection function;
- an inbox for listings from outside brokers.

## 2. Parties (version 1)

| Party | What they do |
|---|---|
| **صاحب العقار** (owner) | Sends a request, completes the file, reviews the prepared opportunity summary, confirms it, follows its status |
| **المشتري** (buyer) | Browses, states their buying capacity, saves opportunities, registers interest (an offer comes in a later phase) |
| **فريق رهون** (Rahoon team) | Reviews, asks for completion, verifies, approves or rejects. Prepares opportunities, gets the owner's confirmation, publishes them and follows interest |

Rules for parties:

- One person can be a seller and a buyer **on the same account**.
- The same mobile number never creates two accounts.
- Version 1 deals with the original parties only.

## 3. Experience principles (CONFIRMED by the brief)

- **Language and locale:**
  - Arabic first, with correct RTL.
  - Amounts in Saudi riyals (ر.س).
  - Saudi mobile numbers by default (05…).
  - Saudi cities, districts and projects.
  - Displayed times in Asia/Riyadh.
  - Arabic-Indic and Latin digits are accepted the same way.
- **Accounts:**
  - A visitor can browse and use the first calculators without an account.
  - Signing in is required to save to an account, send a request or register interest.
  - Sign-in is by mobile + SMS code when the SMS integration is live. The UI never shows a verification that didn't happen (see §9).
- **Requests and saving:**
  - A **first request** is different from a **complete, publishable file**. Documents and detailed photos can come later.
  - Progress is saved automatically, with an honest save state. Before sign-in, the draft is «محفوظ على هذا الجهاز فقط».
  - Completing a request never creates a second request.
- **What the UI never says:**
  - It never promises a sale within a time, full recovery of amounts paid, bank approval or guaranteed savings.
  - It uses no invented counts, testimonials or success rates.

## 4. Owner journey

«قدّم طلبك ← استكمل ملفك ← نراجع ونجهز الفرصة ← نبحث عن مشترٍ مناسب ← نتابع النقل والإتمام»

1. **First request (3 short steps):**
   1. The property and the obligation party (developer / bank-financier / more than one).
   2. The key figures. Each one accepts «لا أعرف», and the questions follow the party.
   3. Contact and confirmation: name, mobile and code, a summary, a statement of ownership or relationship, and consent to processing and contact.
2. **The follow-up file** opens on the same request and shows what is missing. It has four groups: property and location, figures and obligations, photos and documents, review and send to the team.
3. **Field rules** come from one central catalog, served by the API and enforced by the server (see `server/src/Rahoon.Api/Modules/Market/FieldCatalog.cs`):
   - The fields depend on the property type and the obligation kind.
   - Fields of an inactive branch are dropped on save, never validated.
   - The catalog has two tiers: required to submit, and required to publish.
4. **Private documents and public listing photos are separate** in upload, storage and display.
   - A request can be sent without photos.
   - Publishing needs a cover photo and at least one suitable photo.

## 5. Buyer journey

«حدد قدرتك الشرائية ← اكتشف الفرص المناسبة ← أرسل اهتمامك ← تابع الموافقات والإجراءات ← أتم الشراء»

1. **Buying capacity:**
   - Amount available now, comfortable installment and its frequency, and an optional maximum purchase price.
   - Cash, or financing from an outside party.
2. **Preferences:** cities and districts or projects, several types, area, bedrooms, ready or under construction, preferred delivery.
3. **Contact and confirmation:** name, mobile and code, then a review.

There are three separate levels of capacity, and team approval of the profile is **not** bank approval:

- **declared** (stated by the buyer);
- **team-reviewed proof**;
- **financing approval** issued by a financing party.

## 6. Statuses

| Object | Statuses |
|---|---|
| Sale request | مسودة → مستلم → قيد المراجعة → يحتاج استكمال → معتمد لإعداد فرصة · مرفوض · مسحوب. After completion it returns to review, and earlier decisions stay in the log |
| Buyer request | مسودة · مستلم · قيد المراجعة · يحتاج استكمال · معتمد للمطابقة · مرفوض · مسحوب |
| Opportunity | قيد الإعداد · بانتظار تأكيد المالك · جاهزة للنشر · منشورة · موقوفة · محجوزة مبدئيًا\* · قيد الإتمام\* · مكتملة\* · مسحوبة (\* Phase 3) |
| Developer / financier approval | لم تُطلب · قيد الطلب · مشروطة · موافق عليها · مرفوضة · انتهت صلاحيتها. Kept separate from Rahoon's approval |
| Interest | مستلم · قيد المتابعة · مغلق · مسحوب. It never reserves the property or accepts an offer |

These rules apply across the statuses:

- Approving a sale request never publishes anything.
- The opportunity is prepared, the owner confirms the summary, and then the team publishes.
- If a published price, balance or material transfer condition changes, a **new terms version** is created. That version needs the owner's confirmation and republication.
- Interests keep the terms version they were made on.

## 7. Calculation rules (server is the single source: `MarketCalculator`)

### A. Developer obligation

| Symbol | Meaning |
|---|---|
| P | The approved amount paid towards the unit price |
| D | The remaining balance to the developer, including arrears when the statement says so |
| A | The overdue part of D that is due now |
| V | The optional reduction the owner accepts on P |

The scenario figures:

- Owner amount = P − V, before the owner's documented costs.
- If the buyer carries A now:
  - buyer now = owner amount + A + buyer costs due now;
  - future developer obligation = D − A;
  - buyer total = owner amount + D + all buyer costs. A is never added twice.

### B. Bank / financier

- Owner net = proposed sale price − the payoff amount due to the financier at completion − the owner's costs.
- The payoff amount comes from the financier's letter, with what it includes and how long it is valid.
- Rahoon never computes the payoff as the installment × the remaining months.
- Past installments are never a measure of what is owed to the seller.
- A negative result is shown as a gap that needs review. It is never shown as zero.

### C. Buyer cost and comparison

These figures are shown separately:

- due now;
- paid to the parties;
- received by the seller;
- future installments;
- extra payments;
- Rahoon's commission;
- other confirmed costs, each with who carries it.

A price difference is shown only against a dated comparison reference that has a source.

### General rules

- An unknown value is **never zero**. The result becomes «تقدير غير مكتمل» with the list of missing fields.
- A quarterly installment may be converted to a monthly equivalent **for comparison**. The real amount and its date are always shown, together with the extra payments.

### Verification examples (also C# tests)

These are verification examples, not real prices or fees:

1. **Developer:** contract 1,000,000, paid 300,000, balance 700,000 including arrears of 20,000, reduction 10,000, buyer costs 15,000 now.
   - Result: seller 290,000 · now 325,000 · future 680,000 · total 1,005,000.
2. **Bank:** sale 1,100,000, payoff 800,000, seller costs 10,000.
   - Result: seller net 290,000.
3. **Unknown balance or fees:** «تقدير غير مكتمل» with the missing fields.

## 8. Revenue policy (OPEN: configuration, not a hard-coded rate)

The policy as the reference model frames it, kept apart from the parties' fees and other costs:

- Submitting a request and the first review are free.
- The seller pays no Rahoon commission.
- The buyer pays a commission under the approved policy and contract.

The settings live in `Market:Commission`:

- rate, basis, timing, payer, VAT policy;
- `Approved` (default **false**).

While the policy isn't approved, every screen says the commission is «تُحدد وفق العقد المعتمد». It is never shown as 0. The 1.25% of the reference site **isn't** applied, and no Saudi rate has been chosen.

## 9. Integrations and decisions still needed (OPEN)

| # | Item | State |
|---|---|---|
| D1 | **SMS provider** for the mobile code | Only a sandbox gateway exists. Staging runs with `Auth:SmsConfirmation=false`, so whoever knows a mobile number can open that account. **Blocking for any real use** |
| D2 | **Commission policy** (rate, basis, timing, VAT, contract) | Not approved. See §8 |
| D3 | **Map tiles and geocoding provider** for production | Dev uses the public OpenStreetMap tiles and Nominatim within their usage policies. Production needs a provider with suitable terms |
| D4 | **Advertising and brokerage licensing** (REGA brokerage law, FAL licences) | To verify before public launch: the advertisement requirements for each published opportunity |
| D5 | **Developer / financier directory** | The seeded lists are fictional demo names. The real list needs approval |
| D6 | **Retention and archive** of the legacy mortgage-help data | Kept untouched in the database (schemas `cases`, `requests`, …). Its deletion or anonymisation needs a decision |
| D7 | **Object storage and malware scanning** for documents and photos in production | Local disk + placeholder scanner only |
| D8 | **Phase 2/3 scope** (offers, reservation, completion, fees) | Not started. Starts only when the product owner asks |
