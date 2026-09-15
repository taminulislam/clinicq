using ClinicQ.Domain.Entities;

namespace ClinicQ.Web.Data.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(AppUser user, CancellationToken cancellationToken = default);
}
