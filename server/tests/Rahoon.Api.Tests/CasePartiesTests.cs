using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>L06 parties, contact preferences, owner invitation and POA requests.</summary>
[Collection(ApiCollection.Name)]
public sealed class CasePartiesTests(ApiFixture api)
{
    private const string Anchor = "RH-2026-004172";

    [Fact]
    public async Task Parties_are_masked_and_carry_contact_preferences()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var (s, body) = await sara.GetAsync($"/api/cases/{Anchor}/parties");
        Assert.Equal(HttpStatusCode.OK, s);
        var text = body!.ToJsonString();
        Assert.DoesNotContain("1098734542", text);
        Assert.DoesNotContain("0551234581", text);

        var items = body["items"]!.AsArray();
        Assert.True(items.Count >= 3);
        var primary = items.First(i => i!["isPrimary"]!.GetValue<bool>())!;
        Assert.Equal("1•••••••42", TestClient.Str(primary, "nationalIdMasked"));
        Assert.Equal("المالك والمقترض", TestClient.Str(primary, "roleLabel"));
        Assert.Equal("verified_user", TestClient.Str(primary["note"], "icon"));
        var occupant = items.First(i => TestClient.Str(i, "role") == "occupant")!;
        Assert.False(occupant["contactAllowed"]!.GetValue<bool>());
        Assert.False(occupant["actions"]!["message"]!.GetValue<bool>());
        var rep = items.First(i => TestClient.Str(i, "role") == "informal_representative")!;
        Assert.False(rep["financialVisibility"]!.GetValue<bool>());

