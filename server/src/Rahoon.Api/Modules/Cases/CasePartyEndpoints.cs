using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Cases;

public sealed record AddPartyRequest(string? Role, string? Kind, string? FullName, string? NationalId, string? Phone, string? Email, string? Language,
    string? Relation, bool? IsContractParty, bool? ContactAllowed, string? EmploymentStatus, string? SpecialNeeds, string? Notes);
public sealed record UpdatePartyRequest(string? Role, string? FullName, string? NationalId, string? Phone, string? Email, string? Language, string? Relation,
    bool? IsContractParty, bool? ContactAllowed, string? EmploymentStatus, string? SpecialNeeds, string? Notes);
public sealed record ContactPreferencesRequest(List<string>? AllowedChannels, string? ContactHours, string? CommunicationNeeds);
public sealed record PoaRequestBody(DateOnly? DueOn, List<string>? Channels);

/// <summary>
/// L06 parties: all personal data masked (full values only through the audited reveal endpoint),
/// contact preferences from the owner access record, owner invitation and POA requests.
/// </summary>
public static class CasePartyEndpoints
{
    public static readonly Dictionary<string, string> ChannelLabels = new()
    {
        ["platform"] = "المنصة", ["sms"] = "رسائل نصية", ["email"] = "البريد الإلكتروني", ["call"] = "مكالمة هاتفية",
    };

