namespace POS.Domain.Common;

public enum ErrorCode
{
    // Req 1
    AuditWriteFailed,
    // Req 2, 3, 4
    DuplicateUsername, DuplicateEmail, InvalidEmailFormat, InsufficientPermissions,
    LastAdministratorRequired, CannotRemoveOwnAdministratorRole,
    InvalidCredentials, AccountLocked, SessionExpired,
    InvalidOrExpiredResetToken, PasswordRequirementsNotMet, ResetEmailSendFailed,
    // Req 9
    InvalidProductIdentifier, InsufficientInventory, NoActiveShiftForCashTransaction,
    VoucherNotFound, VoucherAlreadyUsed, VoucherExpired, CustomerHasNoStoreCredit,
    AdditionalPaymentRequired, InsufficientPayment, TransactionInventoryUpdateFailed,
    // Req 10, 11, 12
    DuplicateSku, ProductNoLongerAvailable,
    InvalidOrExpiredTransaction, ReturnQuantityExceedsOriginal,
    NoActiveShiftForCashRefund, ManagerAuthorizationRequiredForRefund,
    ReturnInventoryUpdateFailed,
    CashDrawerHasActiveShift, UserHasActiveShift, VarianceExplanationRequired,
    // Req 13, 14, 15
    CustomerEmailAlreadyRegistered,
    CategoryNameAlreadyExistsAtLevel, InvalidParentCategory, MaxCategoryDepthExceeded,
    CircularCategoryReference,
    AdministratorRoleRequiredForGlobalMargin, InvalidProfitMargin, InvalidCostPrice,
    PriceRecalculationFailed,
    // Req 16, 17, 18, 19, 20
    UnsupportedImageFormat, ImageTooLarge, ImageDimensionsExceeded, ImageCorrupted,
    ImageUploadFailed, InsufficientPermissionsForProductImages,
    NoCustomerEmailAvailable, ReceiptEmailSendFailed, ReceiptNotFound, ReceiptPrintFailed,
    ReceiptFooterTooLong,
    InvalidBarcodeFormat, InvalidBarcodeCheckDigit, BarcodeAlreadyAssigned, BarcodeNotFound,
    LineItemQuantityExceedsMaximum,
    InvalidDiscountPercentage, DiscountAmountExceedsBase, DiscountWouldResultInNegativeTotal,
    DiscountExceedsLimit, DiscountReasonRequired,
    ManagerAuthorizationRequiredToVoid, TransactionBelongsToClosedOperatingDay,
    ShiftAlreadyClosed, VoidReasonAndNotesRequired, TransactionAlreadyVoided,
    TransactionVoidedCannotBeReturned, TransactionHasReturns, VoidInventoryRestoreFailed,
    // Genéricos
    DateRangeExceedsLimit, ReportGenerationFailed, NoDataFound, ConcurrencyConflict, UnexpectedError
}
