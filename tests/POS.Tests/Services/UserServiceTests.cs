using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Application.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Tests.Auth;

namespace POS.Tests.Services;

/// <summary>
/// Unit tests for UserService (Req 2.1–2.8, 5.1–5.8).
/// </summary>
public class UserServiceTests : IDisposable
{
    private readonly FakeUserRepositoryExtended _userRepo = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly UserService _sut;

    private readonly Guid _performerId = Guid.NewGuid();

    public UserServiceTests()
    {
        _sut = new UserService(
            _userRepo,
            _hasher,
            _clock,
            _unitOfWork,
            _auditWriter);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- CreateUserAsync Tests ---

    [Fact]
    public async Task CreateUser_ValidInput_ReturnsSuccess()
    {
        var result = await _sut.CreateUserAsync(
            "newuser", "new@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.True(result.IsSuccess);
        Assert.Equal("newuser", result.Value!.Username);
        Assert.Equal("new@test.com", result.Value.Email);
        Assert.True(result.Value.IsActive);
        Assert.Single(result.Value.UserRoles);
    }

    [Fact]
    public async Task CreateUser_ValidInput_HashesPassword()
    {
        var result = await _sut.CreateUserAsync(
            "newuser", "new@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.True(result.IsSuccess);
        Assert.Equal("hashed_Valid1!pass", result.Value!.PasswordHash);
    }

    [Fact]
    public async Task CreateUser_ValidInput_EnqueuesAuditEntry()
    {
        await _sut.CreateUserAsync(
            "newuser", "new@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("CreateUser", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("   ")] // whitespace
    public async Task CreateUser_InvalidUsername_ReturnsFailure(string username)
    {
        var result = await _sut.CreateUserAsync(
            username, "new@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateUser_UsernameTooLong_ReturnsFailure()
    {
        var longUsername = new string('a', 51);
        var result = await _sut.CreateUserAsync(
            longUsername, "new@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CreateUser_UsernameExactlyMaxLength_ReturnsSuccess()
    {
        var username = new string('a', 50);
        var result = await _sut.CreateUserAsync(
            username, "new@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("invalid")] // no @ sign
    [InlineData("@invalid.com")] // no local part
    [InlineData("invalid@")] // no domain
    [InlineData("invalid@domain")] // no TLD dot
    public async Task CreateUser_InvalidEmail_ReturnsInvalidEmailFormat(string email)
    {
        var result = await _sut.CreateUserAsync(
            "newuser", email, "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidEmailFormat, result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateUser_EmailTooLong_ReturnsInvalidEmailFormat()
    {
        var longEmail = new string('a', 90) + "@test.com"; // 99 chars - but over 100 with domain
        var email = new string('a', 92) + "@test.com"; // 101 chars total
        var result = await _sut.CreateUserAsync(
            "newuser", email, "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidEmailFormat, result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateUser_InvalidPassword_ReturnsPasswordRequirementsNotMet()
    {
        var result = await _sut.CreateUserAsync(
            "newuser", "new@test.com", "weak", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.PasswordRequirementsNotMet, result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsDuplicateUsername()
    {
        _userRepo.Users.Add(CreateUser("existinguser", "existing@test.com"));

        var result = await _sut.CreateUserAsync(
            "existinguser", "new@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.DuplicateUsername, result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_ReturnsDuplicateEmail()
    {
        _userRepo.Users.Add(CreateUser("other", "existing@test.com"));

        var result = await _sut.CreateUserAsync(
            "newuser", "existing@test.com", "Valid1!pass", "New User",
            new[] { Role.WellKnown.CashierId }, _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.DuplicateEmail, result.Error!.Value.Code);
    }

    [Fact]
    public async Task CreateUser_MultipleRoles_AssignsAll()
    {
        var roles = new[] { Role.WellKnown.AdministratorId, Role.WellKnown.ManagerId };
        var result = await _sut.CreateUserAsync(
            "newuser", "new@test.com", "Valid1!pass", "New User",
            roles, _performerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.UserRoles.Count);
    }

    // --- UpdateUserAsync Tests ---

    [Fact]
    public async Task UpdateUser_ValidInput_ReturnsSuccess()
    {
        var user = CreateUser("oldname", "old@test.com");
        _userRepo.Users.Add(user);

        var result = await _sut.UpdateUserAsync(
            user.Id, "newname", "new@test.com", "New Name", _performerId);

        Assert.True(result.IsSuccess);
        Assert.Equal("newname", result.Value!.Username);
        Assert.Equal("new@test.com", result.Value.Email);
        Assert.Equal("New Name", result.Value.FullName);
    }

    [Fact]
    public async Task UpdateUser_DuplicateUsernameOtherUser_ReturnsDuplicateUsername()
    {
        var user1 = CreateUser("user1", "user1@test.com");
        var user2 = CreateUser("user2", "user2@test.com");
        _userRepo.Users.Add(user1);
        _userRepo.Users.Add(user2);

        // Try to rename user2 to user1's username
        var result = await _sut.UpdateUserAsync(
            user2.Id, "user1", "user2@test.com", "User 2", _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.DuplicateUsername, result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateUser_SameUsername_Succeeds()
    {
        var user = CreateUser("myname", "my@test.com");
        _userRepo.Users.Add(user);

        // Keeping the same username should work (excludes self)
        var result = await _sut.UpdateUserAsync(
            user.Id, "myname", "new@test.com", "Updated Name", _performerId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateUser_DuplicateEmailOtherUser_ReturnsDuplicateEmail()
    {
        var user1 = CreateUser("user1", "user1@test.com");
        var user2 = CreateUser("user2", "user2@test.com");
        _userRepo.Users.Add(user1);
        _userRepo.Users.Add(user2);

        var result = await _sut.UpdateUserAsync(
            user2.Id, "user2", "user1@test.com", "User 2", _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.DuplicateEmail, result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateUser_EnqueuesAuditEntry()
    {
        var user = CreateUser("oldname", "old@test.com");
        _userRepo.Users.Add(user);

        await _sut.UpdateUserAsync(user.Id, "newname", "new@test.com", "New Name", _performerId);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("UpdateUser", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    // --- UpdateRolesAsync Tests ---

    [Fact]
    public async Task UpdateRoles_AddRole_Succeeds()
    {
        var user = CreateUserWithRoles("admin", new[] { Role.WellKnown.CashierId });
        _userRepo.Users.Add(user);

        var newRoles = new[] { Role.WellKnown.CashierId, Role.WellKnown.ManagerId };
        var result = await _sut.UpdateRolesAsync(user.Id, newRoles, _performerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.UserRoles.Count);
    }

    [Fact]
    public async Task UpdateRoles_RemoveNonAdminRole_Succeeds()
    {
        var user = CreateUserWithRoles("user", new[] { Role.WellKnown.CashierId, Role.WellKnown.ManagerId });
        _userRepo.Users.Add(user);

        var newRoles = new[] { Role.WellKnown.CashierId };
        var result = await _sut.UpdateRolesAsync(user.Id, newRoles, _performerId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.UserRoles);
    }

    [Fact]
    public async Task UpdateRoles_RemoveOwnAdminRole_ReturnsCannotRemoveOwnAdministratorRole()
    {
        var userId = Guid.NewGuid();
        var user = CreateUserWithRoles("admin", new[] { Role.WellKnown.AdministratorId, Role.WellKnown.ManagerId });
        user.Id = userId;
        // Fix UserRole references
        foreach (var ur in user.UserRoles)
            ur.UserId = userId;
        _userRepo.Users.Add(user);
        _userRepo.AdminCount = 2; // There are other admins

        // performedBy == userId: trying to remove own admin role
        var newRoles = new[] { Role.WellKnown.ManagerId };
        var result = await _sut.UpdateRolesAsync(userId, newRoles, userId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.CannotRemoveOwnAdministratorRole, result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateRoles_RemoveLastAdmin_ReturnsLastAdministratorRequired()
    {
        var userId = Guid.NewGuid();
        var user = CreateUserWithRoles("admin", new[] { Role.WellKnown.AdministratorId });
        user.Id = userId;
        foreach (var ur in user.UserRoles)
            ur.UserId = userId;
        _userRepo.Users.Add(user);
        _userRepo.AdminCount = 1; // Only one admin

        var otherPerformer = Guid.NewGuid();
        var newRoles = new[] { Role.WellKnown.CashierId };
        var result = await _sut.UpdateRolesAsync(userId, newRoles, otherPerformer);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.LastAdministratorRequired, result.Error!.Value.Code);
    }

    [Fact]
    public async Task UpdateRoles_RemoveAdminWithMultipleAdmins_Succeeds()
    {
        var userId = Guid.NewGuid();
        var user = CreateUserWithRoles("admin", new[] { Role.WellKnown.AdministratorId });
        user.Id = userId;
        foreach (var ur in user.UserRoles)
            ur.UserId = userId;
        _userRepo.Users.Add(user);
        _userRepo.AdminCount = 3; // Multiple admins exist

        var otherPerformer = Guid.NewGuid();
        var newRoles = new[] { Role.WellKnown.CashierId };
        var result = await _sut.UpdateRolesAsync(userId, newRoles, otherPerformer);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateRoles_EnqueuesAuditEntry()
    {
        var user = CreateUserWithRoles("user", new[] { Role.WellKnown.CashierId });
        _userRepo.Users.Add(user);

        var newRoles = new[] { Role.WellKnown.CashierId, Role.WellKnown.ManagerId };
        await _sut.UpdateRolesAsync(user.Id, newRoles, _performerId);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("UpdateUserRoles", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    // --- DeactivateUserAsync Tests ---

    [Fact]
    public async Task DeactivateUser_NonAdmin_Succeeds()
    {
        var user = CreateUserWithRoles("cashier", new[] { Role.WellKnown.CashierId });
        _userRepo.Users.Add(user);

        var result = await _sut.DeactivateUserAsync(user.Id, _performerId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }

    [Fact]
    public async Task DeactivateUser_LastAdmin_ReturnsLastAdministratorRequired()
    {
        var user = CreateUserWithRoles("admin", new[] { Role.WellKnown.AdministratorId });
        _userRepo.Users.Add(user);
        _userRepo.AdminCount = 1;

        var result = await _sut.DeactivateUserAsync(user.Id, _performerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.LastAdministratorRequired, result.Error!.Value.Code);
    }

    [Fact]
    public async Task DeactivateUser_AdminWithOthersExisting_Succeeds()
    {
        var user = CreateUserWithRoles("admin", new[] { Role.WellKnown.AdministratorId });
        _userRepo.Users.Add(user);
        _userRepo.AdminCount = 2;

        var result = await _sut.DeactivateUserAsync(user.Id, _performerId);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
    }

    [Fact]
    public async Task DeactivateUser_EnqueuesAuditEntry()
    {
        var user = CreateUserWithRoles("user", new[] { Role.WellKnown.CashierId });
        _userRepo.Users.Add(user);

        await _sut.DeactivateUserAsync(user.Id, _performerId);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("DeactivateUser", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    // --- GetUserAsync / GetUsersAsync Tests ---

    [Fact]
    public async Task GetUser_ExistingUser_ReturnsSuccess()
    {
        var user = CreateUser("testuser", "test@test.com");
        _userRepo.Users.Add(user);

        var result = await _sut.GetUserAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("testuser", result.Value!.Username);
    }

    [Fact]
    public async Task GetUser_NonExistingUser_ReturnsFailure()
    {
        var result = await _sut.GetUserAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        _userRepo.Users.Add(CreateUser("user1", "user1@test.com"));
        _userRepo.Users.Add(CreateUser("user2", "user2@test.com"));

        var result = await _sut.GetUsersAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    // --- Helpers ---

    private static User CreateUser(string username, string email) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        Email = email,
        PasswordHash = $"hashed_password",
        FullName = "Test User",
        IsActive = true,
        FailedLoginCount = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        RowVersion = Array.Empty<byte>()
    };

    private static User CreateUserWithRoles(string username, Guid[] roleIds)
    {
        var user = CreateUser(username, $"{username}@test.com");
        foreach (var roleId in roleIds)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                AssignedAt = DateTimeOffset.UtcNow,
                AssignedBy = Guid.NewGuid()
            });
        }
        return user;
    }
}

// --- Extended Fakes for UserService tests ---

internal sealed class FakeUserRepositoryExtended : IUserRepository
{
    public List<User> Users { get; } = new();
    public int AdminCount { get; set; }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Id != excludeUserId &&
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByEmailAsync(string email, Guid excludeUserId, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Id != excludeUserId &&
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

    public Task<int> CountAdministratorsWithLockAsync(CancellationToken ct = default)
        => Task.FromResult(AdminCount);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<User>>(Users.AsReadOnly());

    public Task AddAsync(User entity, CancellationToken ct = default)
    {
        Users.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(User entity) { /* no-op for fake */ }
    public void Remove(User entity) => Users.Remove(entity);
}
