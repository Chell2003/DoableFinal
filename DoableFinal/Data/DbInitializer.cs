using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DoableFinal.Models;

namespace DoableFinal.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
            {
                // Ensure database is created
                try
                {
                    context.Database.Migrate();
                }
                catch (Exception ex)
                {
                    var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                    throw;
                }

                // Initialize roles
                var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                string[] roles = { "Admin", "Client", "Employee", "Project Manager" };
                
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }
                }

                // Create admin user if it doesn't exist
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                const string adminEmail = "admin@doable.com";
                const string adminPassword = "Admin@123456";

                if (await userManager.FindByEmailAsync(adminEmail) == null)
                {
                    var admin = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FirstName = "Admin",
                        LastName = "User",
                        EmailConfirmed = true,
                        Role = "Admin"
                    };

                    var result = await userManager.CreateAsync(admin, adminPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(admin, "Admin");
                    }
                }

                await context.SaveChangesAsync();
            }
        }
    }
}