using ClaudCare.Api.Domain;
using ClaudCare.Api.Tests.Infrastructure;
using ClaudCare.Api.Tests.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Tests.Communication;

[Collection(ApiCollection.Name)]
public class NotificationTests(ApiFactory root)
{
    [Fact]
    public async Task List_count_read_delete_and_restore_work_per_user()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var other = await c.PatientAsync();
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10);
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H11);

        var list = await c.Http.GetApiAsync("/api/notifications", p.Token);
        Assert.Equal(2, list["unread"].GetInt32());
        Assert.Equal(2, list["page"].J("total").GetInt32());
        Assert.Equal(2, (await c.Http.GetApiAsync("/api/notifications/unread-count", p.Token))["unread"].GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/notifications/unread-count", other.Token))["unread"].GetInt32());

        var id = list["page"].J("items")[0].GetProperty("id").GetInt32();
        Assert.Equal(404, (await c.Http.PostAsync($"/api/notifications/{id}/read", null, other.Token)).Status); // إشعار غيره
        Assert.Equal(204, (await c.Http.PostAsync($"/api/notifications/{id}/read", null, p.Token)).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/notifications?unread=true", p.Token))["page"].J("total").GetInt32());

        Assert.Equal(204, (await c.Http.DeleteAsync($"/api/notifications/{id}", p.Token)).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/notifications", p.Token))["page"].J("total").GetInt32());
        Assert.Equal(404, (await c.Http.DeleteAsync($"/api/notifications/{id}", p.Token)).Status);
        Assert.Equal(200, (await c.Http.PostAsync($"/api/notifications/{id}/restore", null, p.Token)).Status);
        Assert.Equal(2, (await c.Http.GetApiAsync("/api/notifications", p.Token))["page"].J("total").GetInt32());

        Assert.Equal(1, (await c.Http.PostAsync("/api/notifications/read-all", null, p.Token))["marked"].GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/notifications/unread-count", p.Token))["unread"].GetInt32());
        Assert.Equal(401, (await c.Http.GetApiAsync("/api/notifications")).Status);
    }

    [Fact]
    public async Task Filters_by_type()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/notifications?type=Appointment", p.Token))["page"].J("total").GetInt32());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/notifications?type=Invoice", p.Token))["page"].J("total").GetInt32());
    }
}

[Collection(ApiCollection.Name)]
public class MessagingTests(ApiFactory root)
{
    [Fact]
    public async Task Patient_can_message_only_doctors_they_have_a_booking_with()
    {
        var c = await Clinic.CreateAsync(root);
        var seen = await c.DoctorAsync();
        var stranger = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.BookOkAsync(p.Token, seen.Id, Clinic.Wed, Clinic.H10);

        Assert.Equal(403, (await c.Http.PostAsync("/api/conversations", new { userId = stranger.Id }, p.Token)).Status);
        var ok = await c.Http.PostAsync("/api/conversations", new { userId = seen.Id, text = "مرحبا دكتور" }, p.Token);
        Assert.Equal(200, ok.Status);
        var again = await c.Http.PostAsync("/api/conversations", new { userId = seen.Id }, p.Token);
        Assert.Equal(ok["id"].GetInt32(), again["id"].GetInt32()); // محادثة واحدة لكل زوج
        Assert.Equal(400, (await c.Http.PostAsync("/api/conversations", new { userId = p.Id }, p.Token)).Status);

        // الطبيب يبدأ مع مريضه لا مع غريب
        var doctorToken = await c.DoctorTokenAsync(seen);
        var strangerPatient = await c.PatientAsync();
        Assert.Equal(404, (await c.Http.PostAsync("/api/conversations", new { userId = strangerPatient.Id }, doctorToken)).Status);
    }

