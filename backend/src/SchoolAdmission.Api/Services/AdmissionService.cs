using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Data.Entities;
using SchoolAdmission.Api.DTOs;
using System.Data;

namespace SchoolAdmission.Api.Services;

public class AdmissionService : IAdmissionService
{
    private readonly AdmissionDbContext _dbContext;
    private readonly ILogger<AdmissionService> _logger;
    private readonly int _holdDurationSeconds;

    public AdmissionService(
        AdmissionDbContext dbContext,
        IConfiguration configuration,
        ILogger<AdmissionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _holdDurationSeconds = configuration.GetValue("HOLD_DURATION_SECONDS", 600);
    }

    public async Task<CreatedClassDto> CreateClassAsync(CreateClassRequest request, CancellationToken ct = default)
    {
        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new BadRequestException("Class name cannot be empty.");
        }

        if (request.TotalSeats <= 0)
        {
            throw new BadRequestException("Seat capacity must be a positive integer greater than zero.");
        }

        var exists = await _dbContext.Classes
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
        {
            throw new ConflictException($"A class with the name '{trimmedName}' already exists.");
        }

        var classId = "c_" + Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        var admissionClass = new AdmissionClass
        {
            Id = classId,
            Name = trimmedName,
            TotalSeats = request.TotalSeats,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Classes.Add(admissionClass);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created new class '{ClassName}' (ID: {ClassId}) with {TotalSeats} seats",
            admissionClass.Name, admissionClass.Id, admissionClass.TotalSeats);

