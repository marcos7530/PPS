# Requirements Document

## Introduction

Este documento especifica los requisitos para un sistema de punto de venta (POS) empresarial desarrollado en .NET. El sistema proporciona capacidades completas de auditoría, gestión de usuarios con control de acceso basado en roles, autenticación segura, y herramientas de análisis e informes para operaciones comerciales.

## Glossary

- **POS_System**: El sistema completo de punto de venta que gestiona ventas, inventario, usuarios y análisis
- **Audit_Log**: Registro cronológico inmutable de todas las operaciones del sistema
- **User**: Cuenta de usuario individual con credenciales y permisos asignados
- **Role**: Conjunto de permisos que define las acciones permitidas para un usuario
- **Administrator**: Usuario con rol de acceso completo al sistema
- **Session**: Período autenticado de acceso al sistema para un usuario
- **Report_Engine**: Componente que genera informes basados en datos del sistema
- **Dashboard**: Interfaz visual que muestra gráficas y métricas del negocio
- **Authentication_Service**: Servicio que verifica identidad de usuarios y gestiona sesiones
- **Password_Reset_Token**: Token temporal con tiempo de expiración para recuperación de contraseña
- **Transaction**: Operación de venta o modificación de datos en el sistema
- **Chart_Widget**: Componente visual configurable que muestra una gráfica específica
- **Return**: Operación que revierte parcial o totalmente una transacción de venta previa
- **Refund**: Reembolso monetario o crédito otorgado al cliente por una devolución
- **Cash_Drawer**: Caja física donde se almacena efectivo durante un turno de ventas
- **Shift**: Período de tiempo durante el cual un usuario tiene control de una caja
- **Cash_Count**: Proceso de conteo de efectivo al inicio o cierre de un turno
- **Withdrawal**: Retiro de efectivo de la caja durante un turno (sangría)
- **Deposit**: Adición de efectivo a la caja durante un turno
- **Customer**: Persona o entidad que realiza compras y puede tener historial de transacciones
- **Category**: Agrupación lógica de productos con posibilidad de jerarquía
- **Parent_Category**: Categoría superior en la jerarquía de categorías
- **Child_Category**: Categoría subordinada a una categoría padre
- **Cost_Price**: Costo de adquisición de un producto expresado en la moneda configurada del sistema
- **Profit_Margin**: Porcentaje de ganancia aplicado sobre el Cost_Price para calcular el precio de venta
- **Global_Profit_Margin**: Profit_Margin por defecto del sistema aplicable a todos los productos sin margen más específico
- **Category_Profit_Margin**: Profit_Margin definido para una Category que sobrescribe el Global_Profit_Margin
- **Product_Profit_Margin**: Profit_Margin definido para un producto individual que sobrescribe el Category_Profit_Margin y el Global_Profit_Margin
- **Effective_Profit_Margin**: Profit_Margin resultante de aplicar la precedencia producto > categoría > global para un producto determinado
- **Suggested_Price**: Precio de venta calculado como Cost_Price × (1 + Effective_Profit_Margin / 100)
- **Manual_Price_Override**: Precio de venta ingresado manualmente por un usuario en lugar del Suggested_Price
- **Gross_Margin**: Diferencia monetaria entre el precio de venta registrado y el Cost_Price registrado
- **Realized_Margin_Percentage**: Porcentaje de margen calculado como (precio de venta - Cost_Price) / precio de venta × 100
- **Product_Image**: Archivo de imagen único asociado a un producto y almacenado por el sistema, con un máximo de una Product_Image por producto
- **Thumbnail**: Versión reducida de 200 x 200 píxeles de la Product_Image de un producto, utilizada en listados e interfaces de venta
- **Image_Placeholder**: Imagen genérica por defecto provista por el sistema que se muestra cuando un producto no tiene Product_Image almacenada o cuando la carga de una imagen falla en la interfaz
- **Receipt**: Comprobante emitido por el POS_System que documenta una Transaction completada o un Return completado
- **Receipt_Reprint**: Emisión posterior de un Receipt ya generado, identificada como reimpresión y contabilizada por cantidad de reimpresiones
- **Receipt_Footer_Text**: Texto opcional configurado por un Administrator que el POS_System imprime al final de cada Receipt
- **Thermal_Printer**: Impresora térmica de 80 mm de ancho de papel utilizada como canal de salida de un Receipt
- **Barcode**: Código de identificación de un producto codificado en formato EAN-13, UPC-A o Code 128
- **Discount**: Reducción del importe de una línea de Transaction o del total de una Transaction, expresada como porcentaje o como monto fijo
- **Discount_Limit**: Porcentaje máximo de Discount que un User puede aplicar según su Role sin autorización adicional
- **Discount_Authorization**: Aprobación explícita de un User con rol Manager o Administrator que habilita un Discount superior al Discount_Limit del User que lo aplica
- **Discount_Reason**: Motivo obligatorio asociado a un Discount, seleccionado de una lista predefinida
- **Void**: Operación que anula una Transaction completada del Operating_Day en curso sin eliminarla del sistema
- **Voided_Transaction**: Transaction marcada como anulada mediante un Void, excluida de los totales de venta y conservada en el historial y en el Audit_Log
- **Operating_Day**: Día calendario del negocio, en la zona horaria configurada del sistema, al que pertenece una Transaction
- **Store_Credit**: Saldo a favor otorgado a un Customer como resultado de un Return, utilizable como método de pago
- **Store_Credit_Voucher**: Comprobante de Store_Credit no asociado a un Customer, identificado por un código de 32 caracteres alfanuméricos con vigencia de 365 días

## Requirements

### Requirement 1: Auditoría de Operaciones

**User Story:** Como auditor del sistema, quiero que todas las operaciones sean trazables, para poder revisar el historial completo de cambios y acciones.

#### Acceptance Criteria

1. WHEN a User performs an operation that modifies data (create, update, delete of User, Product, Transaction, or system configuration), THE POS_System SHALL record the operation in the Audit_Log with UTC timestamp (millisecond precision), user identifier, operation type, and affected entity identifiers

2. IF an operation fails due to validation error or system error, THEN THE POS_System SHALL record the failed attempt in the Audit_Log with error reason

3. THE POS_System SHALL store Audit_Log entries in append-only storage without modification or deletion capability for a minimum retention period of 7 years

4. WHEN an Administrator requests audit history, THE POS_System SHALL retrieve and display up to 10,000 audit entries per query with filtering by date range (maximum 366 days), user identifier, and operation type

5. IF an Administrator's audit query exceeds 10,000 matching entries, THEN THE POS_System SHALL return the most recent 10,000 entries and indicate total count available

6. WHERE an operation modifies existing data, THE POS_System SHALL include in the audit entry the before state and after state in JSON format

7. WHEN a Transaction is completed, THE POS_System SHALL record all line items with product identifiers and quantities, subtotal, tax amount, discount amount, final total, payment method, and UTC timestamp in the Audit_Log

8. IF the POS_System fails to write an audit entry, THEN THE POS_System SHALL reject the operation and return an error to the User

### Requirement 2: Gestión de Usuarios y Roles

**User Story:** Como administrador, quiero gestionar usuarios y asignarles roles, para controlar el acceso a las funcionalidades del sistema.

#### Acceptance Criteria

1. WHEN an Administrator creates a User account, THE POS_System SHALL accept username (1 to 50 characters), email (valid format, maximum 100 characters), password (minimum 8 characters), and at least one assigned Role

2. IF a User creation request contains a username or email that already exists in the system, THEN THE POS_System SHALL reject the creation and provide an error message indicating the duplicate field

3. IF a User creation request contains an invalid email format, THEN THE POS_System SHALL reject the creation and provide an error message indicating invalid email

