# Backend Validation Implementation Guide

## Overview
This fix implements **strict backend validation** for TIN, Pag-IBIG, and phone number fields to prevent account creation with invalid data, even if frontend validation is bypassed.

## Problem Addressed
- Frontend-only validation could be bypassed by modifying HTML or removing constraints
- Invalid data could still be saved to the database
- Accounts were created even when fields had incorrect formats
- Profile display showed empty fields when invalid data wasn't saved

## Solution Architecture

### Three-Layer Validation Approach

```
┌─────────────────────────────────────────────┐
│  Layer 1: Frontend Validation              │
│  - HTML5 patterns and required attributes   │
│  - JavaScript client-side checks           │
│  - Provides immediate user feedback        │
└─────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────┐
│  Layer 2: Model Validation                 │
│  - [Required] attributes                   │
│  - [RequiredIf] conditional attributes     │
│  - Format validation attributes            │
└─────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────┐
│  Layer 3: Backend Explicit Validation      │
│  - Programmatic null/empty checks          │
│  - Format validation using validators      │
│  - Role-based requirement enforcement      │
│  - Early returns prevent db operations     │
└─────────────────────────────────────────────┘
```

## Components

### 1. RequiredIfAttribute (New)
**Location:** `Validation/RequiredIfAttribute.cs`

```csharp
[RequiredIf("Role", new[] { "Employee", "Project Manager", "Admin" })]
public string? MobileNumber { get; set; }
```

- Validates field is required when Role matches specified values
- Works with ModelState validation
- Provides conditional requirement validation
- Error message specifies which role requires the field

### 2. Updated Validation Attributes
**Locations:**
- `Validation/PhonePHAttribute.cs`
- `Validation/TinAttribute.cs`
- `Validation/PagIbigAttribute.cs`

Features:
- Allow null values (let [Required] handle presence)
- Validate format when values are provided
- Used by both frontend and backend

### 3. CreateUserViewModel
**Location:** `ViewModels/CreateUserViewModel.cs`

Enhanced with:
- `[RequiredIf]` attributes for conditional requirements
- Role-specific field validation
- Clear error messages

### 4. AdminController.CreateUser()
**Location:** `Controllers/AdminController.cs` (line 247)

Strict validation sequence:
1. Check ModelState validity
2. Validate password
3. Validate birthday age
4. For Employee/PM/Admin roles:
   - Check each required field is not empty
   - Validate format using validators
   - Return with error if ANY field fails
5. For Client roles:
   - Similar checks
6. Check email doesn't exist
7. Only THEN create user account

## Validation Rules Reference

### Phone Number
- **Field:** MobileNumber
- **Pattern:** `^09\d{9}$` (11 digits starting with 09)
- **Examples:**
  - Valid: 09123456789
  - Invalid: 9123456789, 08123456789, 09-123-456-789
- **Required for:** Employee, Project Manager, Admin, Client

### TIN Number
- **Field:** TinNumber
- **Pattern:** `^\d{3}-\d{3}-\d{3}(-\d{3})?$`
- **Format:** XXX-XXX-XXX or XXX-XXX-XXX-XXX
- **Digits:** 9 or 12 (dashes optional for matching)
- **Examples:**
  - Valid: 123-456-789, 123456789, 123-456-789-012, 123456789012
  - Invalid: 12-34-56, 123.456.789, 123 456 789
- **Required for:** Employee, Project Manager, Admin, Client

### Pag-IBIG Account
- **Field:** PagIbigAccount
- **Pattern:** `^\d{12}$` (exactly 12 digits)
- **Examples:**
  - Valid: 123456789012, 100000000000
  - Invalid: 12345678901, 1234567890123, 123456-789012
- **Required for:** Employee, Project Manager, Admin

## Backend Validation Flow (CreateUser)

