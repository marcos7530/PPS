using System.Globalization;
using System.Text.RegularExpressions;
using POS.Domain.Common;

namespace POS.Presentation.Localization;

/// <summary>
/// Provides localized error messages for <see cref="ErrorCode"/> values.
/// Uses static dictionaries for en-US and es-AR cultures.
/// </summary>
public static partial class ErrorMessageLocalizer
{
    private static readonly Dictionary<ErrorCode, string> EnUsMessages = new()
    {
        [ErrorCode.AuditWriteFailed] = "Operation rejected: audit recording failed",
        [ErrorCode.DuplicateUsername] = "Username already exists",
        [ErrorCode.DuplicateEmail] = "Email address already exists",
        [ErrorCode.InvalidEmailFormat] = "Invalid email format",
        [ErrorCode.InsufficientPermissions] = "Insufficient permissions",
        [ErrorCode.LastAdministratorRequired] = "Cannot remove last administrator",
        [ErrorCode.CannotRemoveOwnAdministratorRole] = "Cannot remove own administrator role",
        [ErrorCode.InvalidCredentials] = "Invalid credentials",
        [ErrorCode.AccountLocked] = "Account locked due to multiple failed attempts. Try again later",
        [ErrorCode.SessionExpired] = "Session expired",
        [ErrorCode.InvalidOrExpiredResetToken] = "Invalid or expired reset token",
        [ErrorCode.PasswordRequirementsNotMet] = "Password must be 8-128 characters with uppercase, lowercase, digit, and special character",
        [ErrorCode.ResetEmailSendFailed] = "Unable to send recovery email. Please try again",
        [ErrorCode.InvalidProductIdentifier] = "Invalid product identifier",
        [ErrorCode.InsufficientInventory] = "Insufficient inventory: {availableQuantity} available",
        [ErrorCode.NoActiveShiftForCashTransaction] = "No active shift. Open a shift before processing cash transactions",
        [ErrorCode.VoucherNotFound] = "Store credit voucher not found",
        [ErrorCode.VoucherAlreadyUsed] = "Store credit voucher has already been used",
        [ErrorCode.VoucherExpired] = "Store credit voucher expired on {expirationDate}",
        [ErrorCode.CustomerHasNoStoreCredit] = "Customer has no available store credit balance",
        [ErrorCode.AdditionalPaymentRequired] = "Store credit insufficient. Additional payment required",
        [ErrorCode.InsufficientPayment] = "Amount received is less than the total amount due",
        [ErrorCode.TransactionInventoryUpdateFailed] = "Failed to update inventory during transaction",
        [ErrorCode.DuplicateSku] = "SKU already exists",
        [ErrorCode.ProductNoLongerAvailable] = "Product is no longer available",
        [ErrorCode.InvalidOrExpiredTransaction] = "Transaction not found or cannot be returned",
        [ErrorCode.ReturnQuantityExceedsOriginal] = "Return quantity exceeds available quantity",
        [ErrorCode.NoActiveShiftForCashRefund] = "No active shift. Open a shift before processing cash refunds",
        [ErrorCode.ManagerAuthorizationRequiredForRefund] = "Manager authorization required for this refund",
        [ErrorCode.ReturnInventoryUpdateFailed] = "Failed to update inventory during return",
        [ErrorCode.CashDrawerHasActiveShift] = "Cash drawer already has an active shift",
        [ErrorCode.UserHasActiveShift] = "User already has an active shift",
        [ErrorCode.VarianceExplanationRequired] = "Variance exceeds $10.00. Please provide an explanation",
        [ErrorCode.CustomerEmailAlreadyRegistered] = "Email address already registered to another customer",
        [ErrorCode.CategoryNameAlreadyExistsAtLevel] = "Category name already exists at this level",
        [ErrorCode.InvalidParentCategory] = "Invalid parent category",
        [ErrorCode.MaxCategoryDepthExceeded] = "Maximum category depth of 5 levels exceeded",
        [ErrorCode.CircularCategoryReference] = "Circular category reference not allowed",
        [ErrorCode.AdministratorRoleRequiredForGlobalMargin] = "Administrator role required to modify global profit margin",
        [ErrorCode.InvalidProfitMargin] = "Profit margin must be between 0.00% and 1000.00% with maximum 2 decimal places",
        [ErrorCode.InvalidCostPrice] = "Cost price must be between 0.01 and 999999.99 with maximum 2 decimal places",
        [ErrorCode.PriceRecalculationFailed] = "Price recalculation failed, no prices were changed",
        [ErrorCode.UnsupportedImageFormat] = "Unsupported image format. Allowed formats: JPEG, PNG, WebP",
        [ErrorCode.ImageTooLarge] = "Image file exceeds maximum size of 5 MB",
        [ErrorCode.ImageDimensionsExceeded] = "Image dimensions exceed maximum of 4000x4000 pixels",
        [ErrorCode.ImageCorrupted] = "Image file is corrupted or unreadable",
        [ErrorCode.ImageUploadFailed] = "Image upload failed, please try again",
        [ErrorCode.InsufficientPermissionsForProductImages] = "Insufficient permissions to manage product images",
        [ErrorCode.NoCustomerEmailAvailable] = "No customer email available for this transaction",
        [ErrorCode.ReceiptEmailSendFailed] = "Unable to send receipt email, please retry or download the PDF",
        [ErrorCode.ReceiptNotFound] = "Receipt not found for the provided identifier",
        [ErrorCode.ReceiptPrintFailed] = "Receipt printing failed. Retry printing, download PDF, or continue without receipt",
        [ErrorCode.ReceiptFooterTooLong] = "Receipt footer text cannot exceed 200 characters",
        [ErrorCode.InvalidBarcodeFormat] = "Invalid barcode format. Allowed formats: EAN-13, UPC-A, Code 128",
        [ErrorCode.InvalidBarcodeCheckDigit] = "Invalid barcode check digit",
        [ErrorCode.BarcodeAlreadyAssigned] = "Barcode already assigned to product {productName}",
        [ErrorCode.BarcodeNotFound] = "Barcode not found",
        [ErrorCode.LineItemQuantityExceedsMaximum] = "Line item quantity cannot exceed 9999",
        [ErrorCode.InvalidDiscountPercentage] = "Discount percentage must be between 0% and 100%",
        [ErrorCode.DiscountAmountExceedsBase] = "Discount amount exceeds base amount",
        [ErrorCode.DiscountWouldResultInNegativeTotal] = "Discount would result in negative total",
        [ErrorCode.DiscountExceedsLimit] = "Discount exceeds authorized limit for your role",
        [ErrorCode.DiscountReasonRequired] = "Discount reason is required",
        [ErrorCode.ManagerAuthorizationRequiredToVoid] = "Manager or administrator authorization required to void transactions",
        [ErrorCode.TransactionBelongsToClosedOperatingDay] = "Transaction belongs to a closed operating day and cannot be voided",
        [ErrorCode.ShiftAlreadyClosed] = "Shift already closed",
        [ErrorCode.VoidReasonAndNotesRequired] = "Void reason and notes are required",
        [ErrorCode.TransactionAlreadyVoided] = "Transaction has already been voided",
        [ErrorCode.TransactionVoidedCannotBeReturned] = "Voided transactions cannot be returned",
        [ErrorCode.TransactionHasReturns] = "Transaction has existing returns and cannot be voided",
        [ErrorCode.VoidInventoryRestoreFailed] = "Failed to restore inventory during void",
        [ErrorCode.DateRangeExceedsLimit] = "Date range cannot exceed 366 days",
        [ErrorCode.ReportGenerationFailed] = "Report generation failed, please try again or contact support",
        [ErrorCode.NoDataFound] = "No data found for specified criteria",
        [ErrorCode.ConcurrencyConflict] = "Data has been modified by another user. Please refresh and try again",
        [ErrorCode.UnexpectedError] = "An unexpected error occurred. Please try again",
    };