4. THE POS_System SHALL support multiple Roles including Administrator, Manager, Cashier, and Viewer

5. IF a User attempts an operation without the required Role permission, THEN THE POS_System SHALL deny the operation and provide an error message indicating insufficient permissions

6. WHEN a User with valid Role permission attempts an operation, THE POS_System SHALL allow the operation to proceed

7. WHEN an Administrator modifies a User's assigned Roles, THE POS_System SHALL apply permission changes for that User's next authentication session

8. IF an Administrator attempts to delete the last remaining Administrator account, THEN THE POS_System SHALL reject the deletion and provide an error message indicating at least one Administrator must remain

### Requirement 3: Autenticación Segura

**User Story:** Como usuario del sistema, quiero iniciar sesión de forma segura, para proteger el acceso a la información del negocio.

#### Acceptance Criteria

1. WHEN a User submits login credentials, THE Authentication_Service SHALL verify username and password against stored hashed credentials using bcrypt with cost factor minimum 10

2. IF login credentials are invalid, THEN THE Authentication_Service SHALL return error message "Invalid credentials" without revealing whether username or password was incorrect

3. WHEN login credentials are valid, THE Authentication_Service SHALL create a Session with cryptographically random token (minimum 128-bit entropy) and expiration time of 8 hours from creation

4. THE POS_System SHALL enforce password requirements of 8 to 128 characters including at least one uppercase letter, at least one lowercase letter, at least one numeric digit, and at least one special character from the set !@#$%^&*()_+-=[]{}|;:,.<>?

5. WHEN a User fails login attempts three times within any 15-minute window, THE Authentication_Service SHALL lock the account for 30 minutes and return error message "Account locked due to multiple failed attempts"

6. WHEN a Session token is expired or invalid, THE POS_System SHALL reject the request with HTTP 401 status and require re-authentication

7. IF a User with active Session attempts an operation after Session expiration (8 hours), THEN THE POS_System SHALL reject the operation with error message "Session expired, please login again"

8. WHEN a locked account's 30-minute lockout period expires, THE Authentication_Service SHALL automatically unlock the account

### Requirement 4: Recuperación de Contraseñas

**User Story:** Como usuario que olvidó su contraseña, quiero recuperar el acceso a mi cuenta, para poder continuar usando el sistema.

#### Acceptance Criteria

1. WHEN a User requests password reset, THE POS_System SHALL generate a cryptographically random Password_Reset_Token (minimum 128-bit entropy) with 24-hour expiration from generation time

2. IF a User requests password reset for a non-existent email address, THEN THE POS_System SHALL respond with the same success message as for existing accounts without revealing account existence

3. WHEN a Password_Reset_Token is generated for an existing User, THE POS_System SHALL send the token as a URL link to the User's registered email address

4. IF the POS_System fails to send the password reset email after 3 retry attempts, THEN THE POS_System SHALL log the failure and return error message "Unable to send reset email, please contact support"

5. WHEN a User submits a valid Password_Reset_Token with new password that meets password requirements, THE POS_System SHALL update the password hash using bcrypt and invalidate the token immediately

6. IF a Password_Reset_Token has expiration time in the past or does not match any stored token, THEN THE POS_System SHALL reject the password reset request with error message "Invalid or expired reset token"

7. IF a User submits a Password_Reset_Token with new password that fails password requirements, THEN THE POS_System SHALL reject the request with error message describing unmet requirements

8. WHEN a User requests a new password reset, THE POS_System SHALL invalidate all existing Password_Reset_Tokens for that User

9. WHEN a User successfully resets their password, THE POS_System SHALL invalidate all active Sessions for that User

10. THE POS_System SHALL limit password reset requests to 5 per email address per hour to prevent abuse

### Requirement 5: Acceso Completo del Administrador

**User Story:** Como administrador, quiero tener acceso completo a todas las funciones del sistema, para poder gestionar y configurar el sistema según sea necesario.

#### Acceptance Criteria

1. WHERE a User has Administrator role, THE POS_System SHALL grant access to User management (create, read, update, delete Users and Roles), system configuration (tax rates, currency, business information), audit review (read Audit_Log), inventory management (create, read, update, deactivate Products), transaction operations (create, read, void Transactions), and report generation (create, read, export Reports)

2. WHERE a User has Administrator role, THE POS_System SHALL allow viewing and modifying username, email, and roles of any User account including other Administrators

3. IF an Administrator attempts to remove Administrator role from their own account, THEN THE POS_System SHALL reject the operation with error message "Cannot remove your own Administrator role"

4. WHERE a User has Administrator role, THE POS_System SHALL allow configuration of tax rates (0.00% to 100.00% with 2 decimal precision), currency code (3-letter ISO 4217), business name (1 to 100 characters), and business address (1 to 500 characters)

5. WHERE a User has Administrator role, THE POS_System SHALL allow access to all Report types (sales, inventory, audit) and Dashboard configurations for any User

6. IF an Administrator attempts to delete an Administrator account, THEN THE POS_System SHALL check for remaining Administrators and reject if it would be the last Administrator account

7. WHERE a User has Administrator role, THE POS_System SHALL allow creation of other Administrator accounts without limitation on total count

8. IF an Administrator modifies system settings with invalid values (tax rate outside 0-100%, invalid currency code, empty business name), THEN THE POS_System SHALL reject the modification with error message indicating invalid field

### Requirement 6: Aplicación Web Responsiva

**User Story:** Como usuario en diferentes dispositivos, quiero que la aplicación se adapte a mi pantalla, para poder trabajar cómodamente desde escritorio, tablet o móvil.

#### Acceptance Criteria

1. THE POS_System SHALL render user interfaces that display all content without overlap or text truncation at viewport widths from 320px to 2560px

2. WHEN the viewport width is less than 768px, THE POS_System SHALL display navigation menu items in vertically stacked format instead of horizontal layout

3. WHERE the viewport width is less than 768px, THE POS_System SHALL ensure all interactive elements (buttons, links, form controls) have minimum touch target size of 44x44 pixels

4. THE POS_System SHALL maintain access to all core functions (login, transactions, inventory search, report generation) at viewport widths from 320px to 2560px without horizontal scrolling

5. THE POS_System SHALL render body text at minimum 16px font size and heading text at minimum 20px font size on viewports less than 768px wide

6. WHEN the viewport width is less than 768px, THE POS_System SHALL display tabular data (product lists, transaction history) in card format or horizontally scrollable containers with scroll indicators

7. WHERE the viewport width is less than 768px, THE POS_System SHALL display form inputs with minimum height of 44px and visible labels above inputs instead of inline

### Requirement 7: Generación de Informes

**User Story:** Como gerente, quiero generar informes sobre las operaciones del negocio, para analizar rendimiento y tomar decisiones informadas.

#### Acceptance Criteria

1. WHERE a User has Manager or Administrator role, THE Report_Engine SHALL accept report parameters including date range (maximum 366 days), product category identifiers, and user identifiers for sales report generation

2. IF a User requests a report with date range exceeding 366 days, THEN THE Report_Engine SHALL reject the request with error message "Date range cannot exceed 366 days"

3. WHEN a User with Manager or Administrator role requests a report with valid parameters, THE Report_Engine SHALL retrieve Transaction records and Audit_Log entries matching the specified filters

4. IF the requested report data is unavailable due to system error, THEN THE Report_Engine SHALL return error message "Report generation failed, please try again or contact support"

5. THE Report_Engine SHALL export reports in PDF format (maximum 50,000 rows) and Excel format (maximum 100,000 rows) with warning if data exceeds limit

6. WHEN a report is generated, THE Report_Engine SHALL include summary statistics: total sales amount (2 decimal precision), transaction count (integer), and average transaction value (2 decimal precision)

