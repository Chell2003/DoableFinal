using DoableFinal.Data;
using DoableFinal.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoableFinal.Services
{
    public interface IHomePageService
    {
        Task<List<HomePageSection>> GetAllSectionsAsync();
        Task<HomePageSection> GetSectionByKeyAsync(string sectionKey);
        Task<List<HomePageSection>> GetSectionsByCategoryAsync(string category);
        Task UpdateSectionAsync(int id, string? content, string userId, string? imagePath = null);
        Task<HomePageSection> CreateSectionAsync(string sectionKey, string displayName, string content, string? iconClass = null, int? order = null);
    }

    public class HomePageService : IHomePageService
    {
        private readonly ApplicationDbContext _context;

        public HomePageService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<HomePageSection>> GetAllSectionsAsync()
        {
            return await _context.HomePageSections.OrderBy(s => s.SectionOrder).ToListAsync();
        }

        public async Task<HomePageSection> GetSectionByKeyAsync(string sectionKey)
        {
            return await _context.HomePageSections.FirstOrDefaultAsync(s => s.SectionKey == sectionKey);
        }

        public async Task<List<HomePageSection>> GetSectionsByCategoryAsync(string category)
        {
            // e.g., category = "hero", "features", "cta"
            return await _context.HomePageSections
                .Where(s => s.SectionKey.StartsWith(category))
                .OrderBy(s => s.SectionOrder)
                .ToListAsync();
        }

        public async Task UpdateSectionAsync(int id, string? content, string userId, string? imagePath = null)
        {
            var section = await _context.HomePageSections.FindAsync(id);
            if (section != null)
            {
                if (!string.IsNullOrEmpty(content))
                {
                    section.Content = content;
                }
                if (!string.IsNullOrEmpty(imagePath))
                {
                    section.ImagePath = imagePath;
                }
                section.UpdatedAt = DateTime.UtcNow;
                section.UpdatedBy = userId;
                _context.HomePageSections.Update(section);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<HomePageSection> CreateSectionAsync(string sectionKey, string displayName, string content, string? iconClass = null, int? order = null)
        {
            var section = new HomePageSection
            {
                SectionKey = sectionKey,
                DisplayName = displayName,
                Content = content,
                IconClass = iconClass,
                SectionOrder = order,
                CreatedAt = DateTime.UtcNow
            };

            _context.HomePageSections.Add(section);
            await _context.SaveChangesAsync();
            return section;
        }
    }
}
