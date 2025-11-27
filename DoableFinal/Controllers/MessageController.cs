using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Data;
using DoableFinal.Models;
using DoableFinal.ViewModels;
using Microsoft.AspNetCore.Identity;
using System;

namespace DoableFinal.Controllers
{
    [Authorize]
    public class MessageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessageController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? projectId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Determine projects the current user is associated with.
            // For clients, projects where they are the client (Project.ClientId).
            // For project managers, projects where they are the project manager (Project.ProjectManagerId).
            // For employees, projects where they are on the project team (ProjectTeams).
            // For admins, all non-archived projects.
            var projects = new List<Project>();
            if (await _userManager.IsInRoleAsync(currentUser, "Client"))
            {
                projects = await _context.Projects
                    .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "ProjectManager"))
            {
                projects = await _context.Projects
                    .Where(p => p.ProjectManagerId == currentUser.Id && !p.IsArchived)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "Admin"))
            {
                projects = await _context.Projects
                    .Where(p => !p.IsArchived)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }
            else
            {
                // Employee: projects where they are on the project team
                projects = await _context.Projects
                    .Where(p => p.ProjectTeams.Any(pt => pt.UserId == currentUser.Id) && !p.IsArchived)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }

            // Build the set of relevant user IDs based on the discovered projects or filtered project.
            var relevantUserIds = new HashSet<string>();
            var projectsForUsers = projects;
            
            // If a specific projectId is provided, filter to only that project
            if (projectId.HasValue)
            {
                var selectedProject = projects.FirstOrDefault(p => p.Id == projectId.Value);
                if (selectedProject != null)
                {
                    projectsForUsers = new List<Project> { selectedProject };
                }
                else
                {
                    // Project not found or not accessible
                    projectsForUsers = new List<Project>();
                }
            }

            if (projectsForUsers.Any())
            {
                foreach (var proj in projectsForUsers)
                {
                    if (!string.IsNullOrEmpty(proj.ProjectManagerId)) relevantUserIds.Add(proj.ProjectManagerId);
                    // Include the project client only if the current user is allowed to message clients (Project Manager and Admin)
                    if (!string.IsNullOrEmpty(proj.ClientId) && (await _userManager.IsInRoleAsync(currentUser, "ProjectManager") || await _userManager.IsInRoleAsync(currentUser, "Admin")))
                    {
                        relevantUserIds.Add(proj.ClientId);
                    }
                    // For Clients: only include the Project Manager from the team
                    // For others: include all team members
                    if (await _userManager.IsInRoleAsync(currentUser, "Client"))
                    {
                        // Already added ProjectManagerId above, so team members are not included for clients
                    }
                    else
                    {
                        var teamUserIds = await _context.ProjectTeams
                            .Where(pt => pt.ProjectId == proj.Id)
                            .Select(pt => pt.UserId)
                            .ToListAsync();
                        foreach (var id in teamUserIds) relevantUserIds.Add(id);
                    }
                }
            }

            // Get all users except the current user, but restrict to only users related to the filtered project(s).
            var allUsersQuery = _userManager.Users.Where(u => u.Id != currentUser.Id);
            // For Client and ProjectManager: validate freshly to exclude archived/removed users
            if (await _userManager.IsInRoleAsync(currentUser, "Client"))
            {
                // Clients: Only show users currently on their projects
                if (relevantUserIds.Any())
                {
                    allUsersQuery = allUsersQuery.Where(u => relevantUserIds.Contains(u.Id));
                }
                else
                {
                    allUsersQuery = allUsersQuery.Where(u => false);
                }
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "Employee"))
            {
                if (relevantUserIds.Any())
                {
                    allUsersQuery = allUsersQuery.Where(u => relevantUserIds.Contains(u.Id));
                }
                else
                {
                    // No related users for this user -> empty list
                    allUsersQuery = allUsersQuery.Where(u => false);
                }
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "ProjectManager"))
            {
                // ProjectManagers: Only show users currently on their projects (same as Client)
                if (relevantUserIds.Any())
                {
                    allUsersQuery = allUsersQuery.Where(u => relevantUserIds.Contains(u.Id));
                }
                else
                {
                    allUsersQuery = allUsersQuery.Where(u => false);
                }
            }

            var allUsers = await allUsersQuery.OrderBy(u => u.FirstName).ToListAsync();

            // Get all messages ordered by most recent first, optionally filtered by project
            var messagesQuery = _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == currentUser.Id || m.ReceiverId == currentUser.Id);
            
            if (projectId.HasValue)
            {
                messagesQuery = messagesQuery.Where(m => m.ProjectId == projectId.Value);
            }

            var messages = await messagesQuery.OrderByDescending(m => m.CreatedAt).ToListAsync();

            // Get users with messages and their most recent conversations
            // Only include users that are in the filtered project(s) to avoid showing conversations with users outside the project
            var usersWithMessages = messages
                .GroupBy(m => m.SenderId == currentUser.Id ? m.ReceiverId : m.SenderId)
                .Where(g => g.Key != currentUser.Id) // exclude conversations that are only with self
                .Select(g =>
                {
                    var userId = g.Key;
                    var lastMessage = g.FirstOrDefault(); // already ordered by CreatedAt desc
                    var user = allUsers.FirstOrDefault(u => u.Id == userId);
                    
                    // Only include this conversation if the other user is in the relevant user set for this project
                    if (lastMessage == null || user == null || !relevantUserIds.Contains(userId))
                        return null;
                    
                    return new ViewModels.ConversationViewModel
                    {
                        UserId = userId,
                        User = user,
                        LastMessage = lastMessage,
                        UnreadCount = g.Count(m => !m.IsRead && m.ReceiverId == currentUser.Id)
                    };
                })
                .Where(x => x != null)
                .ToList();

            // Get users who don't have any messages yet
            var usersWithoutMessages = allUsers
                .Where(u => !usersWithMessages.Any(m => m?.UserId == u.Id))
                .ToList();

            // Initialize lists for the view
            ViewBag.UsersWithMessages = usersWithMessages;
            ViewBag.UsersWithoutMessages = usersWithoutMessages;
            ViewBag.Projects = projects;
            ViewBag.SelectedProjectId = projectId;

            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string content, string receiverId, int? projectId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Validate that the recipient is authorized based on user role
            var receiver = await _userManager.FindByIdAsync(receiverId);
            if (receiver == null)
            {
                return BadRequest("Recipient not found");
            }

            // Get user's accessible projects
            var projects = new List<Project>();
            if (await _userManager.IsInRoleAsync(currentUser, "Client"))
            {
                projects = await _context.Projects
                    .Where(p => p.ClientId == currentUser.Id && !p.IsArchived)
                    .ToListAsync();
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "ProjectManager"))
            {
                projects = await _context.Projects
                    .Where(p => p.ProjectManagerId == currentUser.Id && !p.IsArchived)
                    .ToListAsync();
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "Admin"))
            {
                projects = await _context.Projects
                    .Where(p => !p.IsArchived)
                    .ToListAsync();
            }
            else
            {
                // Employee
                projects = await _context.Projects
                    .Where(p => p.ProjectTeams.Any(pt => pt.UserId == currentUser.Id) && !p.IsArchived)
                    .ToListAsync();
            }

            // Build set of allowed recipient IDs based on role
            var allowedRecipientIds = new HashSet<string>();
            
            if (await _userManager.IsInRoleAsync(currentUser, "Client"))
            {
                // Clients can only message: project managers of their projects
                foreach (var proj in projects)
                {
                    if (!string.IsNullOrEmpty(proj.ProjectManagerId)) 
                        allowedRecipientIds.Add(proj.ProjectManagerId);
                }
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "ProjectManager"))
            {
                // ProjectManagers can message: clients and team members of their projects
                foreach (var proj in projects)
                {
                    if (!string.IsNullOrEmpty(proj.ClientId)) 
                        allowedRecipientIds.Add(proj.ClientId);
                    var teamUserIds = await _context.ProjectTeams
                        .Where(pt => pt.ProjectId == proj.Id)
                        .Select(pt => pt.UserId)
                        .ToListAsync();
                    foreach (var id in teamUserIds) 
                        allowedRecipientIds.Add(id);
                }
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "Employee"))
            {
                // Employees can message: project managers, clients, and other team members from their projects
                foreach (var proj in projects)
                {
                    if (!string.IsNullOrEmpty(proj.ProjectManagerId)) 
                        allowedRecipientIds.Add(proj.ProjectManagerId);
                    // Employees should NOT be allowed to message the project client
                    // (Do not add proj.ClientId for employees)
                    var teamUserIds = await _context.ProjectTeams
                        .Where(pt => pt.ProjectId == proj.Id)
                        .Select(pt => pt.UserId)
                        .ToListAsync();
                    foreach (var id in teamUserIds) 
                        allowedRecipientIds.Add(id);
                }
            }
            else if (await _userManager.IsInRoleAsync(currentUser, "Admin"))
            {
                // Admins can message anyone except themselves
                var allUsers = await _userManager.Users.Select(u => u.Id).ToListAsync();
                allowedRecipientIds = new HashSet<string>(allUsers);
            }

            // Disallow messaging self and enforce role restrictions
            if (receiver.Id == currentUser.Id)
            {
                return BadRequest("Cannot send message to yourself");
            }
            // Employees cannot message clients
            if (await _userManager.IsInRoleAsync(currentUser, "Employee") && receiver.Role == "Client")
            {
                return BadRequest("Employees are not allowed to message clients");
            }

            // Check if recipient is in allowed list
            if (!allowedRecipientIds.Contains(receiverId))
            {
                return BadRequest("You are not authorized to message this user");
            }

            var message = new Message
            {
                Content = content,
                SenderId = currentUser.Id,
                ReceiverId = receiverId,
                ProjectId = projectId,
                CreatedAt = DateTime.Now
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, message = "Message sent successfully" });
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var message = await _context.Messages.FindAsync(id);
            if (message == null)
            {
                return NotFound();
            }

            message.IsRead = true;
            message.ReadAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(0);
            }

            var count = await _context.Messages
                .Where(m => m.ReceiverId == currentUser.Id && !m.IsRead)
                .CountAsync();

            return Json(count);
        }

        [HttpGet]
        public async Task<IActionResult> GetConversation(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == userId) ||
                           (m.SenderId == userId && m.ReceiverId == currentUser.Id))
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.CreatedAt,
                    m.IsRead,
                    m.SenderId,
                    SenderName = $"{m.Sender.FirstName} {m.Sender.LastName}",
                    m.ReceiverId,
                    ReceiverName = $"{m.Receiver.FirstName} {m.Receiver.LastName}"
                })
                .ToListAsync();

            // Mark messages as read
            var unreadMessages = await _context.Messages
                .Where(m => m.ReceiverId == currentUser.Id && m.SenderId == userId && !m.IsRead)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var message in unreadMessages)
                {
                    message.IsRead = true;
                    message.ReadAt = DateTime.Now;
                }
                await _context.SaveChangesAsync();
            }

            return Json(new { messages = messages });
        }

        [HttpGet]
        public async Task<IActionResult> GetSharedProjects(string recipientId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || string.IsNullOrEmpty(recipientId))
            {
                return Json(new List<object>());
            }

            // Helper to get project IDs for a user
            async Task<List<Project>> GetProjectsForUser(ApplicationUser user)
            {
                if (await _userManager.IsInRoleAsync(user, "Client"))
                {
                    return await _context.Projects.Where(p => p.ClientId == user.Id && !p.IsArchived).ToListAsync();
                }
                else
                {
                    return await _context.Projects.Where(p => (p.ProjectTeams.Any(pt => pt.UserId == user.Id) || p.ProjectManagerId == user.Id) && !p.IsArchived).ToListAsync();
                }
            }

            var recipient = await _userManager.FindByIdAsync(recipientId);
            if (recipient == null)
            {
                return Json(new List<object>());
            }

            var currentProjects = await GetProjectsForUser(currentUser);
            var recipientProjects = await GetProjectsForUser(recipient);

            var shared = currentProjects.Select(p => p.Id).Intersect(recipientProjects.Select(p => p.Id)).ToList();

            var result = await _context.Projects
                .Where(p => shared.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .OrderBy(p => p.Name)
                .ToListAsync();

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetConversations()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            var conversations = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == currentUser.Id || m.ReceiverId == currentUser.Id)
                .GroupBy(m => m.SenderId == currentUser.Id ? m.ReceiverId : m.SenderId)
                .Select(g => new ViewModels.ConversationViewModel
                {
                    UserId = g.Key,
                    User = g.First().SenderId == currentUser.Id ? g.First().Receiver : g.First().Sender,
                    LastMessage = g.OrderByDescending(m => m.CreatedAt).First(),
                    UnreadCount = g.Count(m => !m.IsRead && m.ReceiverId == currentUser.Id)
                })
                .OrderByDescending(c => c.LastMessage != null ? c.LastMessage.CreatedAt : DateTime.MinValue)
                .ToListAsync();

            return Json(conversations);
        }
    }
}