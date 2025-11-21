using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using DoableFinal.Data;
using DoableFinal.Services;
using Microsoft.EntityFrameworkCore;

namespace DoableFinal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ContentService _contentService;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, NotificationService notificationService, ContentService contentService)
    {
        _logger = logger;
        _context = context;
        _notificationService = notificationService;
        _contentService = contentService;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var page = _contentService?.GetPage("Index");
        return View(page);
    }

    public IActionResult About()
    {
        var page = _contentService?.GetPage("About");
        return View(page);
    }

    public IActionResult Services()
    {
        var page = _contentService?.GetPage("Services");
        return View(page);
    }

    public IActionResult Contact()
    {
        var page = _contentService?.GetPage("Contact");
        ViewBag.ContentPage = page;
        return View(new ContactViewModel());
    }

    [HttpPost]
    public IActionResult Contact(ContactViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Save inquiry into database
            var inquiry = new Inquiry
            {
                Name = model.Name,
                Email = model.Email,
                Subject = model.Subject,
                Message = model.Message,
                CreatedAt = DateTime.UtcNow,
                IsHandled = false
            };

            _context.Inquiries.Add(inquiry);
            _context.SaveChanges();

            // Notify all admins via NotificationService
            var admins = _context.Users.Where(u => u.Role == "Admin" && !u.IsArchived).ToList();
            foreach (var admin in admins)
            {
                // Create an in-app notification
                _notificationService.CreateNotification(admin.Id, "New Inquiry", $"New inquiry from {inquiry.Name}: {inquiry.Subject}", "/Admin/Inquiries").GetAwaiter().GetResult();
                // Optionally: send email (if SMTP configured)
                // _notificationService.SendEmailNotificationAsync(admin.Email, "New Inquiry Received", $"You have a new inquiry from {inquiry.Name} ({inquiry.Email}).\n\nSubject: {inquiry.Subject}\n\nMessage:\n{inquiry.Message}").GetAwaiter().GetResult();
            }

            TempData["ContactMessage"] = "Thank you for your message. We'll get back to you soon!";
            return RedirectToAction(nameof(Contact));
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
