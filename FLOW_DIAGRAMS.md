# Validation Flow Diagram

## Account Creation Flow with Validation

```
┌─────────────────────────────────────────────────────────────────┐
│                     USER SUBMITS FORM                           │
│  Role: Employee, Phone: 09123456789, TIN: 123-456-789, etc.    │
└────────────────────┬────────────────────────────────────────────┘
                     │
                     ▼
        ┌──────────────────────────┐
        │  BROWSER / JAVASCRIPT    │
        │  • Check required fields │
        │  • Check patterns        │
        │  • Client-side messages  │
        └────────────┬─────────────┘
                     │
                     ▼
        ┌──────────────────────────┐
        │ Is data valid locally?   │
        │                          │
        │ Invalid ──→ Show error   │
        │ Valid ────▶ Continue     │
        └────────────┬─────────────┘
                     │
                     ▼
       ┌─────────────────────────────────┐
       │  FORM SUBMITTED TO SERVER       │
       │  POST /Admin/CreateUser         │
       └──────────┬──────────────────────┘
                  │
                  ▼
       ┌─────────────────────────────────┐
       │ LAYER 1: ModelState Validation  │
       │ • [Required] attributes         │
       │ • [RequiredIf] attributes       │
       │ • [EmailAddress] attribute      │
       └──────────┬──────────────────────┘
                  │
                  ├─→ Invalid? ──→ Return View with errors
                  │
                  ▼
       ┌─────────────────────────────────┐
       │ LAYER 2: Explicit Backend Check │
       │ • Password not empty            │
       │ • Birthday >= 18 years          │
       │ • Email not registered          │
       └──────────┬──────────────────────┘
                  │
                  ├─→ Invalid? ──→ Return View with errors
                  │
                  ▼
       ┌─────────────────────────────────┐
       │ LAYER 3: Role-Based Validation  │
       │                                 │
       │ For Employee/PM/Admin:          │
       │ ├─ Check MobileNumber not empty │
       │ ├─ Check TinNumber not empty    │
       │ ├─ Check PagIbigAccount not empty
       │ ├─ Check Position not empty     │
       │ ├─ Check Birthday not empty     │
       │ ├─ Check Address not empty      │
       │ └─ Validate all formats         │
       │                                 │
       │ For Client:                     │
       │ ├─ Check MobileNumber not empty │
       │ ├─ Check TinNumber not empty    │
       │ ├─ Check CompanyName not empty  │
       │ ├─ Validate formats             │
       │ └─ Other client-specific checks │
       └──────────┬──────────────────────┘
                  │
                  ├─→ Invalid? ──→ Return View with specific error
                  │
                  ▼
       ┌─────────────────────────────────┐
       │  Format Validation (Details)    │
       │                                 │
       │  Phone: phoneAttr.IsValid()     │
       │  ├─→ Match: ^09\d{9}$          │
       │  ├─→ Valid: 09123456789 ✓     │
       │  └─→ Invalid: 9123456789 ✗    │
       │                                 │
       │  TIN: tinAttr.IsValid()         │
       │  ├─→ Match: \d{3}-\d{3}-\d{3}$ │
       │  ├─→ Valid: 123-456-789 ✓     │
       │  └─→ Invalid: 12-34-56 ✗      │
       │                                 │
       │  Pag-IBIG: pagIbigAttr.IsValid()
       │  ├─→ Match: ^\d{12}$           │
       │  ├─→ Valid: 123456789012 ✓    │
       │  └─→ Invalid: 12345678901 ✗   │
       └──────────┬──────────────────────┘
                  │
                  ├─→ Invalid? ──→ Return View with format error
                  │
                  ▼
       ┌─────────────────────────────────┐
       │ ALL VALIDATIONS PASSED ✓        │
       │ Data is clean and safe          │
       └──────────┬──────────────────────┘
                  │
                  ▼
       ┌─────────────────────────────────┐
       │ Create ApplicationUser object   │
       │ with validated data             │
       └──────────┬──────────────────────┘
                  │
                  ▼
       ┌─────────────────────────────────┐
       │ _userManager.CreateAsync()      │
       │ • Write to database             │
       │ • Add to role                   │
       └──────────┬──────────────────────┘
                  │
                  ▼
       ┌─────────────────────────────────┐
       │ SUCCESS!                        │
       │ • Account created               │
       │ • User added to role            │
       │ • Redirect to Users list        │
       └─────────────────────────────────┘
```

