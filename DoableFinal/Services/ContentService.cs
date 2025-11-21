using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DoableFinal.ViewModels;

namespace DoableFinal.Services
{
    public class ContentService
    {
        private readonly string _contentFile;

        public ContentService()
        {
            _contentFile = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "home_pages.json");
        }

        private IDictionary<string, ContentPageViewModel> LoadPagesInternal()
        {
            try
            {
                if (!File.Exists(_contentFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_contentFile));
                    var defaultPages = new Dictionary<string, ContentPageViewModel>
                    {
                        { "Index", new ContentPageViewModel { Key = "Index", DisplayName = "Home - Index", TitleHtml = "<h1>QONNEC</h1>", BodyHtml = "<p>Streamline your projects...</p>", ImagePath = "" } },
                        { "About", new ContentPageViewModel { Key = "About", DisplayName = "Home - About", TitleHtml = "<h1>About Us</h1>", BodyHtml = "<p>About content...</p>", ImagePath = "" } },
                        { "Services", new ContentPageViewModel { Key = "Services", DisplayName = "Home - Services", TitleHtml = "<h1>Our Services</h1>", BodyHtml = "<p>Services...</p>", ImagePath = "" } },
                        { "Contact", new ContentPageViewModel { Key = "Contact", DisplayName = "Home - Contact", TitleHtml = "<h1>Contact</h1>", BodyHtml = "<p>Contact form text...</p>", ImagePath = "" } }
                    };
                    var json = JsonSerializer.Serialize(defaultPages, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_contentFile, json);
                }

                var contentJson = File.ReadAllText(_contentFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, ContentPageViewModel>>(contentJson) ?? new Dictionary<string, ContentPageViewModel>();
                return dict.ToDictionary(k => k.Key, v => v.Value);
            }
            catch
            {
                return new Dictionary<string, ContentPageViewModel>();
            }
        }

        public ContentPageViewModel? GetPage(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var pages = LoadPagesInternal();
            return pages.ContainsKey(key) ? pages[key] : null;
        }

        public IEnumerable<ContentPageViewModel> GetAllPages()
        {
            return LoadPagesInternal().Values;
        }
    }
}