        return new CreatedClassDto(admissionClass.Id, admissionClass.Name, admissionClass.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassDto>> GetClassesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var classes = await _dbContext.Classes
            .AsNoTracking()
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new ClassDto(
                c.Id,
                c.Name,
                c.TotalSeats,
                c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAtUtc > now),
                c.Holds.Count(h => h.Status == HoldStatus.Confirmed),
                c.TotalSeats - c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAtUtc > now) - c.Holds.Count(h => h.Status == HoldStatus.Confirmed)
            ))
            .ToListAsync(ct);

        return classes;
    }

    public async Task<ClassDto> GetClassByIdAsync(string classId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var classDto = await _dbContext.Classes
            .AsNoTracking()
            .Where(c => c.Id == classId)
            .Select(c => new ClassDto(
                c.Id,
                c.Name,
                c.TotalSeats,
                c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAtUtc > now),
                c.Holds.Count(h => h.Status == HoldStatus.Confirmed),
                c.TotalSeats - c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAtUtc > now) - c.Holds.Count(h => h.Status == HoldStatus.Confirmed)
            ))
            .FirstOrDefaultAsync(ct);

        if (classDto == null)
        {
            throw new NotFoundException($"Class with ID '{classId}' was not found.");
        }

        return classDto;
    }

    public async Task<HoldResponseDto> CreateHoldAsync(string classId, string studentId, CancellationToken ct = default)
    {
        var (isValid, errorMessage) = StudentIdValidator.Validate(studentId);
        if (!isValid)
        {
            throw new BadRequestException(errorMessage!);
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            // Step 1: Lock the Class record exclusively using SELECT ... FOR UPDATE on PostgreSQL
            AdmissionClass? admissionClass;
            if (_dbContext.Database.IsNpgsql())
            {
                admissionClass = await _dbContext.Classes
                    .FromSqlInterpolated($"SELECT * FROM \"Classes\" WHERE \"Id\" = {classId} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                admissionClass = await _dbContext.Classes
                    .FirstOrDefaultAsync(c => c.Id == classId, ct);
            }

            if (admissionClass == null)
            {
                throw new NotFoundException($"Class with ID '{classId}' was not found.");
            }

            var now = DateTime.UtcNow;

            // Step 2: Check if this student already has an active hold or confirmed admission for this class
            var hasActive = await _dbContext.Holds
                .AnyAsync(h => h.ClassId == classId &&
                               h.StudentId == studentId &&
                               (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAtUtc > now)), ct);

            if (hasActive)
            {
                throw new ConflictException("Student already has an active hold or confirmed admission for this class.");
            }

            // Step 3: Count active holds and confirmed seats
            var heldSeats = await _dbContext.Holds
                .CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAtUtc > now, ct);

            var confirmedSeats = await _dbContext.Holds
                .CountAsync(h => h.ClassId == classId && h.Status == HoldStatus.Confirmed, ct);

            var availableSeats = admissionClass.TotalSeats - heldSeats - confirmedSeats;
            if (availableSeats <= 0)
            {
                throw new ConflictException($"No seats available for class '{admissionClass.Name}'.");
            }

            // Step 4: Allocate hold
            var holdId = "h_" + Guid.NewGuid().ToString("N")[..8];
            var expiresAt = now.AddSeconds(_holdDurationSeconds);

            var hold = new AdmissionHold
            {
                Id = holdId,
                ClassId = classId,
                StudentId = studentId,
                Status = HoldStatus.Held,
                ExpiresAtUtc = expiresAt,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.Holds.Add(hold);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Hold {HoldId} created for class {ClassId} by student {StudentIdMasked}. Expires at {ExpiresAt}",
                hold.Id, hold.ClassId, MaskStudentId(studentId), expiresAt);

            return new HoldResponseDto(hold.Id, hold.ClassId, hold.StudentId, hold.Status, hold.ExpiresAtUtc);
        });
    }

    public async Task<GetHoldResponseDto> GetHoldByIdAsync(string holdId, CancellationToken ct = default)
    {
        var hold = await _dbContext.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == holdId, ct);

        if (hold == null)
        {
            throw new NotFoundException($"Hold with ID '{holdId}' was not found.");
        }

        var now = DateTime.UtcNow;
        var status = hold.Status;
        if (status == HoldStatus.Held && hold.ExpiresAtUtc.HasValue && hold.ExpiresAtUtc.Value <= now)
        {
            status = HoldStatus.Expired;
        }

        return new GetHoldResponseDto(hold.Id, hold.ClassId, status, hold.ExpiresAtUtc);
    }

    public async Task<ConfirmHoldResponseDto> ConfirmHoldAsync(string holdId, string studentId, CancellationToken ct = default)
    {
        var (isValid, errorMessage) = StudentIdValidator.Validate(studentId);
        if (!isValid)
        {
            throw new BadRequestException(errorMessage!);
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            AdmissionHold? hold;
            if (_dbContext.Database.IsNpgsql())
            {
                hold = await _dbContext.Holds
                    .FromSqlInterpolated($"SELECT * FROM \"Holds\" WHERE \"Id\" = {holdId} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                hold = await _dbContext.Holds.FirstOrDefaultAsync(h => h.Id == holdId, ct);
            }

            if (hold == null)
            {
                throw new NotFoundException($"Hold with ID '{holdId}' was not found.");
            }

            if (hold.StudentId != studentId)
            {
                throw new ForbiddenException("You cannot confirm an application belonging to another student.");
            }

            // Idempotency: If already confirmed, return success without altering seat counts
            if (hold.Status == HoldStatus.Confirmed)
            {
                _logger.LogInformation("Hold {HoldId} already confirmed. Idempotent return.", hold.Id);
                return new ConfirmHoldResponseDto(hold.Id, HoldStatus.Confirmed);
            }

            var now = DateTime.UtcNow;
            if (hold.Status == HoldStatus.Expired || (hold.Status == HoldStatus.Held && hold.ExpiresAtUtc.HasValue && hold.ExpiresAtUtc.Value <= now))
            {
                if (hold.Status != HoldStatus.Expired)
                {
                    hold.Status = HoldStatus.Expired;
                    hold.UpdatedAtUtc = now;
                    await _dbContext.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                throw new ConflictException("Hold has expired and cannot be confirmed.");
            }

            if (hold.Status == HoldStatus.Cancelled)
            {
                throw new ConflictException("Hold was cancelled and cannot be confirmed.");
            }

            hold.Status = HoldStatus.Confirmed;
            hold.ConfirmedAtUtc = now;
            hold.UpdatedAtUtc = now;

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Hold {HoldId} confirmed successfully for student {StudentIdMasked}",
                hold.Id, MaskStudentId(studentId));

            return new ConfirmHoldResponseDto(hold.Id, HoldStatus.Confirmed);
        });
    }

    public async Task CancelHoldAsync(string holdId, string studentId, CancellationToken ct = default)
    {
        var (isValid, errorMessage) = StudentIdValidator.Validate(studentId);
        if (!isValid)
        {
            throw new BadRequestException(errorMessage!);
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

            AdmissionHold? hold;
            if (_dbContext.Database.IsNpgsql())
            {
                hold = await _dbContext.Holds
                    .FromSqlInterpolated($"SELECT * FROM \"Holds\" WHERE \"Id\" = {holdId} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                hold = await _dbContext.Holds.FirstOrDefaultAsync(h => h.Id == holdId, ct);
            }

            if (hold == null)
            {
                throw new NotFoundException($"Hold with ID '{holdId}' was not found.");
            }

            if (hold.StudentId != studentId)
            {
                throw new ForbiddenException("You cannot cancel an application belonging to another student.");
            }

            // Idempotent: if already cancelled, treat as success
            if (hold.Status == HoldStatus.Cancelled)
            {
                return;
            }

            if (hold.Status == HoldStatus.Confirmed)
            {
                throw new BadRequestException("Cannot cancel a confirmed admission.");
            }

            var now = DateTime.UtcNow;
            if (hold.Status == HoldStatus.Expired || (hold.Status == HoldStatus.Held && hold.ExpiresAtUtc.HasValue && hold.ExpiresAtUtc.Value <= now))
            {
                if (hold.Status != HoldStatus.Expired)
                {
                    hold.Status = HoldStatus.Expired;
                    hold.UpdatedAtUtc = now;
                    await _dbContext.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                throw new BadRequestException("Cannot cancel an expired hold.");
            }

            hold.Status = HoldStatus.Cancelled;
            hold.CancelledAtUtc = now;
            hold.UpdatedAtUtc = now;

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("Hold {HoldId} cancelled for student {StudentIdMasked}",
                hold.Id, MaskStudentId(studentId));
        });
    }

    public async Task<IReadOnlyList<MyHoldDto>> GetHoldsForStudentAsync(string studentId, CancellationToken ct = default)
    {
        var (isValid, errorMessage) = StudentIdValidator.Validate(studentId);
        if (!isValid)
        {
            throw new BadRequestException(errorMessage!);
        }

        var now = DateTime.UtcNow;

        var holds = await _dbContext.Holds
            .AsNoTracking()
            .Include(h => h.Class)
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAtUtc)
            .Select(h => new MyHoldDto(
                h.Id,
                h.ClassId,
                h.Class.Name,
                h.Status == HoldStatus.Held && h.ExpiresAtUtc.HasValue && h.ExpiresAtUtc.Value <= now ? HoldStatus.Expired : h.Status,
                h.ExpiresAtUtc
            ))
            .ToListAsync(ct);

        return holds;
    }

    public async Task<int> SweepExpiredHoldsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var expiredHolds = await _dbContext.Holds
            .Where(h => h.Status == HoldStatus.Held && h.ExpiresAtUtc.HasValue && h.ExpiresAtUtc.Value <= now)
            .ToListAsync(ct);

        if (expiredHolds.Count == 0)
        {
            return 0;
        }

        foreach (var hold in expiredHolds)
        {
            hold.Status = HoldStatus.Expired;
            hold.UpdatedAtUtc = now;
        }

        var affected = await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Expired hold sweep updated {Count} holds to EXPIRED status", expiredHolds.Count);

        return expiredHolds.Count;
    }

    private static string MaskStudentId(string studentId)
    {
        if (studentId.Length < 6) return "***";
        return studentId[..3] + "******" + studentId[^4..];
    }
}