    private static readonly Dictionary<ErrorCode, string> EsArMessages = new()
    {
        [ErrorCode.AuditWriteFailed] = "Operación rechazada: falló el registro de auditoría",
        [ErrorCode.DuplicateUsername] = "El nombre de usuario ya existe",
        [ErrorCode.DuplicateEmail] = "La dirección de correo electrónico ya existe",
        [ErrorCode.InvalidEmailFormat] = "Formato de correo electrónico inválido",
        [ErrorCode.InsufficientPermissions] = "Permisos insuficientes",
        [ErrorCode.LastAdministratorRequired] = "No se puede eliminar el último administrador",
        [ErrorCode.CannotRemoveOwnAdministratorRole] = "No se puede remover el propio rol de administrador",
        [ErrorCode.InvalidCredentials] = "Credenciales inválidas",
        [ErrorCode.AccountLocked] = "Cuenta bloqueada por múltiples intentos fallidos. Intente más tarde",
        [ErrorCode.SessionExpired] = "Sesión expirada",
        [ErrorCode.InvalidOrExpiredResetToken] = "Token de recuperación inválido o expirado",
        [ErrorCode.PasswordRequirementsNotMet] = "La contraseña debe tener entre 8 y 128 caracteres con mayúscula, minúscula, dígito y carácter especial",
        [ErrorCode.ResetEmailSendFailed] = "No se pudo enviar el correo de recuperación. Intente nuevamente",
        [ErrorCode.InvalidProductIdentifier] = "Identificador de producto inválido",
        [ErrorCode.InsufficientInventory] = "Inventario insuficiente: {availableQuantity} disponible(s)",
        [ErrorCode.NoActiveShiftForCashTransaction] = "No hay turno activo. Abra un turno antes de procesar transacciones en efectivo",
        [ErrorCode.VoucherNotFound] = "Voucher de crédito no encontrado",
        [ErrorCode.VoucherAlreadyUsed] = "El voucher de crédito ya fue utilizado",
        [ErrorCode.VoucherExpired] = "El voucher de crédito expiró el {expirationDate}",
        [ErrorCode.CustomerHasNoStoreCredit] = "El cliente no tiene saldo de crédito disponible",
        [ErrorCode.AdditionalPaymentRequired] = "Crédito insuficiente. Se requiere un pago adicional",
        [ErrorCode.InsufficientPayment] = "El monto recibido es menor al total adeudado",
        [ErrorCode.TransactionInventoryUpdateFailed] = "Error al actualizar el inventario durante la transacción",
        [ErrorCode.DuplicateSku] = "El SKU ya existe",
        [ErrorCode.ProductNoLongerAvailable] = "El producto ya no está disponible",
        [ErrorCode.InvalidOrExpiredTransaction] = "Transacción no encontrada o no puede ser devuelta",
        [ErrorCode.ReturnQuantityExceedsOriginal] = "La cantidad a devolver excede la cantidad disponible",
        [ErrorCode.NoActiveShiftForCashRefund] = "No hay turno activo. Abra un turno antes de procesar reembolsos en efectivo",
        [ErrorCode.ManagerAuthorizationRequiredForRefund] = "Se requiere autorización de gerente para este reembolso",
        [ErrorCode.ReturnInventoryUpdateFailed] = "Error al actualizar el inventario durante la devolución",
        [ErrorCode.CashDrawerHasActiveShift] = "La caja ya tiene un turno activo",
        [ErrorCode.UserHasActiveShift] = "El usuario ya tiene un turno activo",
        [ErrorCode.VarianceExplanationRequired] = "La diferencia excede $10,00. Por favor proporcione una explicación",
        [ErrorCode.CustomerEmailAlreadyRegistered] = "La dirección de correo ya está registrada para otro cliente",
        [ErrorCode.CategoryNameAlreadyExistsAtLevel] = "El nombre de categoría ya existe en este nivel",
        [ErrorCode.InvalidParentCategory] = "Categoría padre inválida",
        [ErrorCode.MaxCategoryDepthExceeded] = "Se excedió la profundidad máxima de 5 niveles de categoría",
        [ErrorCode.CircularCategoryReference] = "No se permite referencia circular de categoría",
        [ErrorCode.AdministratorRoleRequiredForGlobalMargin] = "Se requiere rol de administrador para modificar el margen de ganancia global",
        [ErrorCode.InvalidProfitMargin] = "El margen de ganancia debe estar entre 0,00% y 1000,00% con máximo 2 decimales",
        [ErrorCode.InvalidCostPrice] = "El precio de costo debe estar entre 0,01 y 999999,99 con máximo 2 decimales",
        [ErrorCode.PriceRecalculationFailed] = "Falló el recálculo de precios, no se modificó ningún precio",
        [ErrorCode.UnsupportedImageFormat] = "Formato de imagen no soportado. Formatos permitidos: JPEG, PNG, WebP",
        [ErrorCode.ImageTooLarge] = "El archivo de imagen excede el tamaño máximo de 5 MB",
        [ErrorCode.ImageDimensionsExceeded] = "Las dimensiones de la imagen exceden el máximo de 4000x4000 píxeles",
        [ErrorCode.ImageCorrupted] = "El archivo de imagen está corrupto o no se puede leer",
        [ErrorCode.ImageUploadFailed] = "La carga de imagen falló, intente nuevamente",
        [ErrorCode.InsufficientPermissionsForProductImages] = "Permisos insuficientes para gestionar imágenes de productos",
        [ErrorCode.NoCustomerEmailAvailable] = "No hay correo de cliente disponible para esta transacción",
        [ErrorCode.ReceiptEmailSendFailed] = "No se pudo enviar el comprobante por correo, reintente o descargue el PDF",
        [ErrorCode.ReceiptNotFound] = "Comprobante no encontrado para el identificador proporcionado",
        [ErrorCode.ReceiptPrintFailed] = "Falló la impresión del comprobante. Reintente, descargue el PDF o continúe sin comprobante",
        [ErrorCode.ReceiptFooterTooLong] = "El texto de pie de comprobante no puede exceder 200 caracteres",
        [ErrorCode.InvalidBarcodeFormat] = "Formato de código de barras inválido. Formatos permitidos: EAN-13, UPC-A, Code 128",
        [ErrorCode.InvalidBarcodeCheckDigit] = "Dígito de verificación de código de barras inválido",
        [ErrorCode.BarcodeAlreadyAssigned] = "Código de barras ya asignado al producto {productName}",
        [ErrorCode.BarcodeNotFound] = "Código de barras no encontrado",
        [ErrorCode.LineItemQuantityExceedsMaximum] = "La cantidad por línea no puede exceder 9999",
        [ErrorCode.InvalidDiscountPercentage] = "El porcentaje de descuento debe estar entre 0% y 100%",
        [ErrorCode.DiscountAmountExceedsBase] = "El monto de descuento excede el monto base",
        [ErrorCode.DiscountWouldResultInNegativeTotal] = "El descuento resultaría en un total negativo",
        [ErrorCode.DiscountExceedsLimit] = "El descuento excede el límite autorizado para su rol",
        [ErrorCode.DiscountReasonRequired] = "El motivo del descuento es obligatorio",
        [ErrorCode.ManagerAuthorizationRequiredToVoid] = "Se requiere autorización de gerente o administrador para anular transacciones",
        [ErrorCode.TransactionBelongsToClosedOperatingDay] = "La transacción pertenece a un día operativo cerrado y no puede ser anulada",
        [ErrorCode.ShiftAlreadyClosed] = "El turno ya está cerrado",
        [ErrorCode.VoidReasonAndNotesRequired] = "El motivo y las notas de anulación son obligatorios",
        [ErrorCode.TransactionAlreadyVoided] = "La transacción ya fue anulada",
        [ErrorCode.TransactionVoidedCannotBeReturned] = "Las transacciones anuladas no pueden ser devueltas",
        [ErrorCode.TransactionHasReturns] = "La transacción tiene devoluciones existentes y no puede ser anulada",
        [ErrorCode.VoidInventoryRestoreFailed] = "Error al restaurar el inventario durante la anulación",
        [ErrorCode.DateRangeExceedsLimit] = "El rango de fechas no puede exceder 366 días",
        [ErrorCode.ReportGenerationFailed] = "Falló la generación del informe, intente nuevamente o contacte soporte",
        [ErrorCode.NoDataFound] = "No se encontraron datos para los criterios especificados",
        [ErrorCode.ConcurrencyConflict] = "Los datos fueron modificados por otro usuario. Actualice y vuelva a intentar",
        [ErrorCode.UnexpectedError] = "Ocurrió un error inesperado. Intente nuevamente",
    };

