using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Tests;

public class AdmissionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _http = factory.CreateClient();

    private async Task<string> CreateClass(int seats)
    {
        var res = await _http.PostAsJsonAsync("/classes", new { name = "Test " + Guid.NewGuid(), totalSeats = seats });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string url, string? student = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (student != null) req.Headers.Add("X-Student-Id", student);
        return _http.SendAsync(req);
    }

    private async Task<JsonElement> GetClass(string id) =>
        await _http.GetFromJsonAsync<JsonElement>($"/classes/{id}");

    // ---- THE concurrency test: seats must never be oversold ----
    [Fact]
    public async Task Concurrent_holds_never_oversell_seats()
    {
        const int seats = 10, requests = 50;
        var classId = await CreateClass(seats);

        var results = await Task.WhenAll(Enumerable.Range(0, requests).Select(i =>
            Task.Run(() => Send(HttpMethod.Post, $"/classes/{classId}/holds", $"student-{Guid.NewGuid()}"))));

        Assert.Equal(seats, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(requests - seats, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var c = await GetClass(classId);
        Assert.Equal(seats, c.GetProperty("heldSeats").GetInt32() + c.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, c.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Concurrent_hold_and_confirm_mix_never_exceeds_total()
    {
        const int seats = 5;
        var classId = await CreateClass(seats);

        var tasks = Enumerable.Range(0, 30).Select(i => Task.Run(async () =>
        {
            var student = $"mix-{i}-{Guid.NewGuid()}";
            var res = await Send(HttpMethod.Post, $"/classes/{classId}/holds", student);
            if (res.StatusCode == HttpStatusCode.Created && i % 2 == 0)
            {
                var id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("holdId").GetString();
                await Send(HttpMethod.Post, $"/holds/{id}/confirm", student);
            }
        }));
        await Task.WhenAll(tasks);

        var c = await GetClass(classId);
        var total = c.GetProperty("heldSeats").GetInt32() + c.GetProperty("confirmedSeats").GetInt32();
        Assert.True(total <= seats, $"oversold: {total} > {seats}");
        Assert.Equal(seats, total); // 30 students for 5 seats: all seats get used
    }

    [Fact]
    public async Task Same_student_cannot_hold_twice_even_concurrently()
    {
        var classId = await CreateClass(10);
        var results = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ =>
            Task.Run(() => Send(HttpMethod.Post, $"/classes/{classId}/holds", "same-student"))));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(14, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Confirm_is_idempotent_and_owner_only()
    {
        var classId = await CreateClass(3);
        var res = await Send(HttpMethod.Post, $"/classes/{classId}/holds", "alice");
        var holdId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("holdId").GetString();

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, $"/holds/{holdId}/confirm", "bob")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Delete, $"/holds/{holdId}", "bob")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, $"/holds/{holdId}/confirm", "alice")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, $"/holds/{holdId}/confirm", "alice")).StatusCode);

        var c = await GetClass(classId);
        Assert.Equal(1, c.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(0, c.GetProperty("heldSeats").GetInt32());
        Assert.Equal(2, c.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Cancel_frees_the_seat()
    {
        var classId = await CreateClass(1);
        var res = await Send(HttpMethod.Post, $"/classes/{classId}/holds", "alice");
        var holdId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("holdId").GetString();

        Assert.Equal(HttpStatusCode.Conflict, (await Send(HttpMethod.Post, $"/classes/{classId}/holds", "bob")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(HttpMethod.Delete, $"/holds/{holdId}", "alice")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Send(HttpMethod.Post, $"/classes/{classId}/holds", "bob")).StatusCode);
    }

    [Fact]
    public async Task Expired_hold_frees_seat_and_cannot_be_confirmed()
    {
        var classId = await CreateClass(1);
        var res = await Send(HttpMethod.Post, $"/classes/{classId}/holds", "alice");
        var holdId = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("holdId").GetString()!;

        // Simulate time passing: move the expiry into the past.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.Holds.Where(h => h.Id == holdId)
                .ExecuteUpdateAsync(s => s.SetProperty(h => h.ExpiresAt, DateTime.UtcNow.AddSeconds(-5)));
        }

        var hold = await _http.GetFromJsonAsync<JsonElement>($"/holds/{holdId}");
        Assert.Equal("EXPIRED", hold.GetProperty("status").GetString());
        Assert.Equal(1, (await GetClass(classId)).GetProperty("availableSeats").GetInt32());

        Assert.Equal(HttpStatusCode.Conflict, (await Send(HttpMethod.Post, $"/holds/{holdId}/confirm", "alice")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Send(HttpMethod.Post, $"/classes/{classId}/holds", "bob")).StatusCode);
    }

    [Fact]
    public async Task Missing_student_header_is_rejected()
    {
        var classId = await CreateClass(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Post, $"/classes/{classId}/holds")).StatusCode);
    }
}
