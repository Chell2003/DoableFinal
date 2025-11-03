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

        private async Task ReloadFormData(CreateTicketViewModel model)
        {
            var projects = await _context.Projects.ToListAsync();
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            
            model.Projects = projects.Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name
            }).ToList();
            
            model.Assignees = employees.Select(e => new SelectListItem
            {
                Value = e.Id,
                Text = $"{e.FirstName} {e.LastName}"
            }).ToList();

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
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }
            
            var tickets = await _context.Tickets
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .Include(t => t.Project)
                .Where(t => 
                    User.IsInRole("Admin") || // Admin sees all tickets
                    (User.IsInRole("Project Manager") && t.Project.ProjectManagerId == currentUser.Id) || // PM sees their project tickets
                    (User.IsInRole("Client") && t.CreatedById == currentUser.Id) // Client sees only their created tickets
                )
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            var viewModel = new TicketListViewModel
            {
                Tickets = tickets,
                NotificationType = NotificationType.General
            };

            return View(viewModel);
        }        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create()
        {
            var projects = await _context.Projects.ToListAsync();
            var employees = await _userManager.GetUsersInRoleAsync("Employee");
            
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
        }        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create(CreateTicketViewModel model)
        {
            var debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine($"ModelState.IsValid: {ModelState.IsValid}");
            debugInfo.AppendLine($"Form Data: Title={model.Title}, Description={model.Description}, ProjectId={model.ProjectId}");
            
            if (!ModelState.IsValid)
            {
                debugInfo.AppendLine("\nValidation Errors:");
                foreach (var modelState in ModelState)
                {
                    foreach (var error in modelState.Value.Errors)
                    {
                        debugInfo.AppendLine($"- {modelState.Key}: {error.ErrorMessage}");
                    }
                }
                TempData["Debug"] = debugInfo.ToString();
                await ReloadFormData(model);
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !User.IsInRole("Client"))
            {
                TempData["Error"] = "You must be logged in as a client to create tickets.";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                debugInfo.AppendLine("Starting ticket creation process...");
                Ticket? ticket = null;
                
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        ticket = new Ticket
                        {
                            Title = model.Title,
                            Description = model.Description,
                            Priority = model.Priority,
                            Status = "Open",
                            Type = model.Type,
                            ProjectId = model.ProjectId,
                            AssignedToId = model.AssignedToId,
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
                            throw new Exception("Failed to save ticket to database - no rows affected");
                        }

                        await transaction.CommitAsync();
                        debugInfo.AppendLine($"Transaction committed successfully. New ticket ID: {ticket.Id}");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception($"Failed to save ticket: {ex.Message}");
                    }
                }

                debugInfo.AppendLine("Beginning notification process...");
                
                if (ticket == null)
                {
                    throw new Exception("Ticket was not properly created");
                }
                if (ticket.AssignedToId != null)
                {
                    await _notificationService.CreateNotification(
                        ticket.AssignedToId,
                        "New Ticket Assigned",
                        $"You have been assigned to ticket: {ticket.Title}",
                        $"/Ticket/Details/{ticket.Id}"
                    );
                }

                // Get Project Manager if project is selected
                if (ticket.ProjectId.HasValue)
                {
                    var project = await _context.Projects
                        .FirstOrDefaultAsync(p => p.Id == ticket.ProjectId);
                    if (project?.ProjectManagerId != null)
                    {
                        await _notificationService.CreateNotification(
                            project.ProjectManagerId,
                            "New Ticket Created",
                            $"A new ticket has been created for project {project.Name}: {ticket.Title}",
                            $"/Ticket/Details/{ticket.Id}"
                        );
                    }
                }

                // Notify all admins
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

                TempData["TicketMessage"] = "Ticket created successfully.";
                // Persist debug info to temp file for diagnostics
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "doable_ticket_debug.log"), debugInfo.ToString() + System.Environment.NewLine);
                }
                catch { }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                debugInfo.AppendLine($"Error: {ex.Message}");                
                TempData["Error"] = "An error occurred while creating the ticket: " + ex.Message;
                TempData["Debug"] = debugInfo.ToString();
                // Persist debug info to temp file for diagnostics
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "doable_ticket_debug.log"), debugInfo.ToString() + System.Environment.NewLine);
                }
                catch { }
                
                // Reload select lists before returning to view
                var projects = await _context.Projects.ToListAsync();
                var employees = await _userManager.GetUsersInRoleAsync("Employee");
                
                model.Projects = projects.Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                }).ToList();
                
                model.Assignees = employees.Select(e => new SelectListItem
                {
                    Value = e.Id,
                    Text = $"{e.FirstName} {e.LastName}"
                }).ToList();

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
        [ValidateAntiForgeryToken]        public async Task<IActionResult> AddAttachment(int ticketId, IFormFile file)
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

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "tickets");
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
                FilePath = $"/uploads/tickets/{uniqueFileName}",
                FileType = file.ContentType,
                FileSize = file.Length,
                UploadedById = currentUser.Id,
                UploadedAt = DateTime.UtcNow
            };

            _context.TicketAttachments.Add(attachment);
            await _context.SaveChangesAsync();

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