    [Fact]
    public async Task Sending_counts_unread_notifies_once_and_reading_clears_it()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync();
        var p = await c.PatientAsync();
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10);
        var dt = await c.DoctorTokenAsync(doc);
        var conv = (await c.Http.PostAsync("/api/conversations", new { userId = doc.Id }, p.Token))["id"].GetInt32();

        Assert.Equal(201, (await c.Http.PostAsync($"/api/conversations/{conv}/messages", new { text = "السلام عليكم" }, p.Token)).Status);
        Assert.Equal(201, (await c.Http.PostAsync($"/api/conversations/{conv}/messages", new { text = "عندي سؤال" }, p.Token)).Status);

        var docList = await c.Http.GetApiAsync("/api/conversations", dt);
        Assert.Equal(2, docList.Body[0].GetProperty("unread").GetInt32());
        Assert.Equal("عندي سؤال", docList.Body[0].GetProperty("lastMessage").GetString());
        Assert.Equal(2, (await c.Http.GetApiAsync("/api/conversations/unread-count", dt))["unread"].GetInt32());
        Assert.Equal(1, (await c.NotificationTitlesAsync(doc.Id)).Count(t => t.StartsWith("رسالة من"))); // إشعار واحد للدفعة

        var msgs = await c.Http.GetApiAsync($"/api/conversations/{conv}/messages", dt);
        Assert.Equal(["السلام عليكم", "عندي سؤال"], msgs["messages"].EnumerateArray().Select(m => m.GetProperty("text").GetString()));
        Assert.False(msgs["messages"][0].GetProperty("mine").GetBoolean());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/conversations/unread-count", dt))["unread"].GetInt32());

        await c.Http.PostAsync($"/api/conversations/{conv}/messages", new { text = "تفضل" }, dt);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/conversations", p.Token)).Body[0].GetProperty("unread").GetInt32());
        Assert.Equal(204, (await c.Http.PostAsync($"/api/conversations/{conv}/read", null, p.Token)).Status);
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/conversations/unread-count", p.Token))["unread"].GetInt32());
    }

    [Fact]
    public async Task Outsiders_get_404_and_paging_walks_back_through_history()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var outsider = await c.PatientAsync();
        await c.BookOkAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10);
        var conv = (await c.Http.PostAsync("/api/conversations", new { userId = doc.Id }, p.Token))["id"].GetInt32();
        for (var i = 1; i <= 5; i++) await c.Http.PostAsync($"/api/conversations/{conv}/messages", new { text = $"م{i}" }, p.Token);

        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/conversations/{conv}/messages", outsider.Token)).Status);
        Assert.Equal(404, (await c.Http.PostAsync($"/api/conversations/{conv}/messages", new { text = "x" }, outsider.Token)).Status);
        Assert.Equal(400, (await c.Http.PostAsync($"/api/conversations/{conv}/messages", new { text = "" }, p.Token)).Status);

        var page1 = await c.Http.GetApiAsync($"/api/conversations/{conv}/messages?take=2", p.Token);
        Assert.Equal(["م4", "م5"], page1["messages"].EnumerateArray().Select(m => m.GetProperty("text").GetString()));
        Assert.True(page1["hasMore"].GetBoolean());
        var oldest = page1["messages"][0].GetProperty("id").GetInt32();
        var page2 = await c.Http.GetApiAsync($"/api/conversations/{conv}/messages?take=10&before={oldest}", p.Token);
        Assert.Equal(["م1", "م2", "م3"], page2["messages"].EnumerateArray().Select(m => m.GetProperty("text").GetString()));
        Assert.False(page2["hasMore"].GetBoolean());
    }

    [Fact]
    public async Task Support_conversation_is_shared_with_the_front_desk_who_join_on_reply()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var desk = await c.TokenAsync(UserRole.Reception);

        var s = await c.Http.PostAsync("/api/conversations/support", new { text = "أحتاج مساعدة بالفاتورة" }, p.Token);
        Assert.Equal("Support", s.Str("kind"));
        Assert.Equal(s["id"].GetInt32(), (await c.Http.PostAsync("/api/conversations/support", null, p.Token))["id"].GetInt32()); // واحدة لكل مريض
        Assert.Equal(403, (await c.Http.PostAsync("/api/conversations/support", null, desk)).Status);

        var queue = await c.Http.GetApiAsync("/api/conversations/support", desk);
        Assert.Contains(queue.Body.EnumerateArray(), x => x.GetProperty("id").GetInt32() == s["id"].GetInt32());
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/conversations/support", p.Token)).Status);

        var id = s["id"].GetInt32();
        Assert.Equal(201, (await c.Http.PostAsync($"/api/conversations/{id}/messages", new { text = "أهلًا، كيف نساعدك؟" }, desk)).Status);
        Assert.Equal(1, (await c.Http.GetApiAsync("/api/conversations", p.Token)).Body[0].GetProperty("unread").GetInt32());
        Assert.Contains((await c.Http.GetApiAsync("/api/conversations", desk)).Body.EnumerateArray(), x => x.GetProperty("id").GetInt32() == id); // انضم للمحادثة

        // ممرض لا يدخل محادثة الدعم
        Assert.Equal(404, (await c.Http.GetApiAsync($"/api/conversations/{id}/messages", await c.TokenAsync(UserRole.Nurse))).Status);
    }
}

