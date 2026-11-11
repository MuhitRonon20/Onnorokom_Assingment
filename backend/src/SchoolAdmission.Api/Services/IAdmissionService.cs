using SchoolAdmission.Api.DTOs;

namespace SchoolAdmission.Api.Services;

public interface IAdmissionService
{
    Task<CreatedClassDto> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ClassDto>> GetClassesAsync(CancellationToken ct = default);
    Task<ClassDto> GetClassByIdAsync(string classId, CancellationToken ct = default);
    Task<HoldResponseDto> CreateHoldAsync(string classId, string studentId, CancellationToken ct = default);
    Task<GetHoldResponseDto> GetHoldByIdAsync(string holdId, CancellationToken ct = default);
    Task<ConfirmHoldResponseDto> ConfirmHoldAsync(string holdId, string studentId, CancellationToken ct = default);
    Task CancelHoldAsync(string holdId, string studentId, CancellationToken ct = default);
    Task<IReadOnlyList<MyHoldDto>> GetHoldsForStudentAsync(string studentId, CancellationToken ct = default);
    Task<int> SweepExpiredHoldsAsync(CancellationToken ct = default);
}

