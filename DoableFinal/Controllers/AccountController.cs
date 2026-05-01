using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using System.Net;
using System.Net.Mail;
namespace DoableFinal.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var phoneAttr = new DoableFinal.Validation.PhonePHAttribute();
                var tinAttr = new DoableFinal.Validation.TinAttribute();

                if (!string.IsNullOrEmpty(model.MobileNumber) && !phoneAttr.IsValid(model.MobileNumber))
                {
                    ModelState.AddModelError("MobileNumber", "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits). ");
                    return View(model);
                }

                if (!string.IsNullOrEmpty(model.TinNumber) && !tinAttr.IsValid(model.TinNumber))
                {
                    ModelState.AddModelError("TinNumber", "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.");
                    return View(model);
                }
                // Additional server-side check in case the MinAge validation attribute is bypassed
                if (model.Birthday > DateTime.Today.AddYears(-18))
                {
                    ModelState.AddModelError("Birthday", "You must be at least 18 years old to register.");
                    return View(model);
                }
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Role = "Client",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Birthday = model.Birthday,
                    CompanyName = model.CompanyName,
                    CompanyAddress = model.CompanyAddress,
                    Designation = model.Designation,
                    MobileNumber = model.MobileNumber,
                    TinNumber = model.TinNumber,
                    CompanyType = model.CompanyType
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Client");
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Dashboard");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    // Ensure user has appropriate Identity role based on their custom Role property
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    if (user != null && user.Role != null)
                    {
                        // Check if user has any Identity roles
                        var userRoles = await _userManager.GetRolesAsync(user);
                        if (userRoles == null || userRoles.Count == 0)
                        {
                            // No roles assigned, add the one from custom Role property
                            await _userManager.AddToRoleAsync(user, user.Role);

                            // Refresh the security principal to include the new role
                            await _signInManager.RefreshSignInAsync(user);
                        }
                    }

                    return await RedirectToLocal(returnUrl);
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                EmailNotificationsEnabled = user.EmailNotificationsEnabled,

                ResidentialAddress = user.ResidentialAddress ?? string.Empty,
                Birthday = user.Birthday,
                PagIbigAccount = user.PagIbigAccount ?? string.Empty,
                MobileNumber = user.MobileNumber ?? string.Empty,
                Position = user.Position ?? string.Empty,

                IsActive = user.IsActive,
                IsArchived = user.IsArchived,
                ArchivedAt = user.ArchivedAt
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Defensive validation for fields
            var phoneAttr = new DoableFinal.Validation.PhonePHAttribute();
            var tinAttr = new DoableFinal.Validation.TinAttribute();
            var pagIbigAttr = new DoableFinal.Validation.PagIbigAttribute();

            if (!string.IsNullOrWhiteSpace(model.MobileNumber) && !phoneAttr.IsValid(model.MobileNumber))
            {
                ModelState.AddModelError("MobileNumber", "Invalid Philippine mobile number. Expected format: 09XXXXXXXXX (11 digits). ");
                return View(model);
            }
            if (!string.IsNullOrWhiteSpace(model.TinNumber) && !tinAttr.IsValid(model.TinNumber))
            {
                ModelState.AddModelError("TinNumber", "Invalid TIN. Expected: XXX-XXX-XXX or XXX-XXX-XXX-XXX.");
                return View(model);
            }
            if (!string.IsNullOrWhiteSpace(model.PagIbigAccount) && !pagIbigAttr.IsValid(model.PagIbigAccount))
            {
                ModelState.AddModelError("PagIbigAccount", "Invalid Pag-IBIG MID. Expected 12 numeric digits.");
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Update basic information
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.EmailNotificationsEnabled = model.EmailNotificationsEnabled;

            // Additional fields
            user.ResidentialAddress = model.ResidentialAddress;
            user.Birthday = model.Birthday;
            user.PagIbigAccount = model.PagIbigAccount;
            user.Position = model.Position;
            user.MobileNumber = model.MobileNumber;

            user.IsActive = model.IsActive;
            user.IsArchived = model.IsArchived;
            user.ArchivedAt = model.ArchivedAt;

            // Update email if changed
            if (!string.IsNullOrEmpty(model.Email) && model.Email != user.Email)
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                if (!setEmailResult.Succeeded)
                {
                    foreach (var error in setEmailResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
                user.UserName = model.Email; // Update username to match email
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["ProfileMessage"] = "Profile updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["PasswordSuccessMessage"] = "Your password has been changed successfully.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        private async Task<IActionResult> RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                else if (await _userManager.IsInRoleAsync(user, "Client"))
                {
                    return RedirectToAction("Index", "Client");
                }
                else if (await _userManager.IsInRoleAsync(user, "Employee"))
                {
                    return RedirectToAction("Index", "Employee");
                }
                else if (await _userManager.IsInRoleAsync(user, "Project Manager") || await _userManager.IsInRoleAsync(user, "ProjectManager"))
                {
                    return RedirectToAction("Index", "ProjectManager");
                }
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        //--------->Forgot Password Section <-----
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            // 🔐 Do NOT reveal if user exists
            if (user == null)
                return RedirectToAction(nameof(ForgotPasswordConfirmation));

            // 🔥 IMPORTANT: invalidate all previous tokens
            await _userManager.UpdateSecurityStampAsync(user);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);

            var resetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { code = encodedToken, email = user.Email },
                protocol: HttpContext.Request.Scheme
            );

            var body = $@"
        <h2>Reset Your Password</h2>
        <p>Click the link below:</p>
        <a href='{resetLink}'>Reset Password</a>
        <br/><br/>
        <small>If you did not request this, ignore this email.</small>
    ";

            await SendEmail(user.Email, "Doable Password Reset", body);

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string code = null, string email = null)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(email))
            {
                return BadRequest("A code and email must be supplied for password reset.");
            }

            // ✅ Optional: prevent logged-in users from using reset link
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return RedirectToAction("ResetPasswordInvalid");
            }

            var decodedToken = WebUtility.UrlDecode(code);

            var isValidToken = await _userManager.VerifyUserTokenAsync(
                user,
                _userManager.Options.Tokens.PasswordResetTokenProvider,
                "ResetPassword",
                decodedToken
            );

            if (!isValidToken)
            {
                TempData["ResetEmail"] = email;
                return RedirectToAction("ResetPasswordInvalid");
            }

            return View(new ResetPasswordViewModel
            {
                Email = email,
                Code = code
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                return RedirectToAction("ResetPasswordConfirmation");

            var decodedToken = WebUtility.UrlDecode(model.Code);

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);

            if (result.Succeeded)
            {
                // ✅ Optional but recommended
                await _signInManager.SignOutAsync();

                return RedirectToAction("ResetPasswordConfirmation");
            }

            bool isTokenError = false;

            foreach (var error in result.Errors)
            {
                if (error.Code == "InvalidToken")
                {
                    isTokenError = true;
                }
                else
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            if (isTokenError)
            {
                TempData["ResetEmail"] = model.Email;
                return RedirectToAction("ResetPasswordInvalid");
            }

            return View(model);
        }
        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ResetPasswordInvalid()
        {
            return View();
        }



        [HttpPost]
        public async Task<IActionResult> ResendResetLink(string email)
        {
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("ForgotPassword");

            var user = await _userManager.FindByEmailAsync(email);

            // 🔐 Do NOT reveal if user exists
            if (user == null)
                return RedirectToAction("ForgotPasswordConfirmation");

            // 🔥 IMPORTANT: invalidate previous tokens
            await _userManager.UpdateSecurityStampAsync(user);

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);

            var resetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { code = encodedToken, email = user.Email },
                protocol: HttpContext.Request.Scheme
            );

            var body = $@"
        <h2>Reset Your Password</h2>
        <p>Click the link below:</p>
        <a href='{resetLink}'>Reset Password</a>
    ";

            await SendEmail(user.Email, "Doable Password Reset", body);

            return RedirectToAction("ForgotPasswordConfirmation");
        }
        private async Task SendEmail(string toEmail, string subject, string body)
        {
            var smtp = new SmtpClient("smtp.gmail.com")
            {
                Port = 587, 
                Credentials = new NetworkCredential(
                    "doablemailsender@gmail.com",
                    "wsib ogpp entn urnn"
                ),
                EnableSsl = true 
            };

            var message = new MailMessage
            {
                From = new MailAddress("doablemailsender@gmail.com", "Doable System"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            await smtp.SendMailAsync(message);
        }


    }
}
