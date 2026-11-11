using FluentAssertions;
using SchoolAdmission.Api.DTOs;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SchoolAdmission.Tests;

public class ConcurrencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ConcurrencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Test2_NoOverbookingUnderHighConcurrency_ExactCapacityAllocated()
    {
        // 1. Create a class with fixed capacity of 5 seats
        const int totalSeats = 5;
        const int totalRequests = 50;

        var classRes = await _client.PostAsJsonAsync("/classes", new CreateClassRequest(
            $"Concurrency Class - {Guid.NewGuid():N}", totalSeats));
        classRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();
        cls.Should().NotBeNull();

        // 2. Prepare 50 concurrent requests from 50 unique 17-digit student IDs
        var results = new ConcurrentBag<(HttpStatusCode StatusCode, string Content)>();

        var tasks = Enumerable.Range(1, totalRequests).Select(async i =>
        {
            var studentId = $"{20260000000000000L + i:D17}";
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
            request.Headers.Add("X-Student-Id", studentId);

            var response = await _client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            results.Add((response.StatusCode, content));
        });

        // 3. Execute all requests simultaneously
        await Task.WhenAll(tasks);

        // 4. Verify results
        var successful = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        var serverErrors = results.Count(r => (int)r.StatusCode >= 500);

        serverErrors.Should().Be(0, "there must be no 500 internal server errors under concurrency");
        successful.Should().Be(totalSeats, $"exactly {totalSeats} holds should succeed because total capacity is {totalSeats}");
        conflicts.Should().Be(totalRequests - totalSeats, $"remaining {totalRequests - totalSeats} requests must receive 409 Conflict");

        // 5. Verify database and API state
        var classState = await _client.GetFromJsonAsync<ClassDto>($"/classes/{cls!.Id}");
        classState.Should().NotBeNull();
        classState!.TotalSeats.Should().Be(totalSeats);
        classState.HeldSeats.Should().Be(totalSeats);
        classState.ConfirmedSeats.Should().Be(0);
        classState.AvailableSeats.Should().Be(0);

        // Invariant assertion: Held + Confirmed <= Total
        (classState.HeldSeats + classState.ConfirmedSeats).Should().BeLessOrEqualTo(classState.TotalSeats);
    }
}

