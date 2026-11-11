using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.DTOs;

public record CreateClassRequest(
    [Required(ErrorMessage = "Class name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Class name must be between 1 and 200 characters.")]
    string Name,

    [Range(1, 100000, ErrorMessage = "Total seat capacity must be a positive integer greater than 0.")]
    int TotalSeats
);

public record CreatedClassDto(
    string Id,
    string Name,
    int TotalSeats
);

public record ClassDto(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats
);

