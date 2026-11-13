using TalebElm.Application.DTOs;
using TalebElm.Application.Services.Abstraction;
using TalebElm.Domain.Entities;
using TalebElm.Domain.Exceptions;
using TalebElm.Domain.Interfaces;

namespace TalebElm.Application.Services.Concrete;

public class UserProgressService : IUserProgressService
{
    private readonly IUnitOfWork _unitOfWork;

    public UserProgressService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ProgressResponse>> GetMyProgressAsync(Guid userId, CancellationToken ct = default)
    {
        var progress = await _unitOfWork.UserProgresses.GetAllAsync(ct);
        return progress
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.Module?.Order ?? int.MaxValue)
            .Select(p => new ProgressResponse(p.ModuleId, p.IsUnlocked, p.PassedExam, p.Score))
            .ToList();
    }

    public async Task<IReadOnlyList<ProgressResponse>> GetByTrackAsync(Guid userId, Guid trackId, CancellationToken ct = default)
    {
        var progress = await _unitOfWork.UserProgresses.GetByUserAndTrackAsync(userId, trackId, ct);
        return progress
            .Select(p => new ProgressResponse(p.ModuleId, p.IsUnlocked, p.PassedExam, p.Score))
            .ToList();
    }

    public async Task TryUnlockNextModuleAsync(Guid userId, Guid moduleId, CancellationToken ct = default)
    {
        UserProgress? progress = await _unitOfWork.UserProgresses.GetByUserAndModuleAsync(userId, moduleId, ct) ?? throw new ValidationException("Invalid User Progress");

        // Make the conditions
        bool hasExam = progress.Module.Exam != null;
        //bool hasReq = progress.Module.Problems.Any(mp => mp.IsRequired == true); // No navigation property for problems in Module entity

        bool examGate = !hasExam || progress.PassedExam;
        bool problemGate = progress.CompletedRequiredProblems; // Change to: `!hasReq || progress.CompletedRequiredProblems`, if problems navigation property is added


        if (examGate && problemGate)
        {
            Module? nextModule = progress.Module.Track.Modules
                .Where(m => m.Order > progress.Module.Order)
                .OrderBy(m => m.Order)
                .FirstOrDefault() ?? throw new NotFoundException("Module Not Found");

            UserProgress? nextProgress = await _unitOfWork.UserProgresses.GetByUserAndModuleAsync(userId, nextModule.Id, ct) ?? throw new ValidationException("Invalid User Progress");
            nextProgress.IsUnlocked = true;
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
