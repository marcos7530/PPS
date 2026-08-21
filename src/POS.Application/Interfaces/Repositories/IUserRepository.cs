using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Checks if another user (excluding <paramref name="excludeUserId"/>) already has the given username.
    /// </summary>
    Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default);

    /// <summary>
    /// Checks if another user (excluding <paramref name="excludeUserId"/>) already has the given email.
    /// </summary>
    Task<bool> ExistsByEmailAsync(string email, Guid excludeUserId, CancellationToken ct = default);

    /// <summary>
    /// Gets a user by ID with their UserRoles collection eagerly loaded.
    /// </summary>
    Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Counts the number of users assigned to the Administrator role using UPDLOCK, HOLDLOCK
    /// to prevent concurrent removal of the last administrator.
    /// </summary>
    Task<int> CountAdministratorsWithLockAsync(CancellationToken ct = default);
}
