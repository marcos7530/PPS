using System.Text.Json;
using System.Text.RegularExpressions;
using POS.Application.Commands;
using POS.Application.Common;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Application.Views;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Implements customer management operations including CRUD, search,
/// statistics, and soft deactivation (Req 13.1-13.14).
/// </summary>
public sealed class CustomerService : ICustomerService
{
    private const int NameMinLength = 1;
    private const int NameMaxLength = 100;
    private const int EmailMaxLength = 100;
    private const int PhoneMinLength = 7;
    private const int PhoneMaxLength = 20;
    private const int NotesMaxLength = 500;

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ICustomerRepository _customerRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public CustomerService(
        ICustomerRepository customerRepository,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _customerRepository = customerRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <inheritdoc />
    public async Task<Result<Customer>> CreateAsync(CreateCustomerCommand cmd, CancellationToken ct = default)
    {
        // Validate name (1-100 chars)
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length < NameMinLength || cmd.Name.Length > NameMaxLength)
            return Result<Customer>.Failure(ErrorCode.UnexpectedError);

        // Validate email if provided (optional, valid format, max 100 chars)
        if (cmd.Email is not null)
        {
            var emailValidation = ValidateEmail(cmd.Email);
            if (!emailValidation.IsSuccess)
                return Result<Customer>.Failure(emailValidation.Error!.Value);
        }

        // Validate phone if provided (7-20 digits with optional formatting)
        if (cmd.Phone is not null)
        {
            var phoneValidation = ValidatePhone(cmd.Phone);
            if (!phoneValidation.IsSuccess)
                return Result<Customer>.Failure(phoneValidation.Error!.Value);
        }

        // Validate notes (optional, max 500 chars)
        if (cmd.Notes is not null && cmd.Notes.Length > NotesMaxLength)
            return Result<Customer>.Failure(ErrorCode.UnexpectedError);

        // Req 13.2: Check duplicate email
        if (cmd.Email is not null && await _customerRepository.ExistsByEmailAsync(cmd.Email, ct))
            return Result<Customer>.Failure(ErrorCode.CustomerEmailAlreadyRegistered);

        // Req 13.3: Check duplicate phone (warn, require confirmation)
        var phoneNormalized = cmd.Phone is not null ? NormalizePhone(cmd.Phone) : null;
        if (phoneNormalized is not null && !cmd.ConfirmPhoneDuplicate)
        {
            if (await _customerRepository.ExistsByPhoneNormalizedAsync(phoneNormalized, ct))
            {
                // Return a specific error indicating phone duplicate - caller should confirm
                return Result<Customer>.Failure(ErrorCode.UnexpectedError);
            }
        }

        // Req 13.4: Generate UUID v4, record creation timestamp
        var now = _clock.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name.Trim(),
            Email = cmd.Email?.Trim(),
            Phone = cmd.Phone?.Trim(),
            PhoneNormalized = phoneNormalized,
            Notes = cmd.Notes?.Trim(),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = cmd.PerformedBy
        };

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _customerRepository.AddAsync(customer, ct);

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "CreateCustomer",
                EntityType: "Customer",
                EntityId: customer.Id,
                RelatedEntityIds: null,
                BeforeState: null,
                AfterState: JsonSerializer.Serialize(new
                {
                    name = customer.Name,
                    email = customer.Email,
                    phone = customer.Phone,
                    notes = customer.Notes
                }),
                Metadata: JsonSerializer.Serialize(new
                {
                    performed_by = cmd.PerformedBy
                })));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Customer>.Success(customer);
    }

    /// <inheritdoc />
    public async Task<Result<Customer>> UpdateAsync(UpdateCustomerCommand cmd, CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(cmd.CustomerId, ct);
        if (customer is null)
            return Result<Customer>.Failure(ErrorCode.UnexpectedError);

        // Validate name (1-100 chars)
        if (string.IsNullOrWhiteSpace(cmd.Name) || cmd.Name.Length < NameMinLength || cmd.Name.Length > NameMaxLength)
            return Result<Customer>.Failure(ErrorCode.UnexpectedError);

        // Validate email if provided
        if (cmd.Email is not null)
        {
            var emailValidation = ValidateEmail(cmd.Email);
            if (!emailValidation.IsSuccess)
                return Result<Customer>.Failure(emailValidation.Error!.Value);
        }

        // Validate phone if provided
        if (cmd.Phone is not null)
        {
            var phoneValidation = ValidatePhone(cmd.Phone);
            if (!phoneValidation.IsSuccess)
                return Result<Customer>.Failure(phoneValidation.Error!.Value);
        }

        // Validate notes
        if (cmd.Notes is not null && cmd.Notes.Length > NotesMaxLength)
            return Result<Customer>.Failure(ErrorCode.UnexpectedError);

        // Req 13.2: Check duplicate email (excluding self)
        if (cmd.Email is not null && await _customerRepository.ExistsByEmailAsync(cmd.Email, cmd.CustomerId, ct))
            return Result<Customer>.Failure(ErrorCode.CustomerEmailAlreadyRegistered);

        // Req 13.3: Check duplicate phone (excluding self)
        var phoneNormalized = cmd.Phone is not null ? NormalizePhone(cmd.Phone) : null;
        if (phoneNormalized is not null && !cmd.ConfirmPhoneDuplicate)
        {
            if (await _customerRepository.ExistsByPhoneNormalizedAsync(phoneNormalized, cmd.CustomerId, ct))
            {
                return Result<Customer>.Failure(ErrorCode.UnexpectedError);
            }
        }

        var beforeState = JsonSerializer.Serialize(new
        {
            name = customer.Name,
            email = customer.Email,
            phone = customer.Phone,
            notes = customer.Notes
        });

        customer.Name = cmd.Name.Trim();
        customer.Email = cmd.Email?.Trim();
        customer.Phone = cmd.Phone?.Trim();
        customer.PhoneNormalized = phoneNormalized;
        customer.Notes = cmd.Notes?.Trim();

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _customerRepository.Update(customer);

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "UpdateCustomer",
                EntityType: "Customer",
                EntityId: customer.Id,
                RelatedEntityIds: null,
                BeforeState: beforeState,
                AfterState: JsonSerializer.Serialize(new
                {
                    name = customer.Name,
                    email = customer.Email,
                    phone = customer.Phone,
                    notes = customer.Notes
                }),
                Metadata: JsonSerializer.Serialize(new
                {
                    performed_by = cmd.PerformedBy
                })));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Customer>.Success(customer);
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeactivateAsync(Guid customerId, Guid performedBy, CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result<Unit>.Failure(ErrorCode.UnexpectedError);

        if (!customer.IsActive)
            return Result<Unit>.Success(Unit.Value);

        customer.IsActive = false;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _customerRepository.Update(customer);

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "DeactivateCustomer",
                EntityType: "Customer",
                EntityId: customer.Id,
                RelatedEntityIds: null,
                BeforeState: "{\"isActive\":true}",
                AfterState: "{\"isActive\":false}",
                Metadata: JsonSerializer.Serialize(new
                {
                    performed_by = performedBy
                })));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Unit>.Success(Unit.Value);
    }

    /// <inheritdoc />
    public async Task<Result<CustomerView>> GetByIdAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result<CustomerView>.Failure(ErrorCode.UnexpectedError);

        var view = new CustomerView(
            Id: customer.Id,
            Name: customer.Name,
            Email: customer.Email,
            Phone: customer.Phone,
            Notes: customer.Notes,
            IsActive: customer.IsActive,
            CreatedAt: customer.CreatedAt,
            CreatedBy: customer.CreatedBy);

        return Result<CustomerView>.Success(view);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CustomerSearchResult>>> SearchAsync(
        string? name,
        string? email,
        string? phone,
        Guid? customerId,
        bool includeInactive = false,
        CancellationToken ct = default)
    {
        // Search by customer ID (exact match)
        if (customerId.HasValue)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId.Value, ct);
            if (customer is null || (!includeInactive && !customer.IsActive))
                return Result<IReadOnlyList<CustomerSearchResult>>.Success(Array.Empty<CustomerSearchResult>());

            var result = new CustomerSearchResult(
                customer.Id, customer.Name, customer.Email, customer.Phone, customer.IsActive);
            return Result<IReadOnlyList<CustomerSearchResult>>.Success(new[] { result });
        }

        // Search by email (exact match)
        if (!string.IsNullOrWhiteSpace(email))
        {
            var customer = await _customerRepository.GetByEmailAsync(email.Trim(), ct);
            if (customer is null || (!includeInactive && !customer.IsActive))
                return Result<IReadOnlyList<CustomerSearchResult>>.Success(Array.Empty<CustomerSearchResult>());

            var result = new CustomerSearchResult(
                customer.Id, customer.Name, customer.Email, customer.Phone, customer.IsActive);
            return Result<IReadOnlyList<CustomerSearchResult>>.Success(new[] { result });
        }

        // Search by name (partial, CI/AI via collation)
        if (!string.IsNullOrWhiteSpace(name))
        {
            var customers = await _customerRepository.SearchByNameAsync(name.Trim(), includeInactive, ct);
            var results = customers.Select(c =>
                new CustomerSearchResult(c.Id, c.Name, c.Email, c.Phone, c.IsActive)).ToList();
            return Result<IReadOnlyList<CustomerSearchResult>>.Success(results);
        }

        // Search by phone (partial match)
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var customers = await _customerRepository.SearchByPhoneAsync(phone.Trim(), includeInactive, ct);
            var results = customers.Select(c =>
                new CustomerSearchResult(c.Id, c.Name, c.Email, c.Phone, c.IsActive)).ToList();
            return Result<IReadOnlyList<CustomerSearchResult>>.Success(results);
        }

        // No criteria: return empty
        return Result<IReadOnlyList<CustomerSearchResult>>.Success(Array.Empty<CustomerSearchResult>());
    }

    /// <inheritdoc />
    public async Task<Result<CustomerStatistics>> GetStatisticsAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result<CustomerStatistics>.Failure(ErrorCode.UnexpectedError);

        var totalTransactions = await _customerRepository.GetTransactionCountAsync(customerId, ct);
        var totalAmount = await _customerRepository.GetTotalPurchaseAmountAsync(customerId, ct);
        var lastPurchase = await _customerRepository.GetLastPurchaseDateAsync(customerId, ct);

        var stats = new CustomerStatistics(
            CustomerId: customerId,
            TotalTransactions: totalTransactions,
            TotalPurchaseAmount: Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero),
            LastPurchaseDate: lastPurchase);

        return Result<CustomerStatistics>.Success(stats);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CustomerPurchaseHistory>>> GetPurchaseHistoryAsync(
        Guid customerId, CancellationToken ct = default)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId, ct);
        if (customer is null)
            return Result<IReadOnlyList<CustomerPurchaseHistory>>.Failure(ErrorCode.UnexpectedError);

        var history = await _customerRepository.GetPurchaseHistoryAsync(customerId, ct);
        return Result<IReadOnlyList<CustomerPurchaseHistory>>.Success(history);
    }

    /// <inheritdoc />
    public async Task<Result<string?>> CheckPhoneDuplicateAsync(
        string phone, Guid? excludeCustomerId = null, CancellationToken ct = default)
    {
        var phoneNormalized = NormalizePhone(phone);

        string? existingName;
        if (excludeCustomerId.HasValue)
        {
            if (!await _customerRepository.ExistsByPhoneNormalizedAsync(phoneNormalized, excludeCustomerId.Value, ct))
                return Result<string?>.Success(null);

            existingName = await _customerRepository.GetNameByPhoneNormalizedAsync(phoneNormalized, excludeCustomerId.Value, ct);
        }
        else
        {
            if (!await _customerRepository.ExistsByPhoneNormalizedAsync(phoneNormalized, ct))
                return Result<string?>.Success(null);

            existingName = await _customerRepository.GetNameByPhoneNormalizedAsync(phoneNormalized, ct);
        }

        return Result<string?>.Success(existingName);
    }

    #region Private Helpers

    /// <summary>
    /// Normalizes a phone number by stripping all non-digit characters.
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    /// Validates email format and length.
    /// </summary>
    private static Result<Unit> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailMaxLength)
            return Result<Unit>.Failure(ErrorCode.InvalidEmailFormat);

        if (!EmailRegex.IsMatch(email))
            return Result<Unit>.Failure(ErrorCode.InvalidEmailFormat);

        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Validates phone format: 7-20 digits (after stripping formatting characters).
    /// </summary>
    private static Result<Unit> ValidatePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return Result<Unit>.Failure(ErrorCode.UnexpectedError);

        // Phone can have formatting characters but must have 7-20 digits
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < PhoneMinLength || digitsOnly.Length > PhoneMaxLength)
            return Result<Unit>.Failure(ErrorCode.UnexpectedError);

        return Result<Unit>.Success(Unit.Value);
    }

    #endregion
}
