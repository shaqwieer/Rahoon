using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;

namespace Rahoon.Api.Modules.Cases;

/// <summary>Creates case sub-records with encryption and masking applied consistently.</summary>
public sealed class CaseFactory(RahoonDbContext db, PiiProtector pii)
{
    /// <summary>Allocates the next platform-unique reference RH-YYYY-NNNNNN under a row lock.</summary>
    public async Task<string> NextReferenceAsync(int year)
    {
        var key = $"case:{year}";
        // Upsert-and-increment atomically so concurrent creations never collide.
        var value = await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 5201)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        return $"RH-{year}-{value[0]:D6}";
    }

    public CaseParty NewParty(Guid orgId, Guid caseId, PartyRole role, string fullName, string? nationalId, string? phone,
        bool primary = false, string? email = null, string language = "ar", string? specialNeeds = null, PartyKind kind = PartyKind.Individual)
    {
        var party = new CaseParty
        {
            OrganizationId = orgId, CaseId = caseId, Role = role, Kind = kind, IsPrimary = primary, FullName = fullName.Trim(),
            DisplayName = kind == PartyKind.Individual ? Mask.PersonName(fullName) : fullName.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(), PreferredLanguage = language, SpecialNeeds = specialNeeds,
        };
        SetNationalId(party, nationalId);
        SetPhone(party, phone);
        return party;
    }

    public void SetNationalId(CaseParty party, string? nationalId)
    {
        var digits = new string((nationalId ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return;
        party.NationalIdEnc = pii.Protect(digits);
        party.NationalIdMasked = Mask.NationalId(digits);
        party.NationalIdHash = pii.LookupHash(digits);
    }

    public void SetPhone(CaseParty party, string? phone)
    {
        var digits = NormalizePhone(phone);
        if (digits is null) return;
        party.PhoneEnc = pii.Protect(digits);
        party.PhoneMasked = Mask.Phone(digits);
    }

    public void SetDeed(Property property, string? deed)
    {
        var digits = new string((deed ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return;
        property.DeedNumberEnc = pii.Protect(digits);
        property.DeedNumberMasked = Mask.Deed(digits);
    }

    /// <summary>Saudi mobile normalized to 05XXXXXXXX, or null when empty.</summary>
    public static string? NormalizePhone(string? phone)
    {
        var d = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (d.Length == 0) return null;
        if (d.StartsWith("966")) d = "0" + d[3..];
        if (d.Length == 9 && d.StartsWith('5')) d = "0" + d;
        return d;
    }

    public static bool IsValidSaudiMobile(string? phone) => NormalizePhone(phone) is { Length: 10 } d && d.StartsWith("05");

    public static bool IsValidNationalId(string? id)
    {
        var d = new string((id ?? "").Where(char.IsDigit).ToArray());
        return d.Length == 10 && (d[0] == '1' || d[0] == '2');
    }
}