7. WHERE a User has Manager or Administrator role, THE Report_Engine SHALL allow scheduling of recurring reports with daily, weekly, or monthly frequency and email delivery to specified recipients (maximum 10 email addresses)

8. IF scheduled report email delivery fails after 3 retry attempts, THEN THE POS_System SHALL log the failure and notify the User who created the schedule

9. WHEN a scheduled report generates successfully, THE Report_Engine SHALL send the report as email attachment in the format specified at schedule creation (PDF or Excel)

10. IF a User requests a report with filters that match zero transactions, THEN THE Report_Engine SHALL return an empty report with message "No data found for specified criteria"

### Requirement 8: Visualización de Gráficas y Dashboard

**User Story:** Como usuario del sistema, quiero ver gráficas del negocio en un dashboard, para monitorear el rendimiento de forma visual e inmediata.

#### Acceptance Criteria

1. THE Dashboard SHALL display Chart_Widgets showing business metrics including sales amount by day (for last 30 days), top 10 products by quantity sold (for last 30 days), and sales amount by product category (for last 30 days)

2. THE POS_System SHALL allow Users to select from available Chart_Widget types (daily sales line chart, top products bar chart, sales by category pie chart, total sales numeric indicator) and add them to their Dashboard (maximum 8 widgets per dashboard)

3. WHEN a User adds, removes, or reorders Chart_Widgets on their Dashboard, THE POS_System SHALL save the configuration associated with that User's account

4. WHEN the Dashboard page is loaded, THE POS_System SHALL retrieve transaction data from the last 90 days and populate each Chart_Widget with calculated metrics

5. IF transaction data fails to load due to system error, THEN THE Dashboard SHALL display error message "Unable to load dashboard data" on affected Chart_Widgets

6. THE POS_System SHALL render Chart_Widgets as line charts (for time series data), bar charts (for ranking data), pie charts (for proportional data), and numeric indicators (for single values with unit labels)

7. WHEN a User hovers mouse pointer over chart data points, THE Dashboard SHALL display tooltip with numeric value (2 decimal precision for currency, 0 decimals for quantities) and associated label

8. THE Dashboard SHALL provide date range filter controls allowing Users to select start date and end date (maximum range 366 days) for displayed data

9. WHEN a User applies date range filter, THE Dashboard SHALL update all Chart_Widgets to display data from the selected date range within 3 seconds

10. IF a User applies date range filter that results in zero transactions, THEN THE Dashboard SHALL display message "No data available for selected date range" on each Chart_Widget

### Requirement 9: Gestión de Transacciones de Venta

**User Story:** Como cajero, quiero registrar transacciones de venta, para procesar compras de clientes y mantener el inventario actualizado.

#### Acceptance Criteria

1. WHERE a User has Cashier, Manager, or Administrator role, THE POS_System SHALL allow creating a Transaction by adding line items with product identifier, quantity (1 to 9999), and unit price (0.01 to 999999.99)

2. IF a User without Cashier, Manager, or Administrator role attempts to create a Transaction, THEN THE POS_System SHALL reject the operation with error message "Insufficient permissions to create transactions"

3. WHEN line items are added to a Transaction, THE POS_System SHALL calculate subtotal (sum of quantity × unit price for all line items), tax amount (subtotal × configured tax rate), discount amount (0.00 to subtotal, calculated and validated as specified in Requirement 19), and final amount (subtotal + tax - discount) with 2 decimal precision

4. IF a User attempts to add a line item with product identifier that does not exist in inventory, THEN THE POS_System SHALL reject the addition with error message "Invalid product identifier"

5. IF a User attempts to add a line item with quantity exceeding current inventory quantity for that product, THEN THE POS_System SHALL reject the addition with error message "Insufficient inventory: [available quantity] available"

6. WHEN a User submits Transaction for completion, THE POS_System SHALL require payment method (cash, credit card, debit card, store credit) and amount received (minimum: final amount)

7. IF a User submits a Transaction for completion with payment method cash or with a cash component and that User has no active Shift, THEN THE POS_System SHALL reject Transaction completion with error message "No active shift. Open a shift before processing cash transactions"

8. WHERE payment method is store credit, THE POS_System SHALL require either a Store_Credit_Voucher code (32 alphanumeric characters) or the customer identifier of a Customer with a Store_Credit balance greater than 0.00 linked to the Transaction

9. IF a submitted Store_Credit_Voucher code does not match any stored voucher, THEN THE POS_System SHALL reject Transaction completion with error message "Store credit voucher not found"

10. IF a submitted Store_Credit_Voucher is already marked as used, THEN THE POS_System SHALL reject Transaction completion with error message "Store credit voucher has already been used"

11. IF a submitted Store_Credit_Voucher has an expiration date earlier than the current UTC date, THEN THE POS_System SHALL reject Transaction completion with error message "Store credit voucher expired on [expiration date]"

12. IF the customer identifier submitted for store credit payment corresponds to a Customer with Store_Credit balance of 0.00, THEN THE POS_System SHALL reject Transaction completion with error message "Customer has no store credit available"

13. WHEN a valid Store_Credit_Voucher or Customer Store_Credit balance is submitted, THE POS_System SHALL apply store credit to the Transaction for the lesser of the available store credit amount and the final amount with 2 decimal precision

14. IF the applied store credit amount is less than the final amount, THEN THE POS_System SHALL require an additional payment method (cash, credit card, or debit card) for the remaining amount and display message "Store credit applied: [applied amount]. Additional payment of [remaining amount] required"

15. WHEN a Transaction paid partially or fully with store credit is successfully completed, THE POS_System SHALL decrement the Customer Store_Credit balance by the applied store credit amount or mark the Store_Credit_Voucher as used when the full voucher amount is applied, and record the applied store credit amount, voucher code (if applicable), and customer identifier (if applicable) in the Audit_Log

16. IF amount received is less than final amount, THEN THE POS_System SHALL reject Transaction completion with error message "Insufficient payment: [shortfall amount] required"

17. IF amount received is greater than final amount, THEN THE POS_System SHALL calculate change due (amount received - final amount) with 2 decimal precision and display to User

18. WHEN a Transaction is successfully completed, THE POS_System SHALL generate unique transaction identifier using UUID version 4 format

19. WHERE the completing User has an active Shift, THE POS_System SHALL record the shift identifier of that Shift and the Operating_Day of the Transaction in the Transaction record and in the Audit_Log when the Transaction is successfully completed

20. WHERE a Transaction is completed with payment method credit card or debit card and the completing User has no active Shift, THE POS_System SHALL record the Transaction with an empty shift identifier and exclude the Transaction from Shift cash calculations specified in Requirement 12

21. WHEN a Transaction is successfully completed, THE POS_System SHALL atomically decrement inventory quantities for all line item products by their sold quantities

22. IF inventory update fails for any product during Transaction completion, THEN THE POS_System SHALL rollback the entire Transaction and return error message "Transaction failed, inventory could not be updated"

### Requirement 10: Gestión de Inventario

**User Story:** Como gerente de inventario, quiero gestionar productos y existencias, para mantener el inventario actualizado y prevenir desabastecimiento.

#### Acceptance Criteria

1. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow creation of products with name (1 to 100 characters), SKU (1 to 50 characters), description (0 to 500 characters), price (0.01 to 999999.99), Cost_Price (0.01 to 999999.99 as specified in Requirement 15), category identifier, current quantity (0 to 999999), and minimum stock threshold (0 to 999999)

2. WHERE a product's current quantity is less than or equal to its minimum stock threshold, THE POS_System SHALL mark the product with low stock status visible in product lists

3. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow modification of product name, description, price, Cost_Price, category, current quantity, and minimum stock threshold for existing products

4. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow marking products as deactivated without deletion from the system

