using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

public record HoldResponseDto(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? ExpiresAt
);

public record GetHoldResponseDto(
    string HoldId,
    string ClassId,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? ExpiresAt
);

public record ConfirmHoldResponseDto(
    string HoldId,
    string Status
);

public record MyHoldDto(
    string HoldId,
    string ClassId,
    string ClassName,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? ExpiresAt
);

