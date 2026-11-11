using FluentAssertions;
using SchoolAdmission.Api.DTOs;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SchoolAdmission.Tests;

public class DualInstanceConcurrencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _backendInstance1;
    private readonly HttpClient _backendInstance2;

    public DualInstanceConcurrencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // Simulate two separate backend instances connecting to the same database
        _backendInstance1 = factory.CreateClient();
        _backendInstance2 = factory.CreateClient();
    }

    [Fact]
    public async Task BonusTest_SimultaneousRequestsToMultipleBackendInstances_NeverOverbooks()
    {
        // 1. Create a class with fixed capacity of 6 seats
        const int totalSeats = 6;
        const int requestsPerInstance = 25; // 50 requests in total across both instances

        var classRes = await _backendInstance1.PostAsJsonAsync("/classes", new CreateClassRequest(
            $"Multi-Backend Class - {Guid.NewGuid():N}", totalSeats));
        classRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var cls = await classRes.Content.ReadFromJsonAsync<CreatedClassDto>();
        cls.Should().NotBeNull();

        // 2. Prepare requests distributed evenly between Instance 1 and Instance 2
        var results = new ConcurrentBag<(int InstanceId, HttpStatusCode StatusCode)>();

        var instance1Tasks = Enumerable.Range(1, requestsPerInstance).Select(async i =>
        {
            var studentId = $"{20261000000000000L + i:D17}";
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
            req.Headers.Add("X-Student-Id", studentId);

            var res = await _backendInstance1.SendAsync(req);
            results.Add((1, res.StatusCode));
        });

        var instance2Tasks = Enumerable.Range(1, requestsPerInstance).Select(async i =>
        {
            var studentId = $"{20262000000000000L + i:D17}";
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{cls!.Id}/holds");
            req.Headers.Add("X-Student-Id", studentId);

            var res = await _backendInstance2.SendAsync(req);
            results.Add((2, res.StatusCode));
        });

        // 3. Execute all 50 requests simultaneously across both backend instances
        await Task.WhenAll(instance1Tasks.Concat(instance2Tasks));

        // 4. Verification
        var successful = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflicts = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        successful.Should().Be(totalSeats,
            $"exactly {totalSeats} seats should be reserved across both instances without overbooking");
        conflicts.Should().Be((requestsPerInstance * 2) - totalSeats,
            "all excess concurrent requests across both instances must be rejected with 409 Conflict");

        // 5. Query state from Instance 2 to verify consistent view
        var stateFromInstance2 = await _backendInstance2.GetFromJsonAsync<ClassDto>($"/classes/{cls!.Id}");
        stateFromInstance2.Should().NotBeNull();
        stateFromInstance2!.TotalSeats.Should().Be(totalSeats);
        stateFromInstance2.HeldSeats.Should().Be(totalSeats);
        stateFromInstance2.ConfirmedSeats.Should().Be(0);
        stateFromInstance2.AvailableSeats.Should().Be(0);

        (stateFromInstance2.HeldSeats + stateFromInstance2.ConfirmedSeats).Should().BeLessOrEqualTo(stateFromInstance2.TotalSeats);
    }
}