5. IF a User attempts to add a line item to a Transaction with deactivated product, THEN THE POS_System SHALL reject the addition with error message "Product is no longer available"

6. WHEN a User with Manager or Administrator role modifies product quantity, THE POS_System SHALL record the change in the Audit_Log including previous quantity, new quantity, adjustment reason (return, damage, correction, restock), and User identifier

7. WHEN Transaction completion would result in negative inventory quantity for any product, THE POS_System SHALL reject the Transaction as specified in Requirement 9 criterion 5

8. IF a User attempts to create a product with SKU that already exists in the system, THEN THE POS_System SHALL reject creation with error message "SKU already exists"

9. THE POS_System SHALL enforce SKU uniqueness across all products including deactivated products

10. WHERE a product is marked as deactivated, THE POS_System SHALL exclude it from product selection lists for new Transactions while preserving it in historical Transaction records


### Requirement 11: Devoluciones y Reembolsos

**User Story:** Como cajero, quiero procesar devoluciones de productos vendidos, para gestionar reembolsos a clientes y ajustar el inventario correctamente.

#### Acceptance Criteria

1. WHERE a User has Cashier, Manager, or Administrator role, THE POS_System SHALL allow initiating a Return by providing the original transaction identifier

2. IF a User provides a transaction identifier that does not exist or is older than 90 days, THEN THE POS_System SHALL reject the Return with error message "Invalid or expired transaction identifier"

3. WHEN a User initiates a Return with valid transaction identifier, THE POS_System SHALL display all line items from the original Transaction with product names, quantities, and unit prices

4. WHERE a Return is being processed, THE POS_System SHALL allow selection of line items to return with return quantity (1 to original line item quantity)

5. IF a User attempts to specify return quantity exceeding the original line item quantity, THEN THE POS_System SHALL reject the selection with error message "Return quantity cannot exceed original quantity of [original quantity]"

6. WHEN line items are selected for Return, THE POS_System SHALL calculate refund amount as sum of (return quantity × unit price) for all selected items with 2 decimal precision

7. WHERE a Return is being processed, THE POS_System SHALL require selection of refund method (cash, credit card reversal, store credit) and reason code (defective product, customer regret, wrong product, other)

8. IF refund method is cash and the User processing the Return has no active Shift, THEN THE POS_System SHALL reject the Return completion with error message "No active shift. Open a shift before processing cash refunds"

9. WHEN a Return is completed with cash refund method, THE POS_System SHALL record the shift identifier of the active Shift of the completing User in the Return record and in the Audit_Log

10. WHERE refund method is store credit or the Return refund amount exceeds 500.00, THE POS_System SHALL require authorization from a User with Manager or Administrator role

11. IF authorization is required and a User without Manager or Administrator role attempts to complete the Return, THEN THE POS_System SHALL reject the completion with error message "Manager authorization required for refunds exceeding 500.00"

12. WHEN a Return is successfully completed, THE POS_System SHALL generate unique return identifier using UUID version 4 format and link it to the original transaction identifier

13. WHEN a Return is successfully completed, THE POS_System SHALL atomically increment inventory quantities for all returned products by their return quantities

14. IF inventory update fails for any product during Return completion, THEN THE POS_System SHALL rollback the entire Return and return error message "Return failed, inventory could not be updated"

15. WHEN a Return is completed with store credit refund method, THE POS_System SHALL create a Store_Credit balance associated with the Customer (if Transaction was linked to Customer) or generate a Store_Credit_Voucher code (32 alphanumeric characters) valid for 365 days, usable as payment method as specified in Requirement 9

16. WHERE a Return is completed, THE POS_System SHALL record the Return in the Audit_Log with UTC timestamp (millisecond precision), user identifier, return identifier, original transaction identifier, returned line items with quantities, refund amount, refund method, reason code, and authorizing manager identifier (if applicable)

### Requirement 12: Turnos de Caja

**User Story:** Como cajero y supervisor, quiero gestionar turnos de caja con apertura y cierre de turno, para controlar el efectivo y garantizar transparencia en las operaciones de caja.

#### Acceptance Criteria

1. WHERE a User has Cashier, Manager, or Administrator role, THE POS_System SHALL allow opening a Shift by specifying Cash_Drawer identifier (1 to 20 characters), opening cash amount (0.00 to 999999.99), and opening cash breakdown by denomination (100.00, 50.00, 20.00, 10.00, 5.00, 1.00, 0.25, 0.10, 0.05, 0.01)

2. IF a User attempts to open a Shift for a Cash_Drawer that already has an active Shift, THEN THE POS_System SHALL reject the opening with error message "Cash drawer already has an active shift"

3. IF a User already has an active Shift for any Cash_Drawer, THEN THE POS_System SHALL reject attempts to open another Shift with error message "User already has an active shift"

4. WHEN a Shift is successfully opened, THE POS_System SHALL generate unique shift identifier using UUID version 4 format and record opening timestamp (UTC millisecond precision), user identifier, Cash_Drawer identifier, and opening cash amount

5. WHILE a Shift is active for a User, THE POS_System SHALL allow that User to record Withdrawals specifying amount (0.01 to 99999.99), reason (bank deposit, change request, other), and optional notes (0 to 200 characters)

6. WHILE a Shift is active for a User, THE POS_System SHALL allow that User to record Deposits specifying amount (0.01 to 99999.99), source (change delivery, correction, other), and optional notes (0 to 200 characters)

7. WHEN a Withdrawal or Deposit is recorded, THE POS_System SHALL log the operation in the Audit_Log with UTC timestamp, user identifier, shift identifier, operation type (withdrawal or deposit), amount, and reason/source

8. WHERE a User has an active Shift, THE POS_System SHALL calculate expected cash balance with 2 decimal precision as opening cash amount + total cash amount of Transactions recorded with that shift identifier and not marked as voided + Deposits - Withdrawals - total cash Refunds recorded with that shift identifier as specified in Requirement 11 - total cash amount of Transactions recorded with that shift identifier and voided during the Shift as specified in Requirement 20

9. WHEN a User closes their active Shift, THE POS_System SHALL require Cash_Count entry with closing cash amount (0.00 to 999999.99) and closing cash breakdown by denomination

10. IF closing cash amount differs from expected cash balance, THEN THE POS_System SHALL calculate variance (closing cash amount - expected cash balance) and mark the Shift with variance status (over if positive, short if negative, balanced if 0.00)

11. WHERE closing cash variance absolute value exceeds 10.00, THE POS_System SHALL require mandatory notes (1 to 500 characters) explaining the variance

12. IF a User attempts to close Shift with variance exceeding 10.00 without providing variance notes, THEN THE POS_System SHALL reject the closure with error message "Variance explanation required for variances exceeding 10.00"

13. WHEN a Shift is successfully closed, THE POS_System SHALL record closing timestamp (UTC millisecond precision), closing cash amount, expected cash balance, variance amount, variance status, variance notes (if applicable), and mark the Shift as closed

14. WHERE a Shift is closed, THE POS_System SHALL generate a shift summary report including shift identifier, user name, opening timestamp, closing timestamp, opening cash amount, total cash sales, total cash Refunds, total voided cash sales, total Withdrawals, total Deposits, expected cash balance, closing cash amount, variance amount, variance status, transaction count, cash Return count, and voided transaction count

15. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow viewing shift summary reports for any User and any Cash_Drawer with filtering by date range (maximum 366 days) and variance status

### Requirement 13: Gestión de Clientes

**User Story:** Como usuario del sistema, quiero registrar y vincular clientes a transacciones, para mantener historial de compras y ofrecer mejor servicio.

#### Acceptance Criteria