---

## Invalid Input → Error Response Flow

```
┌──────────────────────────────────────────────┐
│ USER INPUT (INVALID)                         │
│ Phone: "1234567890" (missing 09 prefix)      │
└────────────────┬─────────────────────────────┘
                 │
                 ▼
        ┌────────────────────┐
        │ Frontend Validation │
        │ Pattern check fails │
        └────────────┬────────┘
                     │
                     ▼
        ┌────────────────────────────┐
        │ Show error in browser      │
        │ "Invalid format: 09XXXXXX"  │
        │ User sees it immediately   │
        └────────────┬───────────────┘
                     │
        But user could modify HTML...
                     │
                     ▼
   ┌─────────────────────────────┐
   │ POST with invalid data via  │
   │ DevTools or custom request  │
   └──────────┬──────────────────┘
              │
              ▼
   ┌──────────────────────────────┐
   │ BACKEND: Validation Layer 3  │
   │ phone validation check fails │
   └──────────┬───────────────────┘
              │
              ▼
   ┌──────────────────────────────┐
   │ Backend validation error:    │
   │ "Invalid phone format"       │
   │                              │
   │ ModelState.AddModelError()   │
   └──────────┬───────────────────┘
              │
              ▼
   ┌──────────────────────────────┐
   │ EARLY RETURN (KEY!)          │
   │ return View(model);          │
   │                              │
   │ Database write NEVER happens │
   │ User sees error on form      │
   └──────────────────────────────┘
```

---

## Validation Attribute Hierarchy

```
┌─────────────────────────────────────────────────────────┐
│                    VALIDATION LAYER                     │
└────────┬──────────────────────────────┬─────────────────┘
         │                              │
         ▼                              ▼
    ┌──────────┐              ┌────────────────┐
    │ Required │              │ RequiredIf     │
    │          │              │                │
    │ If null  │              │ If Role ==     │
    │ invalid  │              │ specific value │
    └──────────┘              └────────────────┘
         │                              │
         ▼                              ▼
    ┌──────────┐              ┌────────────────┐
    │ Format   │              │ Format         │
    │ Attr     │              │ Attr           │
    │          │              │                │
    │ Phone    │              │ Phone          │
    │ TIN      │              │ TIN            │
    │ Pag-IBIG │              │ Pag-IBIG       │
    └──────────┘              └────────────────┘

        ┌───────────────────────────┐
        │ Additional BackEnd Checks │
        │                           │
        │ • Null/empty checks       │
        │ • IsValid() method calls  │
        │ • Role-based logic        │
        │ • Early returns           │
        └───────────────────────────┘

              │
              ▼
        ┌─────────────────┐
        │ Account Created │
        │ (or Error View) │
        └─────────────────┘
```

---

## Data Flow for Valid User Creation

```
┌──────────────────────────────┐
│ FRONTEND FORM INPUT          │
├──────────────────────────────┤
│ Role: Employee               │
│ Name: John Doe               │
│ Email: john@example.com      │
│ Phone: 09123456789           │
│ TIN: 123-456-789             │
│ Pag-IBIG: 123456789012       │
│ Birthday: 1990-01-15         │
│ Position: Developer          │
│ Address: 123 Main St         │
└───────────┬──────────────────┘
            │
            ▼
    ┌──────────────────────────┐
    │ VALIDATION PASSED        │
    │ • All checks OK          │
    │ • All formats valid      │
    │ • All required fields OK │
    └───────────┬──────────────┘
                │
                ▼
    ┌──────────────────────────────┐
    │ CREATE ApplicationUser        │
    ├──────────────────────────────┤
    │ UserName: john@example.com   │
    │ Email: john@example.com      │
    │ FirstName: John              │
    │ LastName: Doe                │
    │ Role: Employee               │
    │ MobileNumber: 09123456789    │
    │ TinNumber: 123-456-789       │
    │ PagIbigAccount: 123456789012 │
    │ Birthday: 1990-01-15         │
    │ Position: Developer          │
    │ ResidentialAddress: 123 Main │
    └───────────┬──────────────────┘
                │
                ▼
    ┌──────────────────────────────┐
    │ DATABASE INSERT              │
    │ AspNetUsers table            │
    │ • All fields populated       │
    │ • Valid data saved           │
    └───────────┬──────────────────┘
                │
                ▼
    ┌──────────────────────────────┐
    │ USER ROLES TABLE INSERT      │
    │ Associate user with role     │
    └───────────┬──────────────────┘
                │
                ▼
    ┌──────────────────────────────┐
    │ SUCCESS                      │
    │ ✓ Account created            │
    │ ✓ Role assigned              │
    │ ✓ Data persisted             │
    │ ✓ Profile will display all   │
    └──────────────────────────────┘
```

