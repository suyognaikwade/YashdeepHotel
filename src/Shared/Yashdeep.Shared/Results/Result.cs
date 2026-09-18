using System;
using System.Diagnostics.CodeAnalysis;

namespace Yashdeep.Shared.Results;

/// <summary>
/// Standard Result pattern implementation representing success or failure outcomes without exceptions.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot contain an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must contain a valid error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);
}

/// <summary>
/// Generic Result pattern implementation holding a strongly typed success value or failure error.
/// </summary>
/// <typeparam name="TValue">The underlying success value type.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the success value. Throws InvalidOperationException if accessed on a failed result.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    /// <summary>
    /// Tries to get the value safely.
    /// </summary>
    public bool TryGetValue([NotNullWhen(true)] out TValue? value)
    {
        if (IsSuccess && _value != null)
        {
            value = _value;
            return true;
        }

        value = default;
        return false;
    }

    public static Result<TValue> Success(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "Success value cannot be null.");
        }

        return new Result<TValue>(value, true, Error.None);
    }

    public static new Result<TValue> Failure(Error error) => new(default, false, error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);
    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
