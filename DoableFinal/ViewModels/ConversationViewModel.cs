using System;
using DoableFinal.Models;

namespace DoableFinal.ViewModels
{
    public class ConversationViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public Message? LastMessage { get; set; }
        public int UnreadCount { get; set; }
    }
}
