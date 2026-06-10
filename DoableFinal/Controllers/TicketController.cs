using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using DoableFinal.Services;
using System;
using System.IO;

namespace DoableFinal.Controllers
{
    [Authorize]
    public class TicketController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly NotificationService _notificationService;

        public TicketController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
        }        [Authorize(Roles = "Client,Admin,Project Manager")]

        private async Task ReloadFormData(CreateTicketViewModel model, string clientId)
        {
            var projects = await _context.Projects
                .Where(p => p.ClientId == clientId && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            model.Projects = projects.Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name
            }).ToList();

          
            model.Assignees = new List<SelectListItem>();

            model.PriorityLevels = new List<SelectListItem>
    {
        new SelectListItem { Value = "Low", Text = "Low" },
        new SelectListItem { Value = "Medium", Text = "Medium" },
        new SelectListItem { Value = "High", Text = "High" },
        new SelectListItem { Value = "Critical", Text = "Critical" }
    };

            model.TicketTypes = new List<SelectListItem>
    {
        new SelectListItem { Value = "Bug", Text = "Bug" },
        new SelectListItem { Value = "Feature Request", Text = "Feature Request" },
        new SelectListItem { Value = "Support", Text = "Support" },
        new SelectListItem { Value = "Other", Text = "Other" }
    };
        }
        public async Task<IActionResult> Index(
    string? q = "",
    string? statusFilter = "",
    string? fromDate = "",
    string? toDate = "")
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return Challenge();
            }

            var query = _context.Tickets
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .Include(t => t.Project)
                .AsQueryable();

            // Role-based filtering
            query = query.Where(t =>
                User.IsInRole("Admin") ||
                (User.IsInRole("Project Manager") &&
                    t.Project != null &&
                    t.Project.ProjectManagerId == currentUser.Id) ||
                (User.IsInRole("Client") &&
                    t.CreatedById == currentUser.Id)
            );

            // Search by title
            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchTerm = q.ToLower();

                query = query.Where(t =>
                    t.Title.ToLower().Contains(searchTerm) ||
                    (t.CreatedBy != null &&
                        (
                            t.CreatedBy.FirstName.ToLower().Contains(searchTerm) ||
                            t.CreatedBy.LastName.ToLower().Contains(searchTerm)
                        ))
                );
            }

            // Status Filter
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(t => t.Status == statusFilter);
            }

            // From Date Filter
            if (!string.IsNullOrWhiteSpace(fromDate) &&
                DateTime.TryParse(fromDate, out var startDate))
            {
                query = query.Where(t =>
                    t.CreatedAt.Date >= startDate.Date);
            }

            // To Date Filter
            if (!string.IsNullOrWhiteSpace(toDate) &&
                DateTime.TryParse(toDate, out var endDate))
            {
                query = query.Where(t =>
                    t.CreatedAt.Date <= endDate.Date);
            }

            var tickets = await query
                .OrderByDescending(t =>
                    t.Priority == "Critical" ? 4 :
                    t.Priority == "High" ? 3 :
                    t.Priority == "Medium" ? 2 :
                    t.Priority == "Low" ? 1 : 0)
                .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
                .ToListAsync();

            ViewBag.StatusFilter = statusFilter;
            ViewBag.SearchQuery = q;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            ViewBag.AvailableStatuses = new List<string>
    {
        "Open",
        "In Progress",
        "Resolved",
        "Closed"
    };

            var viewModel = new TicketListViewModel
            {
                Tickets = tickets,
                NotificationType = NotificationType.General
            };

            return View(viewModel);
        }
        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var projects = await _context.Projects
                .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var viewModel = new CreateTicketViewModel
            {
                Projects = projects.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToList(),
                
                Assignees = employees.Select(e => new SelectListItem
                {
                    Value = e.Id,
                    Text = $"{e.FirstName} {e.LastName}"
                }).ToList(),

                PriorityLevels = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Low", Text = "Low" },
                    new SelectListItem { Value = "Medium", Text = "Medium" },
                    new SelectListItem { Value = "High", Text = "High" },
                    new SelectListItem { Value = "Critical", Text = "Critical" }
                },

                TicketTypes = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Bug", Text = "Bug" },
                    new SelectListItem { Value = "Feature Request", Text = "Feature Request" },
                    new SelectListItem { Value = "Support", Text = "Support" },
                    new SelectListItem { Value = "Other", Text = "Other" }
                }
            };

            return View(viewModel);
        }      
        
        [HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Client")]
public async Task<IActionResult> Create(CreateTicketViewModel model)
{
    var debugInfo = new System.Text.StringBuilder();
    debugInfo.AppendLine($"ModelState.IsValid: {ModelState.IsValid}");

    ModelState.Remove("AssignedToId");
    ModelState.Remove("Assignees");

    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Challenge();

    if (!ModelState.IsValid)
    {
        debugInfo.AppendLine("Model validation failed");
        await ReloadFormData(model, user.Id);
        TempData["Debug"] = debugInfo.ToString();
        return View(model);
    }

    if (user == null)
    {
        TempData["Error"] = "User not found.";
        return RedirectToAction("Login", "Account");
    }

    try
    {
        debugInfo.AppendLine("Starting ticket creation process...");

       
        var ticket = new Ticket
        {
            Title = model.Title,
            Description = model.Description,
            Priority = string.IsNullOrEmpty(model.Priority) ? "Medium" : model.Priority,
            Status = "Open",
            Type = model.Type,
            ProjectId = model.ProjectId,
            CreatedById = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        debugInfo.AppendLine($"Created ticket object with Title: {ticket.Title}");

        await _context.Tickets.AddAsync(ticket);
        debugInfo.AppendLine("Added ticket to context");

        var saveResult = await _context.SaveChangesAsync();

            
                debugInfo.AppendLine($"SaveChangesAsync result: {saveResult}");

        if (saveResult <= 0)
        {
            throw new Exception("Failed to save ticket to database");
        }

        debugInfo.AppendLine($"Ticket saved successfully. ID: {ticket.Id}");

       
        debugInfo.AppendLine("Beginning notification process...");

      
        if (ticket.ProjectId.HasValue)
        {
            // Security: ensure the project actually belongs to this client
            var projectOwned = await _context.Projects
                .AnyAsync(p => p.Id == ticket.ProjectId && p.ClientId == user.Id);
            if (!projectOwned)
            {
                ModelState.AddModelError("ProjectId", "You can only link tickets to your own projects.");
                await ReloadFormData(model, user.Id);
                return View(model);
            }

            var project = await _context.Projects
                .FirstOrDefaultAsync(p => p.Id == ticket.ProjectId);

            if (project?.ProjectManagerId != null)
            {
                await _notificationService.CreateNotification(
                    project.ProjectManagerId,
                    "New Ticket Created",
                    $"New ticket for project {project.Name}: {ticket.Title}",
                    $"/Ticket/Details/{ticket.Id}"
                );
            }
        }

        // ✅ Notify Admins
        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins)
        {
            await _notificationService.CreateNotification(
                admin.Id,
                "New Ticket Created",
                $"A new ticket has been created: {ticket.Title}",
                $"/Ticket/Details/{ticket.Id}"
            );
        }
                await _context.SaveChangesAsync();
                TempData["TicketMessage"] = "Ticket created successfully.";

        // ✅ Keep debug logging
        try
        {
            System.IO.File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "doable_ticket_debug.log"),
                debugInfo.ToString() + Environment.NewLine
            );
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }
    catch (Exception ex)
    {
        debugInfo.AppendLine($"Error: {ex.Message}");

        TempData["Error"] = "An error occurred: " + ex.Message;
        TempData["Debug"] = debugInfo.ToString();

        await ReloadFormData(model, user?.Id ?? "");
        return View(model);
    }
}

        [Authorize(Roles = "Client,Admin,Project Manager")]
        public async Task<IActionResult> Details(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .Include(t => t.Project)
                .Include(t => t.Comments.OrderByDescending(c => c.CreatedAt))
                    .ThenInclude(c => c.CreatedBy)
                .Include(t => t.Attachments.OrderByDescending(a => a.UploadedAt))
                    .ThenInclude(a => a.UploadedBy)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Check if user has permission to view this ticket
            bool hasAccess = 
                User.IsInRole("Admin") || // Admin can view all tickets
                (User.IsInRole("Project Manager") && ticket.Project.ProjectManagerId == currentUser.Id) || // PM can view their project tickets
                (User.IsInRole("Client") && ticket.CreatedById == currentUser.Id); // Client can view their created tickets

            if (!hasAccess)
            {
                return Forbid();
            }

            var viewModel = new TicketDetailsViewModel
            {
                Ticket = ticket,
                Comments = ticket.Comments.ToList(),
                Attachments = ticket.Attachments.ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]        public async Task<IActionResult> AddComment(int ticketId, string content)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }
            
            var comment = new TicketComment
            {
                TicketId = ticketId,
                CommentText = content,
                CreatedById = currentUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();

            var ticket = await _context.Tickets
                .Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Id == ticketId);

            // If comment is from admin, notify the ticket creator
            var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
            if (ticket != null && isAdmin && ticket.CreatedById != null && ticket.CreatedById != currentUser.Id)
            {
                await _notificationService.CreateNotification(
                    ticket.CreatedById,
                    "Admin Comment on Ticket",
                    $"An administrator has commented on your ticket: {ticket.Title}",
                    $"/Ticket/Details/{ticketId}"
                );
            }
            // If not from admin, notify the assigned user
            else if (ticket != null && !isAdmin && ticket.AssignedToId != null && ticket.AssignedToId != currentUser.Id)
            {
                await _notificationService.CreateNotification(
                    ticket.AssignedToId,
                    "New Comment on Ticket",
                    $"New comment added to ticket: {ticket.Title}",
                    $"/Ticket/Details/{ticketId}"
                );
            }

            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAttachment(int ticketId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tickets", ticketId.ToString());
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new TicketAttachment
            {
                TicketId = ticketId,
                FileName = file.FileName,
                FilePath = $"/uploads/tickets/{ticketId}/{uniqueFileName}",
                FileType = file.ContentType,
                FileSize = file.Length,
                UploadedById = currentUser.Id,
                UploadedAt = DateTime.UtcNow
            };

            _context.TicketAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            // If the uploader is a client, redirect back to the Client-specific TicketDetails action
            if (await _userManager.IsInRoleAsync(currentUser, "Client"))
            {
                return RedirectToAction("TicketDetails", "Client", new { id = ticketId });
            }
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                return NotFound();
            }

            ticket.Status = status;
            ticket.UpdatedAt = DateTime.UtcNow;
            
            if (status == "Resolved")
            {
                ticket.ResolvedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();            if (ticket.CreatedById != null)
            {
                await _notificationService.CreateNotification(
                    ticket.CreatedById,
                    "Ticket Status Updated",
                    $"Ticket '{ticket.Title}' status has been updated to {status}",
                    $"/Ticket/Details/{ticket.Id}"
                );
            }

            return RedirectToAction(nameof(Details), new { id });
        }        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Project Manager")]
        public async Task<IActionResult> AssignTicket(int id, string assignedToId)
        {
            if (string.IsNullOrEmpty(assignedToId))
            {
                return BadRequest("Invalid assignee");
            }

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
            {
                return NotFound();
            }

            ticket.AssignedToId = assignedToId;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                assignedToId,
                "Ticket Assigned",
                $"You have been assigned to ticket: {ticket.Title}",
                $"/Ticket/Details/{ticket.Id}"
            );

            return RedirectToAction(nameof(Details), new { id });
        }

        public IActionResult TestSql()
        {
            var sql = @"
                SELECT t.Id, t.Title, t.Description, t.Priority, t.Status, t.Type,
                       t.CreatedAt, u.UserName as CreatedBy
                FROM Tickets t
                JOIN AspNetUsers u ON t.CreatedById = u.Id
                ORDER BY t.CreatedAt DESC;
            ";

            // Execute the raw SQL query
            var tickets = _context.Tickets.FromSqlRaw(sql).ToList();

            return View(tickets);
        }
    }
}