[Collection(ApiCollection.Name)]
public class AccountTests(ApiFactory root)
{
    [Fact]
    public async Task Profile_reads_and_updates_and_rejects_a_taken_email()
    {
        var c = await Clinic.CreateAsync(root);
        var a = await c.PatientAsync();
        var b = await c.PatientAsync();

        var ok = await c.Http.PutAsync("/api/me/profile", new { fullName = "ريم الأحمد", email = "Reem.A@Example.com", birthYear = 1992, gender = "Female" }, a.Token);
        Assert.Equal(200, ok.Status);
        Assert.Equal("reem.a@example.com", ok.Str("email"));
        Assert.Equal(1992, (await c.Http.GetApiAsync("/api/me/profile", a.Token))["birthYear"].GetInt32());

        Assert.Equal("email_taken", (await c.Http.PutAsync("/api/me/profile", new { fullName = "مستخدم آخر", email = "reem.a@example.com" }, b.Token)).Code);
        Assert.Equal(400, (await c.Http.PutAsync("/api/me/profile", new { fullName = "م", email = "not-an-email" }, b.Token)).Status);
    }

    [Fact]
    public async Task Doctor_updates_about_and_sees_the_specialty()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync();
        var t = await c.DoctorTokenAsync(doc);
        var r = await c.Http.PutAsync("/api/me/profile", new { fullName = "د. منى", about = "استشارية قلب" }, t);
        Assert.Equal("استشارية قلب", r.Str("about"));
        Assert.False(string.IsNullOrEmpty(r.Str("specialty")));
    }

    [Fact]
    public async Task Family_members_crud_is_private_and_protected_when_in_use()
    {
        var c = await Clinic.CreateAsync(root);
        var doc = await c.DoctorAsync();
        var p = await c.PatientAsync();
        var other = await c.PatientAsync();

        var add = await c.Http.PostAsync("/api/me/family", new { name = "سارة", relation = "ابنة", birthYear = 2015 }, p.Token);
        Assert.Equal(201, add.Status);
        var id = add["id"].GetInt32();
        Assert.Equal(400, (await c.Http.PostAsync("/api/me/family", new { name = "س", relation = "ابنة", birthYear = 2015 }, p.Token)).Status);

        Assert.Equal("سارة", (await c.Http.GetApiAsync("/api/me/family", p.Token)).Body[0].GetProperty("name").GetString());
        Assert.Equal(0, (await c.Http.GetApiAsync("/api/me/family", other.Token)).Body.GetArrayLength());
        Assert.Equal(404, (await c.Http.PutAsync($"/api/me/family/{id}", new { name = "غير", relation = "ابنة", birthYear = 2015 }, other.Token)).Status);
        Assert.Equal("سارة محمد", (await c.Http.PutAsync($"/api/me/family/{id}", new { name = "سارة محمد", relation = "ابنة", birthYear = 2016 }, p.Token)).Str("name"));

        var booked = await c.BookAsync(p.Token, doc.Id, Clinic.Wed, Clinic.H10, familyMemberId: id);
        Assert.Equal(201, booked.Status);
        Assert.Equal("family_member_in_use", (await c.Http.DeleteAsync($"/api/me/family/{id}", p.Token)).Code);

        var second = (await c.Http.PostAsync("/api/me/family", new { name = "خالد", relation = "ابن", birthYear = 2018 }, p.Token))["id"].GetInt32();
        Assert.Equal(204, (await c.Http.DeleteAsync($"/api/me/family/{second}", p.Token)).Status);
        Assert.Equal(403, (await c.Http.GetApiAsync("/api/me/family", await c.DoctorTokenAsync(doc))).Status);
    }

    [Fact]
    public async Task Settings_have_defaults_partial_updates_and_reject_unknown_keys()
    {
        var c = await Clinic.CreateAsync(root);
        var p = await c.PatientAsync();
        var other = await c.PatientAsync();

        var d = await c.Http.GetApiAsync("/api/me/settings", p.Token);
        Assert.True(d["values"].J("appointments").GetBoolean());
        Assert.False(d["values"].J("biometric").GetBoolean());

        var up = await c.Http.PutAsync("/api/me/settings", new { values = new Dictionary<string, bool> { ["biometric"] = true, ["offers"] = true } }, p.Token);
        Assert.True(up["values"].J("biometric").GetBoolean());
        Assert.True(up["values"].J("offers").GetBoolean());
        Assert.True(up["values"].J("appointments").GetBoolean()); // لم يُمسّ

        await c.Http.PutAsync("/api/me/settings", new { values = new Dictionary<string, bool> { ["biometric"] = false } }, p.Token);
        Assert.False((await c.Http.GetApiAsync("/api/me/settings", p.Token))["values"].J("biometric").GetBoolean());
        Assert.False((await c.Http.GetApiAsync("/api/me/settings", other.Token))["values"].J("offers").GetBoolean()); // معزولة لكل مستخدم

        Assert.Equal(400, (await c.Http.PutAsync("/api/me/settings", new { values = new Dictionary<string, bool> { ["hack"] = true } }, p.Token)).Status);
        Assert.Equal(400, (await c.Http.PutAsync("/api/me/settings", new { }, p.Token)).Status);
    }
}
