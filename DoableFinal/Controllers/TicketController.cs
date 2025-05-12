using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using DoableFinal.Services;

namespace DoableFinal.Controllers
{
    [Authorize(Roles = "Client,Admin,Project Manager")]
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
        }        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }            var tickets = await _context.Tickets
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

            return View(tickets);
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
            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    return Challenge();
                }

                var ticket = new Ticket
                {
                    Title = model.Title,
                    Description = model.Description,
                    Priority = model.Priority,
                    Status = "Open",
                    Type = model.Type,
                    ProjectId = model.ProjectId,
                    AssignedToId = model.AssignedToId,
                    CreatedById = currentUser.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                if (ticket.AssignedToId != null)
                {
                    await _notificationService.CreateNotification(
                        ticket.AssignedToId,
                        "New Ticket Assigned",
                        $"You have been assigned to ticket: {ticket.Title}",
                        $"/Ticket/Details/{ticket.Id}"
                    );
                }

                TempData["SuccessMessage"] = "Ticket created successfully.";
                return RedirectToAction(nameof(Index));
            }

            // Reload the select lists if we need to return to the view
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

            return View(model);
        }

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
                Content = content,
                CreatedById = currentUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.TicketComments.Add(comment);
            await _context.SaveChangesAsync();

            var ticket = await _context.Tickets
                .Include(t => t.AssignedTo)
                .FirstOrDefaultAsync(t => t.Id == ticketId);            if (ticket != null && ticket.AssignedToId != null && ticket.AssignedToId != currentUser.Id)
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
    }
}
