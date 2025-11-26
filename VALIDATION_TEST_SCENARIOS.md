# Validation Fix - Test Scenarios

## Test Case 1: Creating Employee with Invalid Phone Number

### Steps:
1. Navigate to Admin > Users > Create User
2. Select Role: "Employee"
3. Fill in all fields
4. Phone Number: "1234567890" (invalid - should be 09XXXXXXXXX)
5. TIN: "123-456-789" (valid)
6. Pag-IBIG: "123456789012" (valid)
7. Click Create

### Expected Result:
✓ Form shows error: "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits)."
✓ Account is NOT created
✓ Form values are preserved for correction

---

## Test Case 2: Creating Employee with Invalid TIN

### Steps:
1. Navigate to Admin > Users > Create User
2. Select Role: "Employee"
3. Fill in all fields with valid data
4. TIN: "12-34-56" (invalid format)
5. Phone: "09123456789" (valid)
6. Pag-IBIG: "123456789012" (valid)
7. Click Create

### Expected Result:
✓ Form shows error: "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX."
✓ Account is NOT created
✓ Form values are preserved

---

## Test Case 3: Creating Employee with Invalid Pag-IBIG

### Steps:
1. Navigate to Admin > Users > Create User
2. Select Role: "Employee"
3. Fill in all fields with valid data
4. Pag-IBIG: "1234567890" (invalid - should be 12 digits)
5. Phone: "09123456789" (valid)
6. TIN: "123-456-789" (valid)
7. Click Create

### Expected Result:
✓ Form shows error: "Invalid Pag-IBIG MID. Expected 12 numeric digits."
✓ Account is NOT created
✓ Form values are preserved

---

## Test Case 4: Creating Employee with Missing Required Fields

### Steps:
1. Navigate to Admin > Users > Create User
2. Select Role: "Employee"
3. Fill in Name, Email, Password
4. Leave all required fields (Phone, TIN, Pag-IBIG, Birthday, Position, Address) empty
5. Click Create

### Expected Result:
✓ Multiple error messages appear for missing fields
✓ Account is NOT created
✓ User sees which fields are required

---

## Test Case 5: Creating Employee with All Valid Data

### Steps:
1. Navigate to Admin > Users > Create User
2. Select Role: "Employee"
3. Fill in all fields:
   - Name: John Doe
   - Email: john.doe@example.com
   - Password: SecurePass123!
   - Phone: 09123456789
   - TIN: 123-456-789
   - Pag-IBIG: 123456789012
   - Birthday: 01/15/1990
   - Position: Developer
   - Address: 123 Main St
4. Click Create

### Expected Result:
✓ Account created successfully
✓ Message: "Employee account created successfully."
✓ User redirected to Users list
✓ New user appears in the list

---

## Test Case 6: Verify Profile Displays All Fields

### Steps:
1. Create Employee with valid data (Test Case 5)
2. Log in as Admin
3. Navigate to Admin > Profile
4. Scroll to "Additional Information" section
5. Verify all fields are displayed and populated

### Expected Result:
✓ Phone number: 09123456789
✓ TIN: 123-456-789
✓ Pag-IBIG: 123456789012
✓ Birthday: 01/15/1990
✓ Position: Developer
✓ Address: 123 Main St

---

## Test Case 7: Updating Profile with Invalid Phone

### Steps:
1. Log in as Admin
2. Navigate to Profile
3. Update Phone Number to: "1111111111"
4. Click Save

### Expected Result:
✓ Error message: "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits)."
✓ Profile is NOT updated
✓ Original phone number remains

---

## Test Case 8: Creating Client with Valid Data

### Steps:
1. Navigate to Admin > Users > Create User
2. Select Role: "Client"
3. Fill in all fields:
   - Name: ABC Corporation
   - Email: contact@abc.com
   - Password: SecurePass123!
   - Phone: 09123456789
   - TIN: 123-456-789-012 (12-digit format)
   - Company: ABC Corp
   - Address: Corporate HQ
   - Designation: Manager
   - Type: Corporation
4. Click Create

### Expected Result:
✓ Client account created successfully
✓ Message: "Client account created successfully."
✓ User redirected to Users list

---

## Test Case 9: Frontend Bypass Prevention

### Steps:
1. Open browser DevTools (F12)
2. Navigate to Create User form
3. In DevTools, remove the pattern attribute from phone input:
   ```javascript
   document.querySelector('[name="MobileNumber"]').removeAttribute('pattern');
   ```
4. Submit form with invalid phone: "1234567890"
5. Observe backend validation

### Expected Result:
✓ Error message: "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits)."
✓ Account is NOT created
✓ Backend validation catches the invalid data despite frontend bypass

---

## Test Case 10: Empty Fields Bypass Prevention

### Steps:
1. Open browser DevTools
2. Remove required attributes from inputs:
   ```javascript
   document.querySelectorAll('input[required]').forEach(el => el.removeAttribute('required'));
   ```
3. Submit form with all employee fields empty
4. Observe backend validation

### Expected Result:
✓ Multiple error messages for missing required fields
✓ Account is NOT created
✓ Backend enforces required fields despite frontend removal

---

## Validation Format References

### Valid Phone Examples:
- 09123456789 ✓
- 09987654321 ✓
- 09111111111 ✓

### Invalid Phone Examples:
- 9123456789 ✗ (missing leading 0)
- 09-123-456-789 ✗ (has dashes)
- 08123456789 ✗ (wrong prefix)
- +639123456789 ✗ (country code format)

### Valid TIN Examples:
- 123-456-789 ✓
- 123456789 ✓
- 123-456-789-012 ✓
- 123456789012 ✓

### Valid Pag-IBIG Examples:
- 123456789012 ✓
- 100000000000 ✓
- 999999999999 ✓

### Invalid Pag-IBIG Examples:
- 12345678901 ✗ (11 digits)
- 1234567890123 ✗ (13 digits)
- 12345678-9012 ✗ (has dash)