---

## Profile Display After Valid Account Creation

```
┌─────────────────────────────────────┐
│       USER PROFILE - Additional      │
│            Information               │
├─────────────────────────────────────┤
│ Phone Number:    09123456789 ✓      │
│ TIN:             123-456-789 ✓      │
│ Pag-IBIG:        123456789012 ✓    │
│ Birthday:        January 15, 1990 ✓ │
│ Position:        Developer ✓        │
│ Address:         123 Main St ✓      │
├─────────────────────────────────────┤
│ All fields populated from database   │
│ All data successfully saved          │
└─────────────────────────────────────┘

vs. BEFORE FIX (Invalid data):

┌─────────────────────────────────────┐
│       USER PROFILE - Additional      │
│            Information               │
├─────────────────────────────────────┤
│ Phone Number:    [BLANK] ✗          │
│ TIN:             [BLANK] ✗          │
│ Pag-IBIG:        [BLANK] ✗          │
│ Birthday:        [BLANK] ✗          │
│ Position:        Developer ✓        │
│ Address:         123 Main St ✓      │
├─────────────────────────────────────┤
│ Invalid phone/TIN/Pag-IBIG not saved │
│ despite account creation            │
└─────────────────────────────────────┘
```

---

## Security: Frontend Bypass Prevention

```
┌──────────────────────────────────────────┐
│ ATTACKER OPENS DEVTOOLS (F12)            │
│                                          │
│ javascript:                              │
│ document.querySelector('[name]').       │
│   removeAttribute('required')            │
│ document.querySelector('[pattern]').    │
│   removeAttribute('pattern')             │
│                                          │
│ Submits form with: Phone: "1234567890"  │
└─────────────────┬────────────────────────┘
                  │
                  ▼
        ┌────────────────────────┐
        │ Frontend bypass works  │
        │ Form submits data      │
        └────────────┬───────────┘
                     │
                     ▼
      ┌──────────────────────────────┐
      │ BACKEND VALIDATION CATCHES IT │
      │                              │
      │ Layer 3: Format validation   │
      │ phone: "1234567890"          │
      │ phoneAttr.IsValid()?         │
      │ ├─ Match ^09\d{9}$ ? NO      │
      │ └─ Return false              │
      └──────────┬───────────────────┘
                 │
                 ▼
      ┌──────────────────────────────┐
      │ Add validation error         │
      │ Return View (NOT redirecting)│
      │                              │
      │ User sees error message      │
      │ Database write PREVENTED     │
      └──────────────────────────────┘

RESULT: Attack PREVENTED ✓
```

---

## Comparison: Before vs After

```
BEFORE FIX:
═══════════════════════════════════════════════

Form with invalid phone → Submit
    ↓
JavaScript validation → Pass (or bypass)
    ↓
Backend: ModelState.IsValid → True
    ↓
Backend validation:
    if (!string.IsNullOrEmpty(phone))  {
        if (!isValid) return error
    }
    ↓
    Empty phone? ALLOWED ✗
    Invalid format? SOMETIMES ALLOWED ✗
    ↓
Account created with bad/empty data
    ↓
Profile shows blank fields ✗


AFTER FIX:
═══════════════════════════════════════════════

Form with invalid phone → Submit
    ↓
JavaScript validation → Error (show tooltip)
    ↓
Backend: ModelState.IsValid → False
    (RequiredIf validator runs)
    ↓
Backend validation:
    For Employee role:
        if (string.IsNullOrWhiteSpace(phone))
            return error;
        if (!isValid)
            return error;
    ↓
    Empty phone? REJECTED ✓
    Invalid format? REJECTED ✓
    ↓
Account NOT created, error displayed
    ↓
User corrects format
    ↓
Valid data submitted
    ↓
Account created with valid data
    ↓
Profile shows all fields correctly ✓
```

