using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.DTOs;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SchoolAdmission.Tests;

public class AdmissionHoldTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdmissionHoldTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Test1_HoldCreation_ShouldReserveSeatAndPersistHold()
    {
        // 1. Create a class with available seats
        var createClassResponse = await _client.PostAsJsonAsync("/classes", new CreateClassRequest(
            "Class 6 - Morning Shift - Test1", 50));
        createClassResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdClass = await createClassResponse.Content.ReadFromJsonAsync<CreatedClassDto>();
        createdClass.Should().NotBeNull();

        // 2. Submit a valid hold request
        var studentId = "20152691234567890";
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{createdClass!.Id}/holds");
        request.Headers.Add("X-Student-Id", studentId);

        var holdResponse = await _client.SendAsync(request);
        holdResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var holdDto = await holdResponse.Content.ReadFromJsonAsync<HoldResponseDto>();
        holdDto.Should().NotBeNull();
        holdDto!.HoldId.Should().StartWith("h_");
        holdDto.ClassId.Should().Be(createdClass.Id);
        holdDto.StudentId.Should().Be(studentId);
        holdDto.Status.Should().Be("HELD");
        holdDto.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // 3. Verify the hold is persisted in database
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
            var savedHold = await db.Holds.FindAsync(holdDto.HoldId);
            savedHold.Should().NotBeNull();
            savedHold!.StudentId.Should().Be(studentId);
            savedHold.Status.Should().Be("HELD");
        }

        // 4. Verify held seat count increases correctly
        var classResponse = await _client.GetFromJsonAsync<ClassDto>($"/classes/{createdClass.Id}");
        classResponse.Should().NotBeNull();
        classResponse!.TotalSeats.Should().Be(50);
        classResponse.HeldSeats.Should().Be(1);
        classResponse.ConfirmedSeats.Should().Be(0);
        classResponse.AvailableSeats.Should().Be(49);
    }

    [Fact]
    public async Task Test3_HoldConfirmation_ShouldBeIdempotent_AndIncrementConfirmedSeatsOnce()
    {
        // 1. Create a class
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class 7 - Test3", 10));
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();

        // 2. Create a hold
        var studentId = "20152691234567891";
        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentId);
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponseDto>();

        // 3. First confirmation
        var confirmReq1 = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold!.HoldId}/confirm");
        confirmReq1.Headers.Add("X-Student-Id", studentId);
        var confirmRes1 = await _client.SendAsync(confirmReq1);
        confirmRes1.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmDto1 = await confirmRes1.Content.ReadFromJsonAsync<ConfirmHoldResponseDto>();
        confirmDto1!.Status.Should().Be("CONFIRMED");

        // 4. Verify seat counts after first confirmation
        var classAfter1 = await _client.GetFromJsonAsync<ClassDto>($"/classes/{cls.Id}");
        classAfter1!.ConfirmedSeats.Should().Be(1);
        classAfter1.HeldSeats.Should().Be(0);
        classAfter1.AvailableSeats.Should().Be(9);

        // 5. Confirm again (Idempotency check)
        var confirmReq2 = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold.HoldId}/confirm");
        confirmReq2.Headers.Add("X-Student-Id", studentId);
        var confirmRes2 = await _client.SendAsync(confirmReq2);
        confirmRes2.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmDto2 = await confirmRes2.Content.ReadFromJsonAsync<ConfirmHoldResponseDto>();
        confirmDto2!.Status.Should().Be("CONFIRMED");

        // 6. Verify confirmed seats did NOT increment again
        var classAfter2 = await _client.GetFromJsonAsync<ClassDto>($"/classes/{cls.Id}");
        classAfter2!.ConfirmedSeats.Should().Be(1);
        classAfter2.HeldSeats.Should().Be(0);
        classAfter2.AvailableSeats.Should().Be(9);
    }

    [Fact]
    public async Task ExpiredHold_CannotBeConfirmed_AndReleasesSeat()
    {
        // Custom factory with 1 second hold duration
        var shortFactory = new CustomWebApplicationFactory { HoldDurationSeconds = 1 };
        var shortClient = shortFactory.CreateClient();

        var classRes = await shortClient.PostAsJsonAsync("/classes", new CreateClassRequest("Class Expiration Test", 5));
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();

        var studentId = "20152691234567892";
        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentId);
        var holdRes = await shortClient.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponseDto>();

        // Wait for expiration
        await Task.Delay(1500);

        // Attempt confirmation
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold!.HoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", studentId);
        var confirmRes = await shortClient.SendAsync(confirmReq);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Verify seat was released
        var classDto = await shortClient.GetFromJsonAsync<ClassDto>($"/classes/{cls.Id}");
        classDto!.HeldSeats.Should().Be(0);
        classDto.AvailableSeats.Should().Be(5);
    }

    [Fact]
    public async Task Student_CannotConfirmOrCancel_AnotherStudentsHold()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class Security Test", 5));
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();

        var ownerStudentId = "20152691234567893";
        var otherStudentId = "20152691234567894";

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", ownerStudentId);
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponseDto>();

        // Other student attempts confirmation
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold!.HoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", otherStudentId);
        var confirmRes = await _client.SendAsync(confirmReq);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Other student attempts cancellation
        var cancelReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{hold.HoldId}");
        cancelReq.Headers.Add("X-Student-Id", otherStudentId);
        var cancelRes = await _client.SendAsync(cancelReq);
        cancelRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DuplicateActiveHold_ForSameClass_IsRejected()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class Duplicate Test", 5));
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();

        var studentId = "20152691234567895";

        var holdReq1 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq1.Headers.Add("X-Student-Id", studentId);
        var holdRes1 = await _client.SendAsync(holdReq1);
        holdRes1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Second hold for the same student and class
        var holdReq2 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        holdReq2.Headers.Add("X-Student-Id", studentId);
        var holdRes2 = await _client.SendAsync(holdReq2);
        holdRes2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task HoldCancellation_ReleasesSeat_AndIdempotent()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class Cancel Test", 5));
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();

        var studentId = "20152691234567896";

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentId);
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponseDto>();

        // Cancel hold
        var cancelReq1 = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{hold!.HoldId}");
        cancelReq1.Headers.Add("X-Student-Id", studentId);
        var cancelRes1 = await _client.SendAsync(cancelReq1);
        cancelRes1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Check seat released
        var classDto = await _client.GetFromJsonAsync<ClassDto>($"/classes/{cls.Id}");
        classDto!.HeldSeats.Should().Be(0);
        classDto.AvailableSeats.Should().Be(5);

        // Cancel again (Idempotent DELETE returns 204)
        var cancelReq2 = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{hold.HoldId}");
        cancelReq2.Headers.Add("X-Student-Id", studentId);
        var cancelRes2 = await _client.SendAsync(cancelReq2);
        cancelRes2.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ConfirmedAdmission_CannotBeCancelled()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class Confirmed Cancel Test", 5));
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();

        var studentId = "20152691234567897";

        var holdReq = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        holdReq.Headers.Add("X-Student-Id", studentId);
        var holdRes = await _client.SendAsync(holdReq);
        var hold = await holdRes.Content.ReadFromJsonAsync<HoldResponseDto>();

        // Confirm
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/holds/{hold!.HoldId}/confirm");
        confirmReq.Headers.Add("X-Student-Id", studentId);
        await _client.SendAsync(confirmReq);

        // Attempt cancel
        var cancelReq = new HttpRequestMessage(HttpMethod.Delete, $"/holds/{hold.HoldId}");
        cancelReq.Headers.Add("X-Student-Id", studentId);
        var cancelRes = await _client.SendAsync(cancelReq);
        cancelRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validation_StudentIdRules_AndLeadingZeroesPreserved()
    {
        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest("Class Validation Test", 5));
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();

        // Missing header -> 400
        var reqNoHeader = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
        var resNoHeader = await _client.SendAsync(reqNoHeader);
        resNoHeader.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 16 digits -> 400
        var req16 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        req16.Headers.Add("X-Student-Id", "1234567890123456");
        var res16 = await _client.SendAsync(req16);
        res16.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 18 digits -> 400
        var req18 = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        req18.Headers.Add("X-Student-Id", "123456789012345678");
        var res18 = await _client.SendAsync(req18);
        res18.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Letters -> 400
        var reqLetters = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        reqLetters.Headers.Add("X-Student-Id", "2015269123456789A");
        var resLetters = await _client.SendAsync(reqLetters);
        resLetters.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Leading zeroes preserved (e.g. 01234567890123456)
        var studentWithZero = "01234567890123456";
        var reqZero = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls.Id}/holds");
        reqZero.Headers.Add("X-Student-Id", studentWithZero);
        var resZero = await _client.SendAsync(reqZero);
        resZero.StatusCode.Should().Be(HttpStatusCode.Created);
        var zeroHold = await resZero.Content.ReadFromJsonAsync<HoldResponseDto>();
        zeroHold!.StudentId.Should().Be(studentWithZero);
    }
}