1. WHERE a User has Cashier, Manager, or Administrator role, THE POS_System SHALL allow creation of Customer records with name (1 to 100 characters), email (valid format, maximum 100 characters, optional), phone number (7 to 20 digits with optional formatting characters, optional), and optional notes (0 to 500 characters)

2. IF a User attempts to create a Customer with email address that already exists in the system, THEN THE POS_System SHALL reject the creation with error message "Email address already registered to another customer"

3. IF a User attempts to create a Customer with phone number that already exists in the system, THEN THE POS_System SHALL display a warning message "Phone number already registered to [customer name]. Continue anyway?" and require confirmation

4. WHEN a Customer is successfully created, THE POS_System SHALL generate unique customer identifier using UUID version 4 format and record creation timestamp (UTC millisecond precision) and creating user identifier

5. WHERE a User has Cashier, Manager, or Administrator role, THE POS_System SHALL allow searching for Customers by name (partial match, case insensitive), email (exact match), phone number (partial match), or customer identifier

6. WHEN creating or processing a Transaction, THE POS_System SHALL allow optional linking to a Customer by providing customer identifier or selecting from search results

7. WHERE a Transaction is completed without linking to a Customer, THE POS_System SHALL process the Transaction as an anonymous sale

8. WHERE a Transaction is linked to a Customer, THE POS_System SHALL record the customer identifier in the Transaction record and Audit_Log

9. WHERE a User views a Customer record, THE POS_System SHALL display customer details (name, email, phone, notes) and purchase history including transaction identifiers, transaction timestamps, total amounts, and product summaries for the last 100 transactions

10. IF a Customer has zero transactions, THEN THE POS_System SHALL display message "No purchase history available"

11. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow modification of Customer name, email, phone number, and notes

12. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow marking Customers as inactive without deletion from the system

13. WHERE a Customer is marked as inactive, THE POS_System SHALL exclude the Customer from search results for Transaction linking but preserve all historical Transaction links

14. THE POS_System SHALL calculate and display Customer lifetime statistics including total transactions count, total purchase amount (sum of all Transaction final amounts with 2 decimal precision), and date of last purchase (UTC timestamp)

### Requirement 14: Categorías de Productos

**User Story:** Como gerente de inventario, quiero organizar productos en categorías jerárquicas, para facilitar la navegación, búsqueda y análisis de productos.

#### Acceptance Criteria

1. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow creation of Category records with name (1 to 100 characters), optional Parent_Category identifier, optional description (0 to 500 characters), and display order (integer 1 to 9999)

2. IF a User attempts to create a Category with name that already exists under the same Parent_Category (or at root level if no parent), THEN THE POS_System SHALL reject the creation with error message "Category name already exists at this level"

3. WHEN a Category is successfully created, THE POS_System SHALL generate unique category identifier using UUID version 4 format and record creation timestamp (UTC millisecond precision)

4. WHERE a Category has a Parent_Category specified, THE POS_System SHALL validate that the Parent_Category identifier exists and is not marked as inactive

5. IF a User attempts to create a Category with non-existent or inactive Parent_Category identifier, THEN THE POS_System SHALL reject the creation with error message "Invalid parent category"

6. THE POS_System SHALL support category hierarchy depth of maximum 5 levels (root level counted as level 1)

7. IF a User attempts to create a Category that would exceed hierarchy depth of 5 levels, THEN THE POS_System SHALL reject the creation with error message "Maximum category depth of 5 levels exceeded"

8. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow modification of Category name, Parent_Category identifier, description, and display order

9. IF a User attempts to modify a Category's Parent_Category to create a circular reference (category becoming ancestor of itself), THEN THE POS_System SHALL reject the modification with error message "Circular category reference not allowed"

10. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow marking Categories as inactive without deletion from the system

11. WHERE a Category is marked as inactive, THE POS_System SHALL automatically mark all Child_Categories as inactive recursively

12. WHERE a Category is marked as inactive, THE POS_System SHALL prevent assignment of products to that Category while preserving existing product assignments for historical records

13. WHEN creating or modifying a product, THE POS_System SHALL allow assignment to exactly one Category by providing category identifier

14. WHERE products are assigned to Categories, THE POS_System SHALL allow filtering product lists by category identifier with option to include products from Child_Categories recursively

15. WHEN displaying product selection interface for Transactions, THE POS_System SHALL organize products by Category hierarchy showing Parent_Categories with expandable Child_Categories ordered by display order value

16. WHERE a User views a Category, THE POS_System SHALL display category details (name, parent category name if applicable, description) and product count (total number of active products assigned to this category and all Child_Categories recursively)

17. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow reordering Categories within the same hierarchy level by modifying display order values

18. THE Report_Engine SHALL accept category identifier as filter parameter for sales reports and inventory reports with option to include Child_Categories recursively

### Requirement 15: Configuración de Márgenes de Ganancia

**User Story:** Como gerente de inventario, quiero configurar manualmente los porcentajes de ganancia aplicados sobre el costo de los productos, para controlar la rentabilidad del negocio y calcular precios de venta de forma consistente.

#### Acceptance Criteria

1. WHERE a User has Administrator role, THE POS_System SHALL allow configuration of the Global_Profit_Margin as a percentage value from 0.00 to 1000.00 with 2 decimal precision

2. IF a User without Administrator role attempts to modify the Global_Profit_Margin, THEN THE POS_System SHALL reject the operation with error message "Administrator role required to modify global profit margin"

3. IF a User submits a Global_Profit_Margin, Category_Profit_Margin, or Product_Profit_Margin value outside the range 0.00 to 1000.00 or with more than 2 decimal places, THEN THE POS_System SHALL reject the modification with error message "Profit margin must be between 0.00% and 1000.00% with maximum 2 decimal places"

4. WHERE no Administrator has configured the Global_Profit_Margin, THE POS_System SHALL apply a Global_Profit_Margin of 30.00 percent

5. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow defining, modifying, and clearing a Category_Profit_Margin (0.00 to 1000.00 with 2 decimal precision) for each Category

6. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow defining, modifying, and clearing a Product_Profit_Margin (0.00 to 1000.00 with 2 decimal precision) for each individual product

7. WHEN the POS_System determines the Effective_Profit_Margin for a product, THE POS_System SHALL select the Product_Profit_Margin if defined, otherwise the Category_Profit_Margin of the Category assigned to that product if defined, otherwise the Global_Profit_Margin

8. WHERE a product's assigned Category has no Category_Profit_Margin defined and that Category has a Parent_Category, THE POS_System SHALL select the Category_Profit_Margin of the nearest ancestor Category that has a defined Category_Profit_Margin before selecting the Global_Profit_Margin

9. WHERE a User has Manager or Administrator role and is creating or modifying a product, THE POS_System SHALL require Cost_Price with value from 0.01 to 999999.99 and 2 decimal precision

10. IF a User submits a product Cost_Price outside the range 0.01 to 999999.99 or with more than 2 decimal places, THEN THE POS_System SHALL reject the operation with error message "Cost price must be between 0.01 and 999999.99 with maximum 2 decimal places"

11. WHEN a User enters or modifies a product Cost_Price, THE POS_System SHALL calculate the Suggested_Price as Cost_Price × (1 + Effective_Profit_Margin / 100) rounded to 2 decimal places using half-up rounding and display the Suggested_Price together with the applied Effective_Profit_Margin and its source (product, category, or global)

12. WHEN a User creates or modifies a product, THE POS_System SHALL allow the User to accept the Suggested_Price as the product sale price or to enter a Manual_Price_Override with value from 0.01 to 999999.99 and 2 decimal precision

13. WHERE a Manual_Price_Override is applied to a product, THE POS_System SHALL mark the product sale price as manually overridden and record the overriding user identifier and UTC timestamp (millisecond precision)