```
Request received with form data
     ↓
[1] Check ModelState.IsValid
    └─→ If invalid, return View with errors
     ↓
[2] Validate password not empty
    └─→ If invalid, return with error
     ↓
[3] Validate birthday age >= 18
    └─→ If invalid, return with error
     ↓
[4] Role-based validation
    ├─→ If Employee/PM/Admin:
    │   ├─→ Check ResidentialAddress not empty
    │   ├─→ Check MobileNumber not empty
    │   ├─→ Check Birthday not empty
    │   ├─→ Check TinNumber not empty
    │   ├─→ Check PagIbigAccount not empty
    │   ├─→ Check Position not empty
    │   ├─→ Validate MobileNumber format
    │   ├─→ Validate TinNumber format
    │   └─→ Validate PagIbigAccount format
    │   └─→ If any fails, return with error
    │
    └─→ If Client:
        ├─→ Check CompanyName not empty
        ├─→ Check CompanyAddress not empty
        ├─→ Check CompanyType not empty
        ├─→ Check Designation not empty
        ├─→ Check MobileNumber not empty
        ├─→ Check TinNumber not empty
        ├─→ Validate MobileNumber format
        └─→ Validate TinNumber format
        └─→ If any fails, return with error
     ↓
[5] Check email doesn't already exist
    └─→ If exists, return with error
     ↓
[6] Create ApplicationUser object with validated data
     ↓
[7] Call _userManager.CreateAsync()
     ↓
[8] Add user to role
     ↓
Success! Account created with validated data
```

## Security Features

1. **Multiple validation layers** - No single bypass point
2. **Backend enforcement** - Server validates regardless of frontend
3. **Early returns** - Validation failures prevent database operations
4. **Format validation** - Invalid formats rejected before save
5. **Null/empty checks** - Required fields cannot be bypassed
6. **Logging** - Validation errors logged for audit trail
7. **Error messages** - Clear feedback to user without info leakage

## Error Messages to Users

When validation fails, users see:
- Specific field causing the error
- Expected format (e.g., "09XXXXXXXXX")
- Which role requires this field

Examples:
- "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits)."
- "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX."
- "Invalid Pag-IBIG MID. Expected 12 numeric digits."
- "Mobile Number is required."

## Testing Recommendations

### Unit Testing
```csharp
[TestMethod]
public void CreateUser_InvalidPhone_ReturnsValidationError()
{
    var model = new CreateUserViewModel 
    { 
        Role = "Employee",
        MobileNumber = "1234567890",  // Invalid
        // ... other fields ...
    };
    
    var result = controller.CreateUser(model).Result;
    
    Assert.IsInstanceOfType(result, typeof(ViewResult));
    Assert.IsFalse(((ViewResult)result).ModelState.IsValid);
}
```

### Manual Testing
1. Try to create user with invalid phone - should fail
2. Try to create user with invalid TIN - should fail
3. Try to bypass frontend validation via DevTools - backend should catch it
4. Create valid user - should succeed
5. Verify profile displays all saved fields

### Security Testing
1. Modify HTML to remove pattern attributes - submission should still fail
2. Submit raw form data with invalid values - server rejects
3. Inspect database - no invalid data exists
4. Check logs - validation failures logged

## Migration Guide

If updating existing code:

1. **Add RequiredIfAttribute.cs** to Validation folder
2. **Update CreateUserViewModel.cs** - Add using and [RequiredIf] attributes
3. **Update ProfileViewModel.cs** - Add using and [RequiredIf] attributes
4. **Update AdminController.CreateUser()** - Add backend validation checks
5. **Update AdminController.Profile()** - Add backend validation checks
6. **Rebuild solution** - Verify no compilation errors
7. **Run tests** - Verify validation works

## Performance Impact

Minimal:
- Additional null/empty string checks
- Validation methods already compiled (same as frontend)
- No new database queries
- Early returns prevent unnecessary operations

## Backward Compatibility

- Existing valid accounts unaffected
- Existing invalid accounts remain (historical data)
- Future account creation enforced strictly
- No database schema changes required

## Troubleshooting

### Issue: Build fails with "RequiredIfAttribute not found"
**Solution:** Ensure `using DoableFinal.Validation;` is added to ViewModels

### Issue: Validation errors not appearing
**Solution:** Verify ModelState.IsValid check happens before explicit backend validation

### Issue: Valid data rejected
**Solution:** Check validation regex patterns match expected formats

### Issue: Invalid data still saved
**Solution:** Verify backend validation returns early before CreateAsync call

## Future Enhancements

1. **Custom validation messages** - Per-user-type messages
2. **Async validation** - Database checks (duplicate phone)
3. **Field dependencies** - Validate based on other field values
4. **Localization** - Error messages in multiple languages
5. **Audit trail** - Track all validation failures
6. **Analytics** - Monitor common validation failure patterns
