using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DoableFinal.Models;
using DoableFinal.ViewModels;

namespace DoableFinal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }
            else if (User.IsInRole("Project Manager"))
            {
                return RedirectToAction("Index", "ProjectManager");
            }
            else if (User.IsInRole("Employee"))
            {
                return RedirectToAction("Index", "Employee");
            }
            else if (User.IsInRole("Client"))
            {
                return RedirectToAction("Index", "Client");
            }
        }

        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Services()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Contact(ContactViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Here you would typically send an email or save the contact form
            TempData["SuccessMessage"] = "Thank you for your message. We'll get back to you soon!";
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
