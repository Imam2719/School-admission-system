using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Tests;

public class ApplicationFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _http = factory.CreateClient();

    private async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Apply_approve_pay_flow_and_seat_limit()
    {
        var classId = (await Json(await _http.PostAsJsonAsync("/classes", new { name = "Flow " + Guid.NewGuid(), totalSeats = 1 }))).GetProperty("id").GetString()!;

        var res = await _http.PostAsJsonAsync("/applications", new { guardianName = "Rahim", studentName = "Karim " + Guid.NewGuid(), nid = "1234567890", classId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var a = await Json(res);
        Assert.Matches(@"^\d{14}$", a.GetProperty("studentId").GetString()!);
        Assert.Equal("PENDING", a.GetProperty("status").GetString());

        // nothing is held until the admin approves
        Assert.Equal(1, (await Json(await _http.GetAsync($"/classes/{classId}"))).GetProperty("availableSeats").GetInt32());

        var approved = await Json(await _http.PostAsync($"/applications/{a.GetProperty("id").GetString()}/approve", null));
        Assert.Equal("APPROVED", approved.GetProperty("status").GetString());
        var holdId = approved.GetProperty("holdId").GetString();

        var confirm = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
        confirm.Headers.Add("X-Student-Id", a.GetProperty("studentId").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _http.SendAsync(confirm)).StatusCode);
        Assert.Equal(1, (await Json(await _http.GetAsync($"/classes/{classId}"))).GetProperty("confirmedSeats").GetInt32());

        // second applicant cannot be approved: class is full
        var b = await Json(await _http.PostAsJsonAsync("/applications", new { guardianName = "Salma", studentName = "Other " + Guid.NewGuid(), nid = "9876543210", classId }));
        Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsync($"/applications/{b.GetProperty("id").GetString()}/approve", null)).StatusCode);
    }
}