14. WHERE a product sale price is less than the product Cost_Price, THE POS_System SHALL display a loss indicator on the product form and in product lists with message "Warning: sale price is below cost price (loss of [loss amount] per unit)"

15. IF a User submits a Manual_Price_Override that is less than the product Cost_Price, THEN THE POS_System SHALL require explicit confirmation with message "Sale price [sale price] is below cost price [cost price]. Confirm sale at a loss?" before saving the product

16. WHEN a User modifies the Global_Profit_Margin, a Category_Profit_Margin, or a Product_Profit_Margin, THE POS_System SHALL record the change in the Audit_Log with UTC timestamp (millisecond precision), user identifier, margin scope (global, category, or product), affected entity identifier, previous margin value, and new margin value

17. WHEN a User modifies the Global_Profit_Margin or a Category_Profit_Margin, THE POS_System SHALL display the count of existing active products whose Suggested_Price would change and request confirmation with message "This change affects [product count] products. Recalculate sale prices for these products?"

18. IF a User declines the price recalculation confirmation, THEN THE POS_System SHALL save the new margin value, preserve the existing sale prices of all products, and apply the new margin only to products created or modified after the change

19. WHEN a User confirms the price recalculation, THE POS_System SHALL set the sale price of each affected product to its newly calculated Suggested_Price, excluding products marked with Manual_Price_Override, and record each price change in the Audit_Log with product identifier, previous sale price, and new sale price

20. WHERE a price recalculation is confirmed, THE POS_System SHALL exclude products marked as deactivated from the recalculation and preserve their existing sale prices

21. IF the price recalculation fails for any product, THEN THE POS_System SHALL rollback all sale price changes of that recalculation and return error message "Price recalculation failed, no prices were changed"

22. WHEN a Transaction is completed, THE POS_System SHALL record the Cost_Price of each line item product as it exists at completion time in the Transaction record

23. THE Report_Engine SHALL calculate Gross_Margin for each Transaction line item as (unit price - recorded Cost_Price) × quantity with 2 decimal precision and Realized_Margin_Percentage as (unit price - recorded Cost_Price) / unit price × 100 with 2 decimal precision

24. WHERE a User has Manager or Administrator role, THE Report_Engine SHALL provide Gross_Margin totals and Realized_Margin_Percentage aggregated by Transaction, by product, and by Category for a requested date range of maximum 366 days

25. IF a Transaction line item has no recorded Cost_Price, THEN THE Report_Engine SHALL exclude that line item from Gross_Margin calculations and display message "Margin unavailable for [line item count] line items due to missing cost data"

### Requirement 16: Imágenes de Productos

**User Story:** Como gerente de inventario, quiero cargar una imagen por producto y contar con una imagen genérica por defecto cuando el producto no tiene imagen, para que los cajeros identifiquen los productos visualmente durante la venta y los listados se vean completos y consistentes.

#### Acceptance Criteria

1. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow uploading one Product_Image when creating a product and when modifying an existing product

2. IF a User without Manager or Administrator role attempts to upload, replace, or delete a Product_Image, THEN THE POS_System SHALL reject the operation with error message "Insufficient permissions to manage product images"

3. THE POS_System SHALL store a maximum of one Product_Image per product

4. THE POS_System SHALL accept Product_Image uploads in JPEG, PNG, and WebP formats with file size from 1 byte to 5242880 bytes (5 MB) and pixel dimensions up to 4000 x 4000 pixels

5. WHEN a Product_Image is uploaded, THE POS_System SHALL determine the actual file format by inspecting the file signature bytes of the uploaded content instead of the file name extension

6. IF the file signature bytes of an uploaded Product_Image do not match JPEG, PNG, or WebP, THEN THE POS_System SHALL reject the upload with error message "Unsupported image format. Allowed formats: JPEG, PNG, WebP"

7. IF an uploaded Product_Image file size exceeds 5242880 bytes, THEN THE POS_System SHALL reject the upload with error message "Image file exceeds maximum size of 5 MB"

8. IF an uploaded Product_Image has width greater than 4000 pixels or height greater than 4000 pixels, THEN THE POS_System SHALL reject the upload with error message "Image dimensions exceed maximum of 4000x4000 pixels"

9. IF an uploaded Product_Image cannot be decoded as a complete image of its declared format, THEN THE POS_System SHALL reject the upload with error message "Image file is corrupted or unreadable"

10. WHEN an uploaded file passes all validations specified in criteria 4 through 9 and the target product has no stored Product_Image, THE POS_System SHALL generate a unique image identifier using UUID version 4 format, store the file as the Product_Image of that product, and generate a Thumbnail of 200 x 200 pixels preserving the original aspect ratio with transparent or white padding

11. IF a User uploads a Product_Image for a product that already has a stored Product_Image, THEN THE POS_System SHALL request explicit confirmation with message "This product already has an image. Replace the existing image?" before storing the uploaded file

12. WHEN a User confirms the replacement of a stored Product_Image, THE POS_System SHALL validate the uploaded file as specified in criteria 4 through 9, store the uploaded file as the single Product_Image of that product, generate a new Thumbnail of 200 x 200 pixels preserving the original aspect ratio, and delete the previously stored Product_Image and its Thumbnail

13. IF a User declines the replacement confirmation, THEN THE POS_System SHALL discard the uploaded file and preserve the stored Product_Image and its Thumbnail unchanged

14. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow deleting the stored Product_Image of a product

15. WHEN the Product_Image of a product is deleted, THE POS_System SHALL delete the associated Thumbnail, leave that product with zero stored images, retain the corresponding Audit_Log entries, and display the Image_Placeholder for that product

16. THE POS_System SHALL provide a system-supplied generic Image_Placeholder for products that have no stored Product_Image

17. WHERE a product has a stored Product_Image, THE POS_System SHALL display the Thumbnail of that Product_Image in product lists and in the product selection interface for Transactions

18. WHERE a product has no stored Product_Image, THE POS_System SHALL display the Image_Placeholder in product lists and in the product selection interface for Transactions using the same rendered dimensions as a Thumbnail

19. IF a Product_Image or Thumbnail fails to load in the user interface, THEN THE POS_System SHALL display the Image_Placeholder using the same rendered dimensions as the expected image and preserve the position and size of all surrounding interface elements

20. WHERE the viewport width is 768px or greater, THE POS_System SHALL render Thumbnails and the Image_Placeholder at 200 x 200 pixels in product lists and in the product selection interface for Transactions

21. WHERE the viewport width is less than 768px, THE POS_System SHALL render Thumbnails and the Image_Placeholder at maximum 80 x 80 pixels inside the card format lists specified in Requirement 6 without horizontal scrolling

22. WHEN a User selects the Thumbnail of a product in a product detail view, THE POS_System SHALL display the Product_Image of that product at maximum rendered dimensions of 1200 x 1200 pixels preserving the original aspect ratio

23. WHEN a User uploads, replaces, or deletes a Product_Image, THE POS_System SHALL record the operation in the Audit_Log with UTC timestamp (millisecond precision), user identifier, product identifier, image identifier, operation type (upload, replace, delete), original file name (1 to 255 characters), file size in bytes, and image dimensions in pixels

24. IF image storage fails during an upload or a replacement, THEN THE POS_System SHALL rollback the operation, preserve the previously stored Product_Image unchanged, and return error message "Image upload failed, please try again"

25. WHERE a product is marked as deactivated, THE POS_System SHALL preserve the stored Product_Image and Thumbnail of that product for historical records
### Requirement 17: Comprobantes de Venta

**User Story:** Como cajero, quiero emitir un comprobante impreso, en PDF o por email al completar una venta o una devolución, para entregar al cliente un respaldo de la operación y poder reimprimirlo cuando lo solicite.

#### Acceptance Criteria

