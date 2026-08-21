namespace POS.Domain.ValueObjects;

/// <summary>
/// Represents a validated barcode supporting EAN-13, UPC-A, and Code 128 formats.
/// </summary>
public readonly record struct Barcode
{
    public string Value { get; }
    public BarcodeFormat Format { get; }

    private Barcode(string value, BarcodeFormat format)
    {
        Value = value;
        Format = format;
    }

    /// <summary>
    /// Creates a validated Barcode. Validates format constraints and check digits for EAN-13/UPC-A.
    /// </summary>
    public static Result<Barcode> Create(string value, BarcodeFormat format)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Barcode>.Failure(ErrorCode.InvalidBarcodeFormat);

        return format switch
        {
            BarcodeFormat.Ean13 => ValidateEan13(value),
            BarcodeFormat.UpcA => ValidateUpcA(value),
            BarcodeFormat.Code128 => ValidateCode128(value),
            _ => Result<Barcode>.Failure(ErrorCode.InvalidBarcodeFormat)
        };
    }

    private static Result<Barcode> ValidateEan13(string value)
    {
        if (value.Length != 13 || !AllDigits(value))
            return Result<Barcode>.Failure(ErrorCode.InvalidBarcodeFormat);

        if (!IsValidCheckDigit(value))
            return Result<Barcode>.Failure(ErrorCode.InvalidBarcodeCheckDigit);

        return Result<Barcode>.Success(new Barcode(value, BarcodeFormat.Ean13));
    }

    private static Result<Barcode> ValidateUpcA(string value)
    {
        if (value.Length != 12 || !AllDigits(value))
            return Result<Barcode>.Failure(ErrorCode.InvalidBarcodeFormat);

        if (!IsValidCheckDigit(value))
            return Result<Barcode>.Failure(ErrorCode.InvalidBarcodeCheckDigit);

        return Result<Barcode>.Success(new Barcode(value, BarcodeFormat.UpcA));
    }

    private static Result<Barcode> ValidateCode128(string value)
    {
        if (value.Length < 1 || value.Length > 48)
            return Result<Barcode>.Failure(ErrorCode.InvalidBarcodeFormat);

        // Code 128 supports printable ASCII characters (32-126)
        foreach (var c in value)
        {
            if (c < 32 || c > 126)
                return Result<Barcode>.Failure(ErrorCode.InvalidBarcodeFormat);
        }

        return Result<Barcode>.Success(new Barcode(value, BarcodeFormat.Code128));
    }

    /// <summary>
    /// Validates check digit for EAN-13 and UPC-A using the standard algorithm.
    /// Both use the same modulo-10 algorithm with alternating weights of 1 and 3.
    /// </summary>
    private static bool IsValidCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length - 1; i++)
        {
            var digit = digits[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        var checkDigit = (10 - (sum % 10)) % 10;
        return checkDigit == (digits[^1] - '0');
    }

    private static bool AllDigits(string value)
    {
        foreach (var c in value)
        {
            if (c < '0' || c > '9')
                return false;
        }
        return true;
    }

    public override string ToString() => $"{Format}:{Value}";
}
