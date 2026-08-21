using System.Text.RegularExpressions;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Handles user CRUD operations and role management (Req 2.1–2.8, 5.1–5.8).
/// </summary>
public sealed class UserService
{
    private const int UsernameMaxLength = 50;
    private const int EmailMaxLength = 100;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public UserService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Creates a new user with the given details and role assignments.
    /// </summary>
    public async Task<Result<User>> CreateUserAsync(
        string username,
        string email,
        string password,
        string fullName,
        IReadOnlyList<Guid> roleIds,
        Guid performedBy,
        CancellationToken ct = default)
    {
        // Validate username length
        if (string.IsNullOrWhiteSpace(username) || username.Length < 1 || username.Length > UsernameMaxLength)
            return Result<User>.Failure(DomainError.Create(ErrorCode.DuplicateUsername, "field", "username"));

        // Validate email format and length
        var emailValidation = ValidateEmail(email);
        if (!emailValidation.IsSuccess)
            return Result<User>.Failure(emailValidation.Error!.Value);

        // Validate password
        var passwordValidation = AuthenticationService.ValidatePassword(password);
        if (!passwordValidation.IsSuccess)
            return Result<User>.Failure(passwordValidation.Error!.Value);

        // Check duplicate username
        if (await _userRepository.ExistsByUsernameAsync(username, ct))
            return Result<User>.Failure(ErrorCode.DuplicateUsername);

        // Check duplicate email
        if (await _userRepository.ExistsByEmailAsync(email, ct))
            return Result<User>.Failure(ErrorCode.DuplicateEmail);

        var now = _clock.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = _passwordHasher.Hash(password),
            FullName = fullName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = Array.Empty<byte>()
        };