        var contact = body["contact"]!;
        Assert.Equal("accepted", TestClient.Str(contact["invitation"], "status"));
        Assert.Equal("المنصة، رسائل نصية", TestClient.Str(contact, "allowedChannelsLabel"));
        Assert.Contains("بحضور ابنه", TestClient.Str(contact, "communicationNeeds"));
        Assert.Equal("مكتمل", TestClient.Str(contact["identityVerification"], "label"));
    }

    [Fact]
    public async Task Parties_are_tenant_isolated_with_the_same_refusal_as_unknown_cases()
    {
        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        var foreign = await maha.GetAsync($"/api/cases/{Anchor}/parties");
        var unknown = await maha.GetAsync("/api/cases/RH-2099-000001/parties");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.Status);
        Assert.Equal(foreign.Status, unknown.Status);
        Assert.Equal(TestClient.Str(foreign.Body, "title"), TestClient.Str(unknown.Body, "title"));
        Assert.Equal(HttpStatusCode.Forbidden, (await maha.PostAsync($"/api/cases/{Anchor}/owner-invitation")).Status);

        var omar = await api.LoginAsync("o.alanazi@valuer-b.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await omar.GetAsync($"/api/cases/{Anchor}/parties")).Status);
    }

    [Fact]
    public async Task Adding_and_editing_parties_is_validated_permissioned_and_audited()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());

        var (sBad, bad) = await sara.PostAsync($"/api/cases/{r}/parties", new { role = "owner_borrower", fullName = "س" });
        Assert.Equal(HttpStatusCode.BadRequest, sBad);
        Assert.NotNull(bad!["errors"]!["role"]);
        Assert.NotNull(bad["errors"]!["fullName"]);
        var (sNoId, noId) = await sara.PostAsync($"/api/cases/{r}/parties", new { role = "guarantor", fullName = "فهد سالم العنزي", phone = "0551239999" });
        Assert.Equal(HttpStatusCode.BadRequest, sNoId);
        Assert.NotNull(noId!["errors"]!["nationalId"]);

        // Credit analyst has no case.edit.
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await fahad.PostAsync($"/api/cases/{r}/parties", new { role = "guarantor", fullName = "فهد سالم العنزي", nationalId = "1077665544" })).Status);

        var (sOk, added) = await sara.PostAsync($"/api/cases/{r}/parties",
            new { role = "guarantor", fullName = "فهد سالم العنزي", nationalId = "1077665544", phone = "0551239999", relation = "كفيل غارم" });
        Assert.True(sOk == HttpStatusCode.OK, added?.ToJsonString());
        var id = TestClient.Str(added, "id");
        Assert.Equal(HttpStatusCode.Conflict, (await sara.PostAsync($"/api/cases/{r}/parties", new { role = "guarantor", fullName = "فهد سالم العنزي", nationalId = "1077665544" })).Status);

        var (_, list) = await sara.GetAsync($"/api/cases/{r}/parties");
        Assert.DoesNotContain("1077665544", list!.ToJsonString());
        var g = list["items"]!.AsArray().First(i => TestClient.Str(i, "id") == id)!;
        Assert.Equal("1•••••••44", TestClient.Str(g, "nationalIdMasked"));
        Assert.True(g["isContractParty"]!.GetValue<bool>());

        // The primary owner's identity/phone are locked here; other fields of other parties can be edited.
        var primaryId = TestClient.Str(list["items"]!.AsArray().First(i => i!["isPrimary"]!.GetValue<bool>()), "id");
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PatchAsync($"/api/cases/{r}/parties/{primaryId}", new { phone = "0559998877" })).Status);
        var (sPatch, patched) = await sara.PatchAsync($"/api/cases/{r}/parties/{id}", new { relation = "كفيل متضامن", contactAllowed = false });
        Assert.Equal(HttpStatusCode.OK, sPatch);
        Assert.Contains(patched!["changed"]!.AsArray(), x => x!.GetValue<string>() == "العلاقة");

        var events = await api.WithDbAsync(db => db.AuditEvents.Where(e => e.CaseReference == r && (e.Type == "party.added" || e.Type == "party.updated")).ToListAsync());
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.DoesNotContain("1077665544", e.Title + e.Detail));
    }

    [Fact]
    public async Task Owner_invitation_uses_a_fresh_hashed_token_with_14_day_expiry()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        // Not before the case left Draft.
        var (_, draft) = await sara.PostAsync("/api/cases/drafts");
        var (sDraft, draftRes) = await sara.PostAsync($"/api/cases/{TestClient.Str(draft, "reference")}/owner-invitation");
        Assert.Equal(HttpStatusCode.Conflict, sDraft);
        Assert.Equal("case_draft", TestClient.Str(draftRes, "code"));

        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        var (s1, first) = await sara.PostAsync($"/api/cases/{r}/owner-invitation");
        Assert.Equal(HttpStatusCode.OK, s1);
        var link1 = TestClient.Str(first, "devLink");
        Assert.StartsWith("/invite/", link1);
        var token1 = link1["/invite/".Length..];
        var expires = first!["expiresAt"]!.GetValue<DateTimeOffset>();
        Assert.InRange(expires - DateTimeOffset.UtcNow, TimeSpan.FromDays(13.9), TimeSpan.FromDays(14.1));

        var stored = await api.WithDbAsync(db => db.OwnerAccesses.FirstAsync(o => db.Cases.Any(c => c.Id == o.CaseId && c.Reference == r)));
        Assert.Equal(Rahoon.Api.Infrastructure.Security.Tokens.Sha256(token1), stored.InvitationTokenHash);
        var sms = await api.WithDbAsync(db => db.OutboundMessages.Where(m => m.CaseId == stored.CaseId && m.Channel == MessageChannel.Sms).ToListAsync());
        Assert.Contains(sms, m => m.Body.Contains(link1) && m.Status == OutboundStatus.Simulated);

        // The owner can sign in with it (wizard owner ID 1012345678 → last 4 = 5678).
        var owner = api.Client();
        var (sPub, pub) = await owner.GetAsync($"/api/public/invitations/{token1}");
        Assert.Equal("active", TestClient.Str(pub, "status"));
        var (_, sent) = await owner.PostAsync("/api/auth/owner/verify-id", new { token = token1, idLast4 = "5678" });
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/auth/owner/verify-otp", new { token = token1, code = TestClient.Str(sent, "sandboxCode") })).Status);

        // Refresh: the old link stops working, the new one is active and expires.
        var (s2, second) = await sara.PostAsync($"/api/cases/{r}/owner-invitation");
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.True(second!["refreshed"]!.GetValue<bool>());
        var token2 = TestClient.Str(second, "devLink")["/invite/".Length..];
        Assert.NotEqual(token1, token2);
        Assert.Equal("invalid", TestClient.Str((await owner.GetAsync($"/api/public/invitations/{token1}")).Body, "status"));
        Assert.Equal("active", TestClient.Str((await owner.GetAsync($"/api/public/invitations/{token2}")).Body, "status"));
        // Refreshing the link does not end the owner's signed-in session.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/owner/home")).Status);
        await api.WithDbAsync(async db =>
        {
            var a = await db.OwnerAccesses.FirstAsync(o => o.CaseId == stored.CaseId);
            a.InvitationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });
        Assert.Equal("expired", TestClient.Str((await owner.GetAsync($"/api/public/invitations/{token2}")).Body, "status"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "owner.invitation_sent" && e.Title == "تجديد دعوة المالك")));

        // Case officer can invite too (case.edit); analyst cannot.
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await fahad.PostAsync($"/api/cases/{r}/owner-invitation")).Status);
    }

    [Fact]
    public async Task Contact_preferences_and_poa_request()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        var (sBad, _) = await sara.PutAsync($"/api/cases/{r}/contact-preferences", new { allowedChannels = new[] { "fax" } });
        Assert.Equal(HttpStatusCode.BadRequest, sBad);
        var (sOk, prefs) = await sara.PutAsync($"/api/cases/{r}/contact-preferences",
            new { allowedChannels = new[] { "platform", "call" }, contactHours = "بعد 4 مساءً", communicationNeeds = "يفضّل التواصل الكتابي." });
        Assert.Equal(HttpStatusCode.OK, sOk);
        Assert.Equal("المنصة، مكالمة هاتفية", TestClient.Str(prefs, "allowedChannelsLabel"));
        Assert.Equal("not_sent", TestClient.Str(prefs!["invitation"], "status"));

        var (_, added) = await sara.PostAsync($"/api/cases/{r}/parties", new { role = "informal_representative", fullName = "سالم خالد الغامدي", relation = "ابن المالك", phone = "0551230000" });
        var partyId = TestClient.Str(added, "id");
        var due = B3Scenarios.TodayRiyadh.AddDays(7).ToString("yyyy-MM-dd");
        var (sPoa, poa) = await sara.PostAsync($"/api/cases/{r}/parties/{partyId}/poa-request", new { dueOn = due });
        Assert.True(sPoa == HttpStatusCode.OK, poa?.ToJsonString());
        Assert.Contains(due, TestClient.Str(poa, "ownerMessage"));
        var (sAgain, again) = await sara.PostAsync($"/api/cases/{r}/parties/{partyId}/poa-request", new { dueOn = due });
        Assert.Equal(HttpStatusCode.Conflict, sAgain);
        Assert.Equal("poa_pending", TestClient.Str(again, "code"));

        var party = await api.WithDbAsync(db => db.Parties.FirstAsync(p => p.Id == Guid.Parse(partyId)));
        Assert.Equal(PoaStatus.Requested, party.PoaStatus);
        Assert.True(await api.WithDbAsync(db => db.DocumentRequests.AnyAsync(x => x.PartyId == party.Id && x.DocumentTypeKey == "poa")));
        var (_, list) = await sara.GetAsync($"/api/cases/{r}/parties");
        var rep = list!["items"]!.AsArray().First(i => TestClient.Str(i, "id") == partyId)!;
        Assert.Equal("requested", TestClient.Str(rep, "poaStatus"));
        Assert.Equal("warn", TestClient.Str(rep["note"], "tone"));
    }
}