    private static readonly PartyRole[] AddableRoles =
        [PartyRole.CoBorrower, PartyRole.Guarantor, PartyRole.Agent, PartyRole.LegalRepresentative, PartyRole.Occupant, PartyRole.InformalRepresentative];
    private static readonly PartyRole[] PoaRoles = [PartyRole.Agent, PartyRole.LegalRepresentative, PartyRole.InformalRepresentative];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequirePermission(P.CaseView);
        g.MapGet("/parties", List);
        g.MapPost("/parties", Add).RequirePermission(P.CaseEdit).Idempotent();
        g.MapPatch("/parties/{partyId:guid}", Update).RequirePermission(P.CaseEdit);
        g.MapPost("/parties/{partyId:guid}/poa-request", RequestPoa).RequirePermission(P.DocumentRequest).Idempotent();
        g.MapPost("/owner-invitation", Invite).RequirePermission(P.CaseEdit).Idempotent();
        g.MapGet("/contact-preferences", Preferences);
        g.MapPut("/contact-preferences", SavePreferences).RequirePermission(P.CaseEdit);
    }

    public static string RoleKey(PartyRole r) => r switch
    {
        PartyRole.OwnerBorrower => "owner_borrower",
        PartyRole.CoBorrower => "co_borrower",
        PartyRole.Guarantor => "guarantor",
        PartyRole.Agent => "agent",
        PartyRole.LegalRepresentative => "legal_representative",
        PartyRole.Occupant => "occupant",
        _ => "informal_representative",
    };

    private static PartyRole? ParseRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Replace("_", "");
        return Enum.TryParse<PartyRole>(v, true, out var r) ? r : null;
    }

    private static string InvitationKey(OwnerAccess? a, DateTimeOffset now) => a is null ? "not_sent" : a.RevokedAt is not null ? "revoked" : a.InvitationStatus switch
    {
        OwnerInvitationStatus.Sent when a.InvitationExpiresAt < now => "expired",
        OwnerInvitationStatus.Sent => "sent",
        OwnerInvitationStatus.Accepted => "accepted",
        OwnerInvitationStatus.Expired => "expired",
        OwnerInvitationStatus.Revoked => "revoked",
        _ => "not_sent",
    };

    // ───────── Read ─────────

    private static async Task<IResult> List(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var parties = await db.Parties.AsNoTracking().Where(p => p.CaseId == c.Id).OrderByDescending(p => p.IsPrimary).ThenBy(p => p.CreatedAt).ToListAsync();
        var poaRequests = await db.DocumentRequests.AsNoTracking()
            .Where(r => r.CaseId == c.Id && r.DocumentTypeKey == "poa" && r.PartyId != null && r.Status == DocumentRequestStatus.Open)
            .Select(r => new { r.PartyId, r.DueOn }).ToListAsync();
        var canComms = rc.Has(P.CommsSend);
        var items = parties.Select(p =>
        {
            var roleLabel = CaseEndpoints.PartyRoleLabel(p.Role);
            var poa = poaRequests.FirstOrDefault(r => r.PartyId == p.Id);
            var isRep = PoaRoles.Contains(p.Role);
            var facts = new List<object>();
            if (p.IsPrimary || p.NationalIdMasked is not null) facts.Add(new { k = p.Kind == PartyKind.Organization ? "السجل" : "الهوية", v = p.NationalIdMasked ?? "—", ltr = true });
            if (p.PhoneMasked is not null) facts.Add(new { k = "الجوال", v = p.PhoneMasked, ltr = true });
            if (p.IsPrimary)
            {
                facts.Add(new { k = "اللغة المفضلة", v = p.PreferredLanguage == "en" ? "الإنجليزية" : "العربية", ltr = false });
                if (p.EmploymentStatus is not null) facts.Add(new { k = "الحالة الوظيفية", v = p.EmploymentStatus, ltr = false });
            }
            else
            {
                if (p.Relation is not null) facts.Add(new { k = "العلاقة", v = p.Relation, ltr = false });
                facts.Add(new { k = "طرف في العقد", v = p.IsContractParty ? "نعم" : "لا", ltr = false });
                facts.Add(new { k = "التواصل", v = p.ContactAllowed ? "مسموح" : "غير مسموح", ltr = false });
                if (isRep) facts.Add(new { k = "تفويض موثق", v = PoaLabel(p.PoaStatus), ltr = false });
            }

            // One prominent note per party (icon + tone + text), most important first.
            (string Icon, string Tone, string Text)? note =
                isRep && p.PoaStatus != PoaStatus.Verified
                    ? ("pending", "warn", poa is not null ? $"طُلبت وكالة موثقة · المهلة {poa.DueOn:yyyy-MM-dd}" : p.PoaStatus == PoaStatus.Uploaded ? "الوكالة مرفوعة وبانتظار التحقق" : "طلب وكالة موثقة لمنح صلاحية الاطلاع")
                : !p.IsPrimary && !p.IsContractParty && !p.ContactAllowed
                    ? ("block", "muted", p.Notes ?? "لا تُشارك بيانات مالية مع هذا الطرف")
                : p.IdentityVerifiedAt is { } va
                    ? ("verified_user", "ok", $"متحقق من الهوية عبر {(p.IdentityVerifiedVia?.StartsWith("الدعوة") == true ? "الدعوة" : p.IdentityVerifiedVia ?? "الدعوة")} {va.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}")
                : p.Notes is not null ? ("info", "info", p.Notes) : null;

            return new
            {
                p.Id, name = p.DisplayName, initials = AuthEndpoints.Initials(p.FullName), avatar = p.IsPrimary ? "primary" : "subtle",
                role = RoleKey(p.Role), roleLabel = p.IsPrimary ? roleLabel : (p.Relation is { } rel && p.Role is PartyRole.Occupant ? rel : roleLabel),
                kind = p.Kind == PartyKind.Organization ? "organization" : "individual", p.IsPrimary, p.IsContractParty, p.Relation,
                nationalIdMasked = p.NationalIdMasked, phoneMasked = p.PhoneMasked, emailMasked = p.Email is null ? null : Mask.Email(p.Email),
                language = p.PreferredLanguage, p.EmploymentStatus, p.ContactAllowed, p.SpecialNeeds, p.Notes,
                identityVerifiedAt = p.IdentityVerifiedAt, identityVerifiedVia = p.IdentityVerifiedVia,
                poaStatus = p.PoaStatus.ToString().ToLowerInvariant(), poaDueOn = poa?.DueOn,
                // Non-contract representatives see financial data only with a verified POA (L06 rule).
                financialVisibility = p.IsPrimary || p.IsContractParty || (isRep && p.PoaStatus == PoaStatus.Verified),
                facts,
                note = note is null ? null : new { icon = note.Value.Icon, tone = note.Value.Tone, text = note.Value.Text },
                actions = new
                {
                    message = canComms && p.ContactAllowed && p.PhoneEnc is not null,
                    edit = rc.Has(P.CaseEdit) && !CaseStatusInfo.IsTerminal(c.Status),
                    requestPoa = rc.Has(P.DocumentRequest) && isRep && p.PoaStatus is PoaStatus.None && poa is null,
                    reveal = rc.Has(P.PiiReveal) && (p.NationalIdEnc is not null || p.PhoneEnc is not null),
                },
            };
        }).ToList();

        return Results.Ok(new
        {
            count = items.Count,
            items,
            contact = await PreferencesPayloadAsync(db, rc, c, clock),
            revealNote = "كل كشف للبيانات الكاملة يتطلب سبباً ويُسجَّل ويظهر للمدقق. مدة الكشف 60 ثانية.",
            canAdd = rc.Has(P.CaseEdit) && !CaseStatusInfo.IsTerminal(c.Status),
            addableRoles = AddableRoles.Select(r => new { key = RoleKey(r), label = CaseEndpoints.PartyRoleLabel(r) }),
        });
    }

    private static string PoaLabel(PoaStatus s) => s switch
    {
        PoaStatus.Requested => "مطلوب",
        PoaStatus.Uploaded => "مرفوع · قيد التحقق",
        PoaStatus.Verified => "متحقق",
        _ => "غير مرفوع",
    };

    private static async Task<IResult> Preferences(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        return Results.Ok(await PreferencesPayloadAsync(db, rc, c, clock));
    }

    private static async Task<object> PreferencesPayloadAsync(RahoonDbContext db, RequestContext rc, Case c, IClock clock)
    {
        var primary = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        var a = primary is null ? null : await db.OwnerAccesses.AsNoTracking().FirstOrDefaultAsync(o => o.CaseId == c.Id && o.PartyId == primary.Id);
        var lastContact = await db.Messages.Where(m => m.CaseId == c.Id && !m.InternalOnly).MaxAsync(m => (DateTimeOffset?)m.At);
        var now = clock.UtcNow;
        var key = InvitationKey(a, now);
        static string D(DateTimeOffset? t) => t is null ? "" : t.Value.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd");
        var channels = a?.AllowedChannels ?? ["platform", "sms"];
        var (canInvite, inviteReason) = !rc.Has(P.CaseEdit) ? (false, (string?)null)
            : c.Status == CaseStatus.Draft ? (false, "لا تُرسل دعوة قبل إنشاء الحالة (مسودة).")
            : CaseStatusInfo.IsTerminal(c.Status) ? (false, "الحالة مغلقة أو ملغاة.")
            : primary?.PhoneEnc is null ? (false, "لا يوجد جوال مسجل للمالك.")
            : !primary.ContactAllowed ? (false, "التواصل مع المالك غير مسموح في سجل الطرف.")
            : (true, null);
        return new
        {
            invitation = new
            {
                status = key,
                label = key switch
                {
                    "accepted" => $"مقبولة {D(a!.AcceptedAt)}",
                    "sent" => $"مرسلة {D(a!.InvitedAt)} · صالحة حتى {D(a.InvitationExpiresAt)}",
                    "expired" => $"منتهية {D(a!.InvitationExpiresAt)}",
                    "revoked" => "ملغاة",
                    _ => "لم تُرسل",
                },
                invitedAt = a?.InvitedAt, acceptedAt = a?.AcceptedAt, expiresAt = a?.InvitationExpiresAt,
                canInvite, disabledReason = canInvite ? null : inviteReason,
                actionLabel = key is "sent" or "expired" or "accepted" ? "تجديد الدعوة" : "إرسال الدعوة",
            },
            identityVerification = new
            {
                verified = a?.IdentityVerifiedAt is not null,
                label = a?.IdentityVerifiedAt is not null ? "مكتمل" : "غير مكتمل",
                at = a?.IdentityVerifiedAt, method = a?.IdentityMethod,
            },
            allowedChannels = channels,
            allowedChannelsLabel = string.Join("، ", channels.Select(ch => ChannelLabels.GetValueOrDefault(ch, ch))),
            contactHours = a?.ContactHours,
            communicationNeeds = a?.CommunicationNeeds,
            lastContactAt = lastContact,
            lastContactLabel = lastContact is null ? "لا يوجد" : D(lastContact),
            channelOptions = ChannelLabels.Select(kv => new { key = kv.Key, label = kv.Value }),
        };
    }

    // ───────── Add / edit ─────────

    private static void ValidateIdentity(Validator v, string? nationalId, bool organization)
    {
        if (string.IsNullOrWhiteSpace(nationalId)) return;
        var digits = new string(nationalId.Where(char.IsDigit).ToArray());
        if (organization) v.Require(digits.Length == 10 && digits[0] == '7', "nationalId", "رقم السجل التجاري/الموحد 10 أرقام ويبدأ بـ 7.");
        else v.Require(CaseFactory.IsValidNationalId(digits), "nationalId", digits.Length != 10 ? $"10 أرقام مطلوبة، أُدخل {digits.Length}" : "رقم الهوية يبدأ بـ 1 أو 2.");
    }

    private static void ValidateCommon(Validator v, string? phone, string? email, string? language, string? relation, string? employment, string? needs, string? notes)
    {
        v.Require(string.IsNullOrWhiteSpace(phone) || CaseFactory.IsValidSaudiMobile(phone), "phone", "يجب أن يبدأ بـ 05");
        v.Require(string.IsNullOrWhiteSpace(email) || (email.Contains('@') && email.Length <= 254), "email", "البريد الإلكتروني غير صالح.");
        v.Require(language is null or "ar" or "en", "language", "اختر لغة التواصل.");
        v.Require(relation is null || relation.Length <= 120, "relation", "العلاقة حتى 120 حرفاً.");
        v.Require(employment is null || employment.Length <= 120, "employmentStatus", "النص حتى 120 حرفاً.");
        v.Require(needs is null || needs.Length <= 300, "specialNeeds", "النص حتى 300 حرف.");
        v.Require(notes is null || notes.Length <= 500, "notes", "الملاحظة حتى 500 حرف.");
    }

    private static async Task<IResult> Add(string reference, AddPartyRequest req, CaseAccess access, RahoonDbContext db, CaseFactory factory, PiiProtector pii, AuditLog audit)
    {
        var role = ParseRole(req.Role);
        var org = string.Equals(req.Kind, "organization", StringComparison.OrdinalIgnoreCase);
        var v = new Validator();
        v.Require(role is { } r0 && AddableRoles.Contains(r0), "role", "اختر دور الطرف (المالك الأساسي لا يُضاف من هنا).");
        v.Require(!string.IsNullOrWhiteSpace(req.FullName) && req.FullName.Trim().Length is >= 3 and <= 150, "fullName", "الاسم كما في الهوية مطلوب.");
        ValidateIdentity(v, req.NationalId, org);
        v.Require(role is not (PartyRole.CoBorrower or PartyRole.Guarantor) || !string.IsNullOrWhiteSpace(req.NationalId), "nationalId", "رقم الهوية مطلوب للمقترض المشارك والكفيل.");
        v.Require(role is not (PartyRole.Occupant or PartyRole.InformalRepresentative) || !string.IsNullOrWhiteSpace(req.Relation), "relation", "اذكر علاقة الطرف بالمالك أو بالعقار.");
        ValidateCommon(v, req.Phone, req.Email, req.Language, req.Relation, req.EmploymentStatus, req.SpecialNeeds, req.Notes);
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c, "لا يمكن تعديل أطراف حالة مغلقة أو ملغاة.");
        var digits = new string((req.NationalId ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
        {
            var hash = pii.LookupHash(digits);
            if (await db.Parties.AnyAsync(p => p.CaseId == c.Id && p.NationalIdHash == hash))
                throw new ConflictException("party_exists", "هذا الطرف مضاف مسبقاً إلى الحالة.");
        }
        var party = factory.NewParty(c.OrganizationId, c.Id, role!.Value, req.FullName!, digits.Length == 10 ? digits : null, req.Phone,
            email: req.Email, language: req.Language ?? "ar", specialNeeds: string.IsNullOrWhiteSpace(req.SpecialNeeds) ? null : req.SpecialNeeds.Trim(),
            kind: org ? PartyKind.Organization : PartyKind.Individual);
        party.Relation = string.IsNullOrWhiteSpace(req.Relation) ? null : req.Relation.Trim();
        party.IsContractParty = req.IsContractParty ?? role is PartyRole.CoBorrower or PartyRole.Guarantor;
        party.ContactAllowed = req.ContactAllowed ?? role is not PartyRole.Occupant;
        party.EmploymentStatus = string.IsNullOrWhiteSpace(req.EmploymentStatus) ? null : req.EmploymentStatus.Trim();
        party.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        db.Parties.Add(party);
        await audit.RecordAsync(new AuditEntry("party.added", $"إضافة طرف: {CaseEndpoints.PartyRoleLabel(party.Role)} ({party.DisplayName})", c.Id, c.Reference,
            Detail: $"طرف في العقد: {(party.IsContractParty ? "نعم" : "لا")} · التواصل: {(party.ContactAllowed ? "مسموح" : "غير مسموح")}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { party.Id, name = party.DisplayName, role = RoleKey(party.Role) });
    }

    private static async Task<IResult> Update(string reference, Guid partyId, UpdatePartyRequest req, CaseAccess access, RahoonDbContext db,
        CaseFactory factory, AuditLog audit)
    {
        var v = new Validator();
        var role = req.Role is null ? null : ParseRole(req.Role);
        v.Require(req.Role is null || (role is { } r0 && AddableRoles.Contains(r0)), "role", "دور غير صالح.");
        v.Require(req.FullName is null || req.FullName.Trim().Length is >= 3 and <= 150, "fullName", "الاسم كما في الهوية مطلوب.");
        ValidateCommon(v, req.Phone, req.Email, req.Language, req.Relation, req.EmploymentStatus, req.SpecialNeeds, req.Notes);
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c, "لا يمكن تعديل أطراف حالة مغلقة أو ملغاة.");
        var p = await db.Parties.FirstOrDefaultAsync(x => x.Id == partyId && x.CaseId == c.Id) ?? throw new NotFoundException();
        var v2 = new Validator();
        // The primary owner's identity and phone drive owner sign-in (invitation + last 4 + OTP): they are corrected at the source, not here.
        v2.Require(!p.IsPrimary || (req.FullName is null && req.Phone is null && req.NationalId is null && req.Role is null), "primary",
            "اسم المالك الأساسي وهويته وجواله لا تُعدّل من هنا؛ اطلب تصحيح البيانات من المصدر.");
        v2.Require(req.NationalId is null || p.NationalIdEnc is null, "nationalId", "رقم الهوية مسجل مسبقاً ولا يُعدّل؛ احذف الطرف وأعد إضافته عند الخطأ.");
        if (req.NationalId is not null && p.NationalIdEnc is null) ValidateIdentity(v2, req.NationalId, p.Kind == PartyKind.Organization);
        v2.ThrowIfInvalid();

        var changed = new List<string>();
        if (role is { } r && r != p.Role) { p.Role = r; changed.Add("الدور"); }
        if (req.FullName is { } name && name.Trim() != p.FullName)
        {
            p.FullName = name.Trim();
            p.DisplayName = p.Kind == PartyKind.Individual ? Mask.PersonName(p.FullName) : p.FullName;
            changed.Add("الاسم");
        }
        if (req.NationalId is not null) { factory.SetNationalId(p, req.NationalId); changed.Add("رقم الهوية"); }
        if (req.Phone is not null) { factory.SetPhone(p, req.Phone); changed.Add("الجوال"); }
        if (req.Email is not null && req.Email.Trim() != (p.Email ?? "")) { p.Email = req.Email.Trim().Length == 0 ? null : req.Email.Trim(); changed.Add("البريد"); }
        if (req.Language is not null && req.Language != p.PreferredLanguage) { p.PreferredLanguage = req.Language; changed.Add("اللغة"); }
        if (req.Relation is not null && req.Relation.Trim() != (p.Relation ?? "")) { p.Relation = req.Relation.Trim(); changed.Add("العلاقة"); }
        if (req.IsContractParty is { } icp && icp != p.IsContractParty && !p.IsPrimary) { p.IsContractParty = icp; changed.Add("طرف في العقد"); }
        if (req.ContactAllowed is { } ca && ca != p.ContactAllowed) { p.ContactAllowed = ca; changed.Add("إذن التواصل"); }
        if (req.EmploymentStatus is not null && req.EmploymentStatus.Trim() != (p.EmploymentStatus ?? "")) { p.EmploymentStatus = req.EmploymentStatus.Trim(); changed.Add("الحالة الوظيفية"); }
        if (req.SpecialNeeds is not null && req.SpecialNeeds.Trim() != (p.SpecialNeeds ?? "")) { p.SpecialNeeds = req.SpecialNeeds.Trim(); changed.Add("احتياجات خاصة"); }
        if (req.Notes is not null && req.Notes.Trim() != (p.Notes ?? "")) { p.Notes = req.Notes.Trim(); changed.Add("الملاحظات"); }
        if (changed.Count == 0) return Results.Ok(new { changed });

        await audit.RecordAsync(new AuditEntry("party.updated", $"تعديل بيانات طرف: {p.DisplayName}", c.Id, c.Reference,
            Detail: "الحقول: " + string.Join("، ", changed), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { changed });
    }

    // ───────── Owner invitation & contact preferences ─────────

    private static async Task<IResult> Invite(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit,
        ISmsGateway sms, PiiProtector pii, IConfiguration config, IHostEnvironment env, AuthOptions auth)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        if (c.Status == CaseStatus.Draft) throw new ConflictException("case_draft", "لا تُرسل دعوة للمالك قبل إنشاء الحالة؛ الحالة ما زالت مسودة.");
        CaseStaffing.EnsureNotTerminal(c, "الحالة مغلقة أو ملغاة؛ لا تُرسل دعوات جديدة.");
        var party = await db.Parties.FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary) ?? throw new ConflictException("no_primary", "لا يوجد مالك أساسي للحالة.");
        if (party.PhoneEnc is null) throw new ConflictException("no_phone", "لا يوجد جوال مسجل للمالك لإرسال الدعوة.");
        if (!party.ContactAllowed) throw new ConflictException("contact_not_allowed", "التواصل مع المالك غير مسموح في سجل الطرف.");

        var now = clock.UtcNow;
        var oa = await db.OwnerAccesses.FirstOrDefaultAsync(o => o.CaseId == c.Id && o.PartyId == party.Id);
        var refresh = oa is not null && oa.InvitationStatus != OwnerInvitationStatus.NotSent;
        if (oa is null) db.OwnerAccesses.Add(oa = new OwnerAccess { OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id });
        var token = Tokens.NewToken();
        // Random token; only its hash is stored. A refresh replaces the hash, so any earlier link stops working,
        // and the status returns to Sent so the 14-day expiry is enforced even for an owner who accepted before.
        oa.InvitationTokenHash = Tokens.Sha256(token);
        oa.InvitationStatus = OwnerInvitationStatus.Sent;
        oa.InvitedAt = now;
        oa.InvitationExpiresAt = now.AddDays(14);
        oa.RevokedAt = null;

        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == c.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var baseUrl = (config["Web:PublicBaseUrl"] ?? config.GetSection("Web:AllowedOrigins").Get<string[]>()?.FirstOrDefault() ?? "").TrimEnd('/');
        var path = $"/invite/{token}";
        var expires = oa.InvitationExpiresAt.Value.ToOffset(TimeSpan.FromHours(3));
        var result = await sms.SendAsync(pii.Unprotect(party.PhoneEnc),
            $"رهون: دعاك {org} لمتابعة حالتك {c.Reference} بأمان. افتح الرابط {baseUrl}{path} (صالح حتى {expires:yyyy-MM-dd}). لا تشارك الرابط مع أحد.",
            c.OrganizationId, c.Id, "TPL-INVITE-01");

        await audit.RecordAsync(new AuditEntry("owner.invitation_sent", refresh ? "تجديد دعوة المالك" : "إرسال دعوة المالك", c.Id, c.Reference,
            Detail: $"رسالة نصية إلى {party.PhoneMasked} · صالحة 14 يوماً حتى {expires:yyyy-MM-dd}" + (refresh ? " · أُبطل الرابط السابق" : ""),
            OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        // The raw link is only echoed where OTP codes are echoed too (development/testing sandboxes), never in production.
        var exposeLink = !env.IsProduction() && (env.IsDevelopment() || auth.ExposeSandboxOtp);
        return Results.Ok(new
        {
            status = "sent", refreshed = refresh, expiresAt = oa.InvitationExpiresAt, destination = party.PhoneMasked, channel = "sms",
            smsState = result.State, devLink = exposeLink ? path : null,
        });
    }

    private static async Task<IResult> SavePreferences(string reference, ContactPreferencesRequest req, CaseAccess access, RahoonDbContext db,
        RequestContext rc, IClock clock, AuditLog audit)
    {
        var channels = req.AllowedChannels?.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToList();
        new Validator()
            .Require(channels is null || channels.All(ChannelLabels.ContainsKey), "allowedChannels", "قناة تواصل غير معروفة.")
            .Require(channels is null || channels.Count > 0, "allowedChannels", "اختر قناة واحدة على الأقل.")
            .Require(req.ContactHours is null || req.ContactHours.Trim().Length <= 100, "contactHours", "أوقات التواصل حتى 100 حرف.")
            .Require(req.CommunicationNeeds is null || req.CommunicationNeeds.Trim().Length <= 500, "communicationNeeds", "احتياجات التواصل حتى 500 حرف.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary) ?? throw new ConflictException("no_primary", "لا يوجد مالك أساسي للحالة.");
        var oa = await db.OwnerAccesses.FirstOrDefaultAsync(o => o.CaseId == c.Id && o.PartyId == party.Id);
        if (oa is null) db.OwnerAccesses.Add(oa = new OwnerAccess { OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id });
        var changed = new List<string>();
        if (channels is not null && !channels.SequenceEqual(oa.AllowedChannels)) { oa.AllowedChannels = channels; changed.Add("القنوات المسموحة"); }
        if (req.ContactHours is not null && req.ContactHours.Trim() != (oa.ContactHours ?? "")) { oa.ContactHours = req.ContactHours.Trim(); changed.Add("أوقات التواصل"); }
        if (req.CommunicationNeeds is not null && req.CommunicationNeeds.Trim() != (oa.CommunicationNeeds ?? ""))
        { oa.CommunicationNeeds = req.CommunicationNeeds.Trim().Length == 0 ? null : req.CommunicationNeeds.Trim(); changed.Add("احتياجات التواصل"); }
        if (changed.Count > 0)
        {
            await audit.RecordAsync(new AuditEntry("owner.contact_preferences", "تحديث تفضيلات التواصل مع المالك", c.Id, c.Reference,
                Detail: "الحقول: " + string.Join("، ", changed), OrganizationId: c.OrganizationId));
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return Results.Ok(await PreferencesPayloadAsync(db, rc, c, clock));
    }

    // ───────── POA request ─────────

    private static async Task<IResult> RequestPoa(string reference, Guid partyId, PoaRequestBody req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit, Notifier notifier)
    {
        var today = clock.TodayRiyadh;
        var due = req.DueOn ?? today.AddDays(10);
        new Validator().Require(due > today, "dueOn", "المهلة يجب أن تكون في المستقبل.")
            .Require(req.Channels is null || req.Channels.All(x => x is "portal" or "sms"), "channels", "قناة غير معروفة.").ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == partyId && p.CaseId == c.Id) ?? throw new NotFoundException();
        if (!PoaRoles.Contains(party.Role)) throw new ConflictException("poa_not_applicable", "الوكالة تُطلب للوكيل أو الممثل فقط.");
        if (party.PoaStatus == PoaStatus.Verified) throw new ConflictException("poa_verified", "الوكالة متحقق منها مسبقاً.");
        if (await db.DocumentRequests.AnyAsync(r => r.CaseId == c.Id && r.PartyId == party.Id && r.DocumentTypeKey == "poa" && r.Status == DocumentRequestStatus.Open))
            throw new ConflictException("poa_pending", "يوجد طلب وكالة مفتوح لهذا الطرف.");
        var type = await db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Key == "poa") ?? throw new ConflictException("poa_type_missing", "نوع المستند «وكالة موثقة» غير معرّف في الكتالوج.");

        var doc = new CaseDocument
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = type.Key, Name = $"{type.NameAr} — {party.DisplayName}", Source = DocumentSource.Owner,
            Status = DocumentStatus.Requested, VisibleToOwner = true, VisibleTo = ["case_team", "legal"],
        };
        db.Documents.Add(doc);
        var rendered = await DocumentRequestTemplates.RenderAsync(db, c.OrganizationId, $"{type.NameAr} لـ{party.DisplayName}", due);
        db.DocumentRequests.Add(new DocumentRequest
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentId = doc.Id, DocumentTypeKey = type.Key, RequestedFrom = "owner", PartyId = party.Id, DueOn = due,
            OwnerMessage = rendered.Text, Channels = req.Channels ?? ["portal", "sms"], RequestedByUserId = rc.UserId,
        });
        party.PoaStatus = PoaStatus.Requested;
        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "document", $"مطلوب: {type.NameAr}", rendered.Text, "/owner/documents", c.Id);
        await audit.RecordAsync(new AuditEntry("document.requested", $"طلب وكالة موثقة ({party.DisplayName})", c.Id, c.Reference,
            Detail: $"المهلة {due:yyyy-MM-dd} · من قالب {rendered.Code}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { documentId = doc.Id, dueOn = due, ownerMessage = rendered.Text });
    }
}
