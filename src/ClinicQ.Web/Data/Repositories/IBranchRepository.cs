using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Entities;
using ClinicQ.Domain.Scheduling;

namespace ClinicQ.Web.Data.Repositories;

public interface IBranchRepository
{
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Branch branch, CancellationToken cancellationToken = default);
    Task<BranchSlotRule?> GetSlotRuleAsync(int branchId, CancellationToken cancellationToken = default);
    Task UpsertSlotRuleAsync(BranchSlotRule rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeeScheduleItem>> GetFeeScheduleAsync(int branchId, CancellationToken cancellationToken = default);
    Task<int> AddFeeScheduleItemAsync(FeeScheduleItem item, CancellationToken cancellationToken = default);
}