        // Assign roles
        foreach (var roleId in roleIds)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = now,
                AssignedBy = performedBy
            });
        }

        await _userRepository.AddAsync(user, ct);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "CreateUser",
            EntityType: "User",
            EntityId: user.Id,
            RelatedEntityIds: roleIds.ToList(),
            BeforeState: null,
            AfterState: $"{{\"username\":\"{user.Username}\",\"email\":\"{user.Email}\"}}",
            Metadata: $"{{\"performed_by\":\"{performedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Updates user profile fields (username, email, full name).
    /// </summary>
    public async Task<Result<User>> UpdateUserAsync(
        Guid userId,
        string username,
        string email,
        string fullName,
        Guid performedBy,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<User>.Failure(ErrorCode.UnexpectedError);

        // Validate username length
        if (string.IsNullOrWhiteSpace(username) || username.Length < 1 || username.Length > UsernameMaxLength)
            return Result<User>.Failure(DomainError.Create(ErrorCode.DuplicateUsername, "field", "username"));

        // Validate email format and length
        var emailValidation = ValidateEmail(email);
        if (!emailValidation.IsSuccess)
            return Result<User>.Failure(emailValidation.Error!.Value);

        // Check duplicate username (excluding self)
        if (await _userRepository.ExistsByUsernameAsync(username, userId, ct))
            return Result<User>.Failure(ErrorCode.DuplicateUsername);

        // Check duplicate email (excluding self)
        if (await _userRepository.ExistsByEmailAsync(email, userId, ct))
            return Result<User>.Failure(ErrorCode.DuplicateEmail);

        var beforeState = $"{{\"username\":\"{user.Username}\",\"email\":\"{user.Email}\",\"fullName\":\"{user.FullName}\"}}";

        user.Username = username;
        user.Email = email;
        user.FullName = fullName;
        user.UpdatedAt = _clock.UtcNow;

        _userRepository.Update(user);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "UpdateUser",
            EntityType: "User",
            EntityId: user.Id,
            RelatedEntityIds: null,
            BeforeState: beforeState,
            AfterState: $"{{\"username\":\"{user.Username}\",\"email\":\"{user.Email}\",\"fullName\":\"{user.FullName}\"}}",
            Metadata: $"{{\"performed_by\":\"{performedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Updates the roles assigned to a user. Enforces last administrator invariant
    /// and prevents users from removing their own Administrator role.
    /// </summary>
    public async Task<Result<User>> UpdateRolesAsync(
        Guid userId,
        IReadOnlyList<Guid> newRoleIds,
        Guid performedBy,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, ct);
        if (user is null)
            return Result<User>.Failure(ErrorCode.UnexpectedError);

        var currentRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        var targetRoleIds = newRoleIds.ToHashSet();

        var rolesToRemove = currentRoleIds.Except(targetRoleIds).ToList();
        var rolesToAdd = targetRoleIds.Except(currentRoleIds).ToList();

        // Check if removing Administrator role
        if (rolesToRemove.Contains(Role.WellKnown.AdministratorId))
        {
            // Cannot remove own administrator role
            if (userId == performedBy)
                return Result<User>.Failure(ErrorCode.CannotRemoveOwnAdministratorRole);

            // Last administrator protection with UPDLOCK
            var adminCount = await _userRepository.CountAdministratorsWithLockAsync(ct);
            if (adminCount <= 1)
                return Result<User>.Failure(ErrorCode.LastAdministratorRequired);
        }

        var now = _clock.UtcNow;

        // Remove roles
        foreach (var roleId in rolesToRemove)
        {
            var userRole = user.UserRoles.First(ur => ur.RoleId == roleId);
            user.UserRoles.Remove(userRole);
        }

        // Add roles
        foreach (var roleId in rolesToAdd)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = now,
                AssignedBy = performedBy
            });
        }

        user.UpdatedAt = now;
        _userRepository.Update(user);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "UpdateUserRoles",
            EntityType: "User",
            EntityId: user.Id,
            RelatedEntityIds: newRoleIds.ToList(),
            BeforeState: $"{{\"roles\":[{string.Join(",", currentRoleIds.Select(r => $"\"{r}\""))}]}}",
            AfterState: $"{{\"roles\":[{string.Join(",", targetRoleIds.Select(r => $"\"{r}\""))}]}}",
            Metadata: $"{{\"performed_by\":\"{performedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Soft-deletes a user by setting IsActive to false. Enforces last administrator protection.
    /// </summary>
    public async Task<Result<User>> DeactivateUserAsync(
        Guid userId,
        Guid performedBy,
        CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, ct);
        if (user is null)
            return Result<User>.Failure(ErrorCode.UnexpectedError);

        // If user has Administrator role, check last admin protection
        if (user.UserRoles.Any(ur => ur.RoleId == Role.WellKnown.AdministratorId))
        {
            var adminCount = await _userRepository.CountAdministratorsWithLockAsync(ct);
            if (adminCount <= 1)
                return Result<User>.Failure(ErrorCode.LastAdministratorRequired);
        }

        user.IsActive = false;
        user.UpdatedAt = _clock.UtcNow;

        _userRepository.Update(user);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "DeactivateUser",
            EntityType: "User",
            EntityId: user.Id,
            RelatedEntityIds: null,
            BeforeState: "{\"isActive\":true}",
            AfterState: "{\"isActive\":false}",
            Metadata: $"{{\"performed_by\":\"{performedBy}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public async Task<Result<User>> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(userId, ct);
        if (user is null)
            return Result<User>.Failure(ErrorCode.UnexpectedError);

        return Result<User>.Success(user);
    }

    /// <summary>
    /// Gets all users.
    /// </summary>
    public async Task<Result<IReadOnlyList<User>>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _userRepository.GetAllAsync(ct);
        return Result<IReadOnlyList<User>>.Success(users);
    }

    /// <summary>
    /// Validates email format and length.
    /// </summary>
    private static Result<Application.Common.Unit> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailMaxLength)
            return Result<Application.Common.Unit>.Failure(ErrorCode.InvalidEmailFormat);

        if (!EmailRegex.IsMatch(email))
            return Result<Application.Common.Unit>.Failure(ErrorCode.InvalidEmailFormat);

        return Result<Application.Common.Unit>.Success(Application.Common.Unit.Value);
    }
}