    private static readonly Dictionary<string, Dictionary<ErrorCode, string>> MessagesByCulture = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-US"] = EnUsMessages,
        ["es-AR"] = EsArMessages,
    };

    /// <summary>
    /// Formats the localized error message for the given <paramref name="code"/>,
    /// replacing placeholders <c>{key}</c> with values from <paramref name="args"/>.
    /// Money values (decimal) are formatted with culture-aware <c>ToString("N2", culture)</c>.
    /// </summary>
    public static string Format(ErrorCode code, IReadOnlyDictionary<string, object?>? args = null, string culture = "en-US")
    {
        var cultureInfo = CultureInfo.GetCultureInfo(culture);
        var messages = MessagesByCulture.GetValueOrDefault(culture) ?? EnUsMessages;

        if (!messages.TryGetValue(code, out var template))
            return code.ToString();

        if (args is null || args.Count == 0)
            return template;

        return PlaceholderRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!args.TryGetValue(key, out var value) || value is null)
                return match.Value;

            return value switch
            {
                decimal d => d.ToString("N2", cultureInfo),
                DateTimeOffset dto => dto.ToString("d", cultureInfo),
                DateTime dt => dt.ToString("d", cultureInfo),
                IFormattable f => f.ToString(null, cultureInfo),
                _ => value.ToString() ?? match.Value,
            };
        });
    }

    /// <summary>
    /// Gets the localized error message for a <see cref="DomainError"/>.
    /// </summary>
    public static string Format(DomainError error, string culture = "en-US")
        => Format(error.Code, error.Args, culture);

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}