1. WHEN a Transaction is successfully completed, THE POS_System SHALL generate a Receipt containing transaction identifier, transaction UTC timestamp (millisecond precision) rendered in the configured system time zone, business name and business address as configured in Requirement 5, user name of the completing User, customer name (where the Transaction is linked to a Customer), one line per Transaction line item with product name, quantity, unit price and line amount, subtotal, tax amount, discount amount, final amount, payment method, amount received, and change due, each monetary value with 2 decimal precision

2. WHERE the Transaction is paid partially or fully with store credit as specified in Requirement 9, THE POS_System SHALL include in the Receipt the applied store credit amount and the last 4 characters of the Store_Credit_Voucher code (where a voucher was used)

3. THE POS_System SHALL render a Receipt for output to a Thermal_Printer with 80 mm paper width, as a downloadable PDF file (page width 80 mm), and as an email attachment in PDF format

4. WHERE a Transaction is linked to a Customer with a stored email address, THE POS_System SHALL allow sending the Receipt to that email address

5. IF a User requests email delivery of a Receipt for a Transaction that is not linked to a Customer with a stored email address, THEN THE POS_System SHALL reject the request with error message "No customer email available for this transaction"

6. IF email delivery of a Receipt fails after 3 retry attempts, THEN THE POS_System SHALL log the failure in the Audit_Log and return error message "Unable to send receipt email, please retry or download the PDF"

7. WHERE a User has Cashier, Manager, or Administrator role, THE POS_System SHALL allow requesting a Receipt_Reprint of a previously completed Transaction or Return by providing the transaction identifier or the return identifier

8. IF a User requests a Receipt_Reprint with an identifier that does not match any completed Transaction or Return, THEN THE POS_System SHALL reject the request with error message "Receipt not found for the provided identifier"

9. WHEN a Receipt_Reprint is produced, THE POS_System SHALL include in the Receipt the text "REPRINT #[reprint count]" positioned above the line items and increment the stored reprint count of that Receipt by 1

10. WHERE a Transaction is marked as a Voided_Transaction as specified in Requirement 20, THE POS_System SHALL include in every Receipt_Reprint of that Transaction the text "VOIDED TRANSACTION"

11. WHEN a Return is successfully completed, THE POS_System SHALL generate a Receipt containing return identifier, original transaction identifier, return UTC timestamp (millisecond precision) rendered in the configured system time zone, business name and business address, user name of the completing User, returned line items with product name, return quantity, unit price and line refund amount, total refund amount with 2 decimal precision, refund method, and Store_Credit_Voucher code (where refund method is store credit and a voucher was generated)

12. IF Receipt output to a Thermal_Printer fails, THEN THE POS_System SHALL preserve the completed Transaction or the completed Return without reversal, display error message "Receipt printing failed. Retry printing, download PDF, or continue without receipt", and offer the options retry printing, download PDF, and continue without receipt

13. IF a User selects the option continue without receipt, THEN THE POS_System SHALL close the Receipt output step and record the outcome in the Audit_Log

14. WHERE a User has Administrator role, THE POS_System SHALL allow configuration of a Receipt_Footer_Text of 0 to 200 characters

15. WHERE a Receipt_Footer_Text of 1 or more characters is configured, THE POS_System SHALL print the Receipt_Footer_Text as the last content block of every Receipt

16. IF an Administrator submits a Receipt_Footer_Text longer than 200 characters, THEN THE POS_System SHALL reject the modification with error message "Receipt footer text cannot exceed 200 characters"

17. WHEN a Receipt is emitted and WHEN a Receipt_Reprint is produced, THE POS_System SHALL record the operation in the Audit_Log with UTC timestamp (millisecond precision), user identifier, transaction identifier or return identifier, output channel (thermal printer, PDF, or email), operation type (emission or reprint), and reprint count

### Requirement 18: Búsqueda de Productos y Código de Barras

**User Story:** Como cajero, quiero buscar productos por código de barras, SKU o nombre, para agregar productos a la venta de forma rápida y sin errores de tipeo.

#### Acceptance Criteria

1. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow storing an optional Barcode of 1 to 48 characters for each product in EAN-13, UPC-A, or Code 128 format

2. IF a submitted Barcode does not match the character set and length of EAN-13 (13 numeric digits), UPC-A (12 numeric digits), or Code 128 (1 to 48 characters from the printable ASCII range 32 to 126), THEN THE POS_System SHALL reject the operation with error message "Invalid barcode format. Allowed formats: EAN-13, UPC-A, Code 128"

3. IF a submitted EAN-13 or UPC-A Barcode has an invalid check digit, THEN THE POS_System SHALL reject the operation with error message "Invalid barcode check digit"

4. THE POS_System SHALL enforce Barcode uniqueness across all products including deactivated products

5. IF a User attempts to store a Barcode that is already assigned to another product, THEN THE POS_System SHALL reject the operation with error message "Barcode already assigned to product [product name]"

6. WHERE a User has Cashier, Manager, or Administrator role, THE POS_System SHALL allow searching products by Barcode (exact match), by SKU (exact match), and by product name (partial match, case insensitive and accent insensitive)

7. WHEN a User submits a product search by Barcode or by SKU, THE POS_System SHALL return the matching product within 1 second

8. WHEN a User submits a product search by product name, THE POS_System SHALL return up to 50 matching products ordered by product name within 2 seconds

9. IF a product search by product name matches more than 50 products, THEN THE POS_System SHALL return the first 50 products and display message "Showing 50 of [match count] matches. Refine the search terms"

10. IF a product search by Barcode, SKU, or product name matches zero products, THEN THE POS_System SHALL display message "No products found for the provided search terms"

11. WHEN a User submits a Barcode during an open Transaction and the Barcode matches an active product that is not yet present in the Transaction, THE POS_System SHALL add that product as a line item with quantity 1

12. WHEN a User submits a Barcode during an open Transaction and the Barcode matches an active product already present as a line item of that Transaction, THE POS_System SHALL increment the quantity of that line item by 1

13. IF a User submits a Barcode during an open Transaction that matches no product, THEN THE POS_System SHALL reject the addition with error message "Barcode not found"

14. IF a User submits a Barcode during an open Transaction that matches a deactivated product, THEN THE POS_System SHALL reject the addition with error message "Product is no longer available" as specified in Requirement 10 criterion 5

15. IF adding or incrementing a line item from a Barcode would exceed the current inventory quantity of that product, THEN THE POS_System SHALL reject the addition with error message "Insufficient inventory: [available quantity] available" as specified in Requirement 9 criterion 5

16. IF incrementing a line item from a Barcode would exceed the maximum line item quantity of 9999, THEN THE POS_System SHALL reject the increment with error message "Line item quantity cannot exceed 9999"

17. WHERE a User has Manager or Administrator role and a product has no stored Barcode, THE POS_System SHALL allow generating a Code 128 Barcode of 12 characters for that product and storing it as the Barcode of that product

18. WHEN the POS_System generates a Barcode, THE POS_System SHALL verify uniqueness of the generated value across all products before storing it

19. WHEN a Barcode is stored, modified, or generated, THE POS_System SHALL record the operation in the Audit_Log with UTC timestamp (millisecond precision), user identifier, product identifier, previous Barcode value, and new Barcode value

### Requirement 19: Descuentos en Transacciones

**User Story:** Como cajero, quiero aplicar descuentos por línea o sobre el total de la venta con un motivo registrado, para atender promociones y situaciones especiales dentro de los límites autorizados por la gerencia.

#### Acceptance Criteria

1. WHERE a User has Cashier, Manager, or Administrator role and a Transaction is open, THE POS_System SHALL allow applying a Discount to an individual line item as a percentage from 0.00 to 100.00 with 2 decimal precision or as a fixed amount from 0.00 to the line item amount with 2 decimal precision

