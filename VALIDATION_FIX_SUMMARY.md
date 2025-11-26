# Backend Validation Fix - Summary

## Changes Implemented

### 1. Created New Validation Attribute: RequiredIfAttribute
**File:** `Validation/RequiredIfAttribute.cs`

- Implements conditional required validation based on the role
- Validates that fields marked as required are not null/empty when the role matches specified roles
- Used for Employee, Project Manager, Admin, and Client roles

### 2. Updated CreateUserViewModel
**File:** `ViewModels/CreateUserViewModel.cs`

Added `[RequiredIf]` attributes to:
- `ResidentialAddress` - Required for Employee, Project Manager, Admin
- `MobileNumber` - Required for Employee, Project Manager, Admin, Client
- `Birthday` - Required for Employee, Project Manager, Admin
- `TinNumber` - Required for Employee, Project Manager, Admin, Client
- `PagIbigAccount` - Required for Employee, Project Manager, Admin
- `Position` - Required for Employee, Project Manager, Admin

Added `using DoableFinal.Validation;` directive

### 3. Updated ProfileViewModel
**File:** `ViewModels/ProfileViewModel.cs`

Added same `[RequiredIf]` attributes as CreateUserViewModel
Added `using DoableFinal.Validation;` directive

### 4. Enhanced Backend Validation in AdminController
**File:** `Controllers/AdminController.cs`

#### CreateUser Action:
- Added comprehensive validation for all required fields based on role
- For Employee/Project Manager/Admin roles:
  - Validates ResidentialAddress is provided
  - Validates MobileNumber is provided and has valid format
  - Validates Birthday is provided
  - Validates TinNumber is provided and has valid format
  - Validates PagIbigAccount is provided and has valid format
  - Validates Position is provided
- For Client roles:
  - Validates CompanyName, CompanyAddress, CompanyType, Designation
  - Validates MobileNumber is provided and has valid format
  - Validates TinNumber is provided and has valid format
- Prevents account creation if ANY required field is invalid
- Moved email existence check BEFORE user creation for better error handling
- Added early returns for validation failures to prevent partial account creation

#### Profile Action:
- Added strict backend validation similar to CreateUser
- Validates required fields based on user role
- Enforces format validation for phone, TIN, and Pag-IBIG numbers
- Prevents profile update with invalid data

## How It Works

### Frontend Validation
1. HTML5 input attributes (pattern, maxlength, type)
2. JavaScript client-side validation
3. Data annotation validation attributes

### Backend Validation (NEW)
1. **ModelState validation** - Checks for [Required] attributes
2. **Custom validation** - RequiredIfAttribute checks conditional requirements
3. **Explicit backend checks** - In controller action:
   - Checks each required field is not null/empty
   - Validates format using validation attribute methods
   - Returns error messages to view if any field fails
   - Prevents CreateAsync call if validation fails

### Security Benefits
- Account CANNOT be created with invalid phone/TIN/Pag-IBIG
- Invalid data is rejected at multiple layers
- Error messages guide users to correct format
- Profile data cannot be updated with invalid values

## Validation Rules

### Phone Number (MobileNumber)
- Pattern: 09XXXXXXXXX (11 digits)
- Example: 09123456789

### TIN Number
- Pattern: XXX-XXX-XXX or XXX-XXX-XXX-XXX
- Example: 123-456-789 or 123-456-789-012
- 9 or 12 digits (with or without dashes)

### Pag-IBIG Account
- Pattern: 12 numeric digits
- Example: 123456789012

## Testing Checklist

- [ ] Try creating Employee with invalid phone → should be rejected
- [ ] Try creating Employee with invalid TIN → should be rejected
- [ ] Try creating Employee with invalid Pag-IBIG → should be rejected
- [ ] Try creating Employee with all fields empty → should be rejected
- [ ] Create Employee with all valid data → should succeed
- [ ] Verify all fields display in Profile → Additional Info section
- [ ] Try updating profile with invalid phone → should be rejected
- [ ] Try updating profile with invalid TIN → should be rejected
- [ ] Create Client with valid phone and TIN → should succeed
- [ ] Verify Client profile shows phone and TIN correctly
