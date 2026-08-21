using System.Diagnostics.CodeAnalysis;

namespace POS.Domain.Common;

[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Factory methods on Result<T> are the standard pattern for domain result types.")]
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public DomainError? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(DomainError error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(DomainError error) => new(error);

    public static Result<T> Failure(ErrorCode code) => new(DomainError.Create(code));

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(DomainError error) => new(error);
}