2. WHERE a User has Cashier, Manager, or Administrator role and a Transaction is open, THE POS_System SHALL allow applying a Discount to the Transaction total as a percentage from 0.00 to 100.00 with 2 decimal precision or as a fixed amount from 0.00 to the Transaction subtotal with 2 decimal precision

3. WHEN a Discount is applied, THE POS_System SHALL calculate the discount amount of the Transaction as the sum of all line item discount amounts plus the Transaction total discount amount with 2 decimal precision and use that value as the discount amount specified in Requirement 9 criterion 3

4. IF a User submits a Discount percentage outside the range 0.00 to 100.00 or with more than 2 decimal places, THEN THE POS_System SHALL reject the Discount with error message "Discount percentage must be between 0.00% and 100.00% with maximum 2 decimal places"

5. IF a User submits a fixed Discount amount that exceeds the line item amount or the Transaction subtotal to which it applies, THEN THE POS_System SHALL reject the Discount with error message "Discount amount cannot exceed [maximum discount amount]"

6. THE POS_System SHALL calculate the final amount of a Transaction as a value greater than or equal to 0.00

7. IF the sum of applied Discounts would result in a final amount less than 0.00, THEN THE POS_System SHALL reject the Discount with error message "Discount would result in a negative total"

8. WHERE a User has Administrator role, THE POS_System SHALL allow configuration of the Discount_Limit for the Cashier role as a percentage from 0.00 to 100.00 with 2 decimal precision

9. WHERE no Administrator has configured the Discount_Limit for the Cashier role, THE POS_System SHALL apply a Discount_Limit of 10.00 percent for the Cashier role

10. WHERE a User has Manager or Administrator role, THE POS_System SHALL apply a Discount_Limit of 100.00 percent for that User

11. WHEN a User applies a Discount whose percentage of the affected line item amount or of the Transaction subtotal exceeds the Discount_Limit of the Role of that User, THE POS_System SHALL require a Discount_Authorization from a User with Manager or Administrator role before accepting the Discount

12. IF a Discount requiring Discount_Authorization is submitted without valid credentials of a User with Manager or Administrator role, THEN THE POS_System SHALL reject the Discount with error message "Discount of [discount percentage]% exceeds your limit of [discount limit]%. Manager authorization required"

13. WHEN a Discount_Authorization is granted, THE POS_System SHALL record the user identifier of the authorizing User in the Transaction record

14. WHEN a User applies a Discount, THE POS_System SHALL require a Discount_Reason selected from the list promotion, frequent customer, damaged product, management authorization, other, and allow optional notes of 0 to 200 characters

15. IF a User applies a Discount without selecting a Discount_Reason, THEN THE POS_System SHALL reject the Discount with error message "Discount reason is required"

16. IF a Discount results in a line item unit price after discount that is less than the Cost_Price recorded for that product as specified in Requirement 15, THEN THE POS_System SHALL require explicit confirmation with message "Discounted price [discounted unit price] is below cost price [cost price] (loss of [loss amount] per unit). Confirm sale at a loss?" before accepting the Discount

17. IF a User declines the below cost price confirmation, THEN THE POS_System SHALL discard the submitted Discount and preserve the previous Discount values of the Transaction unchanged

18. WHEN a Discount is applied to an open Transaction and WHEN a Transaction with Discounts is completed, THE POS_System SHALL record the operation in the Audit_Log with UTC timestamp (millisecond precision), transaction identifier, line item identifier (where the Discount applies to a line item), discount amount, discount percentage, Discount_Reason, notes, user identifier of the applying User, and user identifier of the authorizing User (where a Discount_Authorization was granted)

19. WHERE a User has Manager or Administrator role, THE Report_Engine SHALL report total discount amount granted for a requested date range of maximum 366 days aggregated by user identifier, by Discount_Reason, and by day, with 2 decimal precision

20. IF a discount report request matches zero Discounts, THEN THE Report_Engine SHALL return an empty report with message "No data found for specified criteria"

### Requirement 20: Anulación de Transacciones

**User Story:** Como gerente, quiero anular transacciones del día en curso mientras el turno sigue abierto, para corregir errores de caja sin borrar información y manteniendo la trazabilidad completa.

#### Acceptance Criteria

1. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow performing a Void of a completed Transaction whose Operating_Day equals the current Operating_Day and whose recorded shift identifier corresponds to a Shift that is still open

2. IF a User with Cashier role attempts to perform a Void, THEN THE POS_System SHALL reject the operation with error message "Manager authorization required to void transactions"

3. IF a User attempts to perform a Void of a Transaction whose Operating_Day is earlier than the current Operating_Day, THEN THE POS_System SHALL reject the operation with error message "Transaction belongs to a closed operating day. Process a return instead"

4. IF a User attempts to perform a Void of a Transaction whose recorded shift identifier corresponds to a closed Shift, THEN THE POS_System SHALL reject the operation with error message "Shift is already closed. Process a return instead"

5. WHEN a User performs a Void, THE POS_System SHALL require a void reason selected from the list cashier error, customer cancellation, pricing error, duplicate transaction, other, and mandatory notes of 1 to 500 characters

6. IF a User submits a Void without a void reason or with notes shorter than 1 character or longer than 500 characters, THEN THE POS_System SHALL reject the operation with error message "Void reason and notes of 1 to 500 characters are required"

7. WHEN a Void is successfully performed, THE POS_System SHALL atomically increment inventory quantities for all line item products of the Transaction by their sold quantities

8. WHEN a Void is successfully performed for a Transaction with a cash component, THE POS_System SHALL subtract the cash amount of that Transaction from the expected cash balance of the affected Shift as specified in Requirement 12 criterion 8

9. WHEN a Void is successfully performed for a Transaction paid partially or fully with store credit, THE POS_System SHALL restore the applied store credit amount to the Customer Store_Credit balance or restore the Store_Credit_Voucher to unused state with its original expiration date

10. WHEN a Void is successfully performed, THE POS_System SHALL mark the Transaction as a Voided_Transaction and preserve the Transaction record, its line items, and its Receipt history in storage

11. IF a User attempts to perform a Void of a Voided_Transaction, THEN THE POS_System SHALL reject the operation with error message "Transaction is already voided"

12. IF a User attempts to initiate a Return for a Voided_Transaction as specified in Requirement 11, THEN THE POS_System SHALL reject the Return with error message "Transaction is voided and cannot be returned"

13. IF a User attempts to perform a Void of a Transaction that has one or more completed Returns, THEN THE POS_System SHALL reject the operation with error message "Transaction has returns and cannot be voided"

14. WHERE a Transaction is marked as a Voided_Transaction, THE Report_Engine SHALL exclude that Transaction from total sales amount, transaction count, average transaction value, and Gross_Margin calculations

15. WHERE a Transaction is marked as a Voided_Transaction, THE Dashboard SHALL exclude that Transaction from all Chart_Widget metrics

16. WHERE a Transaction is marked as a Voided_Transaction, THE POS_System SHALL display that Transaction in transaction history and in Customer purchase history with void status, void UTC timestamp, and voiding user name

17. WHEN a Void is successfully performed, THE POS_System SHALL record the operation in the Audit_Log with UTC timestamp (millisecond precision), user identifier of the voiding User, transaction identifier, void reason, notes, voided amount with 2 decimal precision, payment method of the voided Transaction, and shift identifier of the affected Shift

18. IF inventory update fails for any product during a Void, THEN THE POS_System SHALL rollback the entire Void, preserve the Transaction as completed, and return error message "Void failed, inventory could not be restored"

19. WHERE a User has Manager or Administrator role, THE POS_System SHALL allow viewing a list of Voided_Transactions filtered by date range (maximum 366 days), user identifier, and void reason
