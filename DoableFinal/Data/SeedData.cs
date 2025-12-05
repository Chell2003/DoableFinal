using Microsoft.AspNetCore.Identity;
using DoableFinal.Models;
using Microsoft.EntityFrameworkCore;

namespace DoableFinal.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Create roles if they don't exist
            string[] roles = { "Admin", "Employee", "Client", "Project Manager" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Check if admin user exists
            var adminEmail = "admin@qonnec.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Administrator",
                    Role = "Admin",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailNotificationsEnabled = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Create a test client user if it doesn't exist
            var clientEmail = "client@example.com";
            var clientUser = await userManager.FindByEmailAsync(clientEmail);
            if (clientUser == null)
            {
                clientUser = new ApplicationUser
                {
                    UserName = clientEmail,
                    Email = clientEmail,
                    FirstName = "Test",
                    LastName = "Client",
                    Role = "Client",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailNotificationsEnabled = true
                };

                var result = await userManager.CreateAsync(clientUser, "Client@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(clientUser, "Client");
                }
            }

            // Create a test project manager user if it doesn't exist
            var pmEmail = "pm@example.com";
            var pmUser = await userManager.FindByEmailAsync(pmEmail);
            if (pmUser == null)
            {
                pmUser = new ApplicationUser
                {
                    UserName = pmEmail,
                    Email = pmEmail,
                    FirstName = "Test",
                    LastName = "ProjectManager",
                    Role = "Project Manager",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    EmailNotificationsEnabled = true
                };

                var result = await userManager.CreateAsync(pmUser, "ProjectManager@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(pmUser, "Project Manager");
                }
            }

            // Create test projects for reporting if they don't exist
            if (!await dbContext.Projects.AnyAsync())
            {
                // Ensure admin user ID is in context
                adminUser = adminUser ?? await userManager.FindByEmailAsync(adminEmail);
                clientUser = clientUser ?? await userManager.FindByEmailAsync(clientEmail);
                pmUser = pmUser ?? await userManager.FindByEmailAsync(pmEmail);

                if (adminUser != null && clientUser != null && pmUser != null)
                {
                    var testProject1 = new Project
                    {
                        Name = "Website Redesign",
                        Description = "Redesign the company website with modern UI/UX",
                        StartDate = DateTime.UtcNow.AddDays(-30),
                        EndDate = DateTime.UtcNow.AddDays(30),
                        Status = "In Progress",
                        ClientId = clientUser.Id,
                        ProjectManagerId = pmUser.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsArchived = false
                    };

                    var testProject2 = new Project
                    {
                        Name = "Mobile App Development",
                        Description = "Develop iOS and Android applications",
                        StartDate = DateTime.UtcNow.AddDays(-60),
                        EndDate = DateTime.UtcNow.AddDays(60),
                        Status = "In Progress",
                        ClientId = clientUser.Id,
                        ProjectManagerId = pmUser.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsArchived = false
                    };

                    var testProject3 = new Project
                    {
                        Name = "Admin Test Project",
                        Description = "A project for testing admin access",
                        StartDate = DateTime.UtcNow.AddDays(-45),
                        EndDate = DateTime.UtcNow.AddDays(45),
                        Status = "In Progress",
                        ClientId = adminUser.Id,
                        ProjectManagerId = pmUser.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsArchived = false
                    };

                    dbContext.Projects.AddRange(testProject1, testProject2, testProject3);
                    await dbContext.SaveChangesAsync();
                }
            }

            // Seed Homepage Sections
            await SeedHomePageSections(dbContext);

            // Fix up users where the custom Role string doesn't match the normalized identity role
            try
            {
                var projectManagersByProperty = await dbContext.Users.Where(u => u.Role == "Project Manager").ToListAsync();
                foreach (var pm in projectManagersByProperty)
                {
                    var rolesForUser = await userManager.GetRolesAsync(pm);
                    if (!rolesForUser.Contains("Project Manager") && !rolesForUser.Contains("ProjectManager"))
                    {
                        await userManager.AddToRoleAsync(pm, "Project Manager");
                    }
                }
            }
            catch {
                // Ignore any seeding-time errors — they're non-fatal
            }
        }

        private static async Task SeedHomePageSections(ApplicationDbContext context)
        {
            // Check if we have ALL sections (homepage, about, services, and contact)
            var homePageSectionsCount = await context.HomePageSections.CountAsync(s => s.SectionKey.StartsWith("hero-") || s.SectionKey.StartsWith("feature-") || s.SectionKey.StartsWith("cta-"));
            var aboutSectionsCount = await context.HomePageSections.CountAsync(s => s.SectionKey.StartsWith("about-"));
            var servicesSectionsCount = await context.HomePageSections.CountAsync(s => s.SectionKey.StartsWith("services-"));
            var contactSectionsCount = await context.HomePageSections.CountAsync(s => s.SectionKey.StartsWith("contact-"));
            
            // We need 10 homepage, 14 about, 39 services, and 21 contact sections
            if (homePageSectionsCount == 10 && aboutSectionsCount == 14 && servicesSectionsCount == 39 && contactSectionsCount == 21)
            {
                return; // All sections already seeded
            }

            var sections = new List<HomePageSection>
            {
                // Hero Section
                new HomePageSection
                {
                    SectionKey = "hero-title",
                    DisplayName = "Hero - Title",
                    Content = "<h1 class=\"display-4 fw-bold mb-4\">QONNEC</h1>",
                    SectionOrder = 1,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "hero-body",
                    DisplayName = "Hero - Description",
                    Content = "<p class=\"lead mb-4\">Streamline your projects, collaborate with your team, and achieve better results with our comprehensive project management solution.</p>",
                    SectionOrder = 2,
                    CreatedAt = DateTime.UtcNow
                },

                // Features Section
                new HomePageSection
                {
                    SectionKey = "feature-1-title",
                    DisplayName = "Feature 1 - Title",
                    Content = "Task Management",
                    SectionOrder = 3,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "feature-1-body",
                    DisplayName = "Feature 1 - Description",
                    Content = "Organize and track tasks with our intuitive Kanban board system.",
                    SectionOrder = 4,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "feature-2-title",
                    DisplayName = "Feature 2 - Title",
                    Content = "Team Collaboration",
                    SectionOrder = 5,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "feature-2-body",
                    DisplayName = "Feature 2 - Description",
                    Content = "Work together seamlessly with real-time updates and communication tools.",
                    SectionOrder = 6,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "feature-3-title",
                    DisplayName = "Feature 3 - Title",
                    Content = "Progress Tracking",
                    SectionOrder = 7,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "feature-3-body",
                    DisplayName = "Feature 3 - Description",
                    Content = "Monitor project progress with detailed analytics and reporting.",
                    SectionOrder = 8,
                    CreatedAt = DateTime.UtcNow
                },

                // CTA Section
                new HomePageSection
                {
                    SectionKey = "cta-title",
                    DisplayName = "CTA - Title",
                    Content = "Ready to Get Started?",
                    SectionOrder = 9,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "cta-body",
                    DisplayName = "CTA - Description",
                    Content = "Join thousands of teams already using our platform to manage their projects.",
                    SectionOrder = 10,
                    CreatedAt = DateTime.UtcNow
                },

                // About Page Sections
                new HomePageSection
                {
                    SectionKey = "about-hero-title",
                    DisplayName = "About - Hero Title",
                    Content = "<h1 class=\"display-4 fw-bold mb-4\">About Us</h1>",
                    SectionOrder = 11,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-hero-body",
                    DisplayName = "About - Hero Description",
                    Content = "<p class=\"lead\">We're dedicated to revolutionizing project management through innovative solutions and exceptional service.</p>",
                    SectionOrder = 12,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-story-title",
                    DisplayName = "About - Our Story Title",
                    Content = "<h2 class=\"text-center mb-4\">Our Story</h2>",
                    SectionOrder = 13,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-story-intro",
                    DisplayName = "About - Our Story Intro",
                    Content = "<p class=\"lead text-center mb-5\">What started as a freelance project has grown into a nationwide software company helping auto repair shops thrive through technology.</p>",
                    SectionOrder = 14,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-story-content",
                    DisplayName = "About - Our Story Content",
                    Content = "<p>It all began as a freelance project when a shop in Quezon City approached us, looking for a system to help manage their business. That small project became the foundation of something much larger.</p><p>In 2017, the software was redesigned to serve general auto repair shops and was originally named <strong>Auto Shop Office</strong>, inspired by the idea of creating an \"office suite\" built specifically for shop operations.</p><p>To reach more users, a website was launched and online ads were run. A partnership was formed, and together we registered our first business name, <strong>HENNIK Automotive Software Solutions</strong>, with the DTI.</p><p>Our very first client placed their trust in us, and through continuous feedback, we refined the system and expanded our user base. Growth was steady as we personally visited shops, conducted demos, and learned directly from our customers.</p><p>As more features were added and more clients came onboard, the workload increased. Eventually, one partner moved on, but development continued — leading to a major breakthrough when a leading auto repair franchise adopted the system.</p><p>This success inspired the next chapter. The business was officially registered with the SEC under a new name: <strong>Qonnec Software Solutions, Inc.</strong> As the product evolved into a more data-driven and intelligent platform, it was rebranded as <strong>Autometrik</strong>.</p><p>Today, Autometrik powers around <strong>200 auto repair shops</strong> across Luzon, Visayas, and Mindanao. Qonnec continues to grow, innovate, and stay true to its mission — helping automotive businesses become more efficient and connected through technology.</p>",
                    SectionOrder = 15,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-mission-title",
                    DisplayName = "About - Mission Title",
                    Content = "<h2 class=\"mb-4\">Our Mission</h2>",
                    SectionOrder = 16,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-mission-content",
                    DisplayName = "About - Mission Content",
                    Content = "<p class=\"lead\">To empower automotive businesses with innovative tools that simplify operations, enhance efficiency, and drive growth.</p><p>We believe that technology should make running a business easier, not harder. Our mission is to provide intelligent, data-driven software solutions that help auto repair shops streamline their workflow, connect their teams, and deliver exceptional customer experiences.</p>",
                    SectionOrder = 17,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-values-title",
                    DisplayName = "About - Values Title",
                    Content = "<h2 class=\"text-center mb-5\">Our Values</h2>",
                    SectionOrder = 18,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-value-1-title",
                    DisplayName = "About - Value 1 Title",
                    Content = "Innovation",
                    SectionOrder = 19,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-value-1-body",
                    DisplayName = "About - Value 1 Description",
                    Content = "We constantly push boundaries to create smarter, data-driven software that helps auto repair shops work more efficiently.",
                    SectionOrder = 20,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-value-2-title",
                    DisplayName = "About - Value 2 Title",
                    Content = "Collaboration",
                    SectionOrder = 21,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-value-2-body",
                    DisplayName = "About - Value 2 Description",
                    Content = "We work closely with our clients and partners, valuing their feedback to continuously improve and evolve our solutions.",
                    SectionOrder = 22,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-value-3-title",
                    DisplayName = "About - Value 3 Title",
                    Content = "Integrity",
                    SectionOrder = 23,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "about-value-3-body",
                    DisplayName = "About - Value 3 Description",
                    Content = "We build lasting relationships through honesty, accountability, and transparency in everything we do.",
                    SectionOrder = 24,
                    CreatedAt = DateTime.UtcNow
                },

                // Services Page Sections
                new HomePageSection
                {
                    SectionKey = "services-hero-title",
                    DisplayName = "Services - Hero Title",
                    Content = "<h1 class=\"display-4 fw-bold mb-4\">Our Services</h1>",
                    SectionOrder = 25,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-hero-body",
                    DisplayName = "Services - Hero Description",
                    Content = "<p class=\"lead\">Comprehensive project management solutions tailored to your needs.</p>",
                    SectionOrder = 26,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-hero-image",
                    DisplayName = "Services - Hero Image",
                    Content = "~/images/logo-combined1.png",
                    SectionOrder = 27,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-core-title",
                    DisplayName = "Services - Core Services Title",
                    Content = "Core Services",
                    SectionOrder = 28,
                    CreatedAt = DateTime.UtcNow
                },
                // Service Cards
                new HomePageSection
                {
                    SectionKey = "services-service1-title",
                    DisplayName = "Services - Service 1 Title",
                    Content = "Task Management",
                    SectionOrder = 29,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-service1-body",
                    DisplayName = "Services - Service 1 Description",
                    Content = "Organize and track tasks with our intuitive Kanban board system. Set priorities, deadlines, and dependencies to keep your projects on track.",
                    SectionOrder = 30,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-service2-title",
                    DisplayName = "Services - Service 2 Title",
                    Content = "Team Collaboration",
                    SectionOrder = 31,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-service2-body",
                    DisplayName = "Services - Service 2 Description",
                    Content = "Foster seamless communication with real-time updates, file sharing, and team discussions. Keep everyone aligned and informed.",
                    SectionOrder = 32,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-service3-title",
                    DisplayName = "Services - Service 3 Title",
                    Content = "Progress Tracking",
                    SectionOrder = 33,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-service3-body",
                    DisplayName = "Services - Service 3 Description",
                    Content = "Monitor project progress with detailed analytics, milestone tracking, and performance metrics. Make data-driven decisions.",
                    SectionOrder = 34,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-title",
                    DisplayName = "Services - Pricing Title",
                    Content = "Software Pricing",
                    SectionOrder = 35,
                    CreatedAt = DateTime.UtcNow
                },
                // Standard Plan
                new HomePageSection
                {
                    SectionKey = "services-pricing-standard-title",
                    DisplayName = "Services - Pricing Standard Title",
                    Content = "STANDARD",
                    SectionOrder = 36,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-standard-price",
                    DisplayName = "Services - Pricing Standard Price",
                    Content = "₱2,999.00 / month / location",
                    SectionOrder = 37,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-standard-users",
                    DisplayName = "Services - Pricing Standard Users",
                    Content = "3 users (₱500.00/additional user)",
                    SectionOrder = 38,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-standard-support",
                    DisplayName = "Services - Pricing Standard Support",
                    Content = "Email/Viber chat support",
                    SectionOrder = 39,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-standard-features",
                    DisplayName = "Services - Pricing Standard Features",
                    Content = "✔ Dashboard, Vehicle Management, Estimates\n✔ Job Orders, Invoices, Appointments\n✔ Customer & Supplier Management\n✔ Accounting (Chart of Accounts, P&L, Balance Sheet)\n✔ Reports (Sales, Service, Mechanic, Top Products)\n✔ 50 Free SMS/month",
                    SectionOrder = 40,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-standard-fee",
                    DisplayName = "Services - Pricing Standard Fee",
                    Content = "One-time implementation fee of ₱20,000.00",
                    SectionOrder = 41,
                    CreatedAt = DateTime.UtcNow
                },
                // Elite Plan
                new HomePageSection
                {
                    SectionKey = "services-pricing-elite-title",
                    DisplayName = "Services - Pricing Elite Title",
                    Content = "ELITE",
                    SectionOrder = 42,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-elite-price",
                    DisplayName = "Services - Pricing Elite Price",
                    Content = "₱3,999.00 / month / location",
                    SectionOrder = 43,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-elite-users",
                    DisplayName = "Services - Pricing Elite Users",
                    Content = "5 users (₱500.00/additional user)",
                    SectionOrder = 44,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-elite-support",
                    DisplayName = "Services - Pricing Elite Support",
                    Content = "Email/Viber chat support",
                    SectionOrder = 45,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-elite-features",
                    DisplayName = "Services - Pricing Elite Features",
                    Content = "✔ 100 Free SMS/month & Custom Sender ID\n✔ Automated SMS Service Reminders\n✔ Loyalty and Memberships\n✔ Barcoding & Online Payments (Future)\n✔ Supplier & Customer Portals (Future)\n✔ Stock Transfer & Inventory Forecasting (Future)\n✔ Payroll (Future), BIR CAS",
                    SectionOrder = 46,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-elite-fee",
                    DisplayName = "Services - Pricing Elite Fee",
                    Content = "One-time implementation fee of ₱30,000.00",
                    SectionOrder = 47,
                    CreatedAt = DateTime.UtcNow
                },
                // Enterprise Plan
                new HomePageSection
                {
                    SectionKey = "services-pricing-enterprise-title",
                    DisplayName = "Services - Pricing Enterprise Title",
                    Content = "ENTERPRISE",
                    SectionOrder = 48,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-enterprise-price",
                    DisplayName = "Services - Pricing Enterprise Price",
                    Content = "Custom Pricing",
                    SectionOrder = 49,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-enterprise-branches",
                    DisplayName = "Services - Pricing Enterprise Branches",
                    Content = "Minimum of 10 branches",
                    SectionOrder = 50,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-enterprise-users",
                    DisplayName = "Services - Pricing Enterprise Users",
                    Content = "10 users (₱300.00/additional user)",
                    SectionOrder = 51,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-enterprise-support",
                    DisplayName = "Services - Pricing Enterprise Support",
                    Content = "Email/Viber chat/Phone support",
                    SectionOrder = 52,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-enterprise-features",
                    DisplayName = "Services - Pricing Enterprise Features",
                    Content = "✔ Dedicated Server Instance\n✔ Customized Printout & Inspection Templates\n✔ Customized Software Branding\n✔ Multi-Location Management & Reports\n✔ API Access",
                    SectionOrder = 53,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-pricing-enterprise-fee",
                    DisplayName = "Services - Pricing Enterprise Fee",
                    Content = "Call us for customized pricing",
                    SectionOrder = 54,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-features-title",
                    DisplayName = "Services - Additional Features Title",
                    Content = "Additional Features",
                    SectionOrder = 55,
                    CreatedAt = DateTime.UtcNow
                },
                // Feature Cards
                new HomePageSection
                {
                    SectionKey = "services-feature1-title",
                    DisplayName = "Services - Feature 1 Title",
                    Content = "Security & Compliance",
                    SectionOrder = 56,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-feature1-body",
                    DisplayName = "Services - Feature 1 Description",
                    Content = "Enterprise-grade security with data encryption, role-based access control, and compliance with industry standards.",
                    SectionOrder = 57,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-feature2-title",
                    DisplayName = "Services - Feature 2 Title",
                    Content = "Mobile Access",
                    SectionOrder = 58,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-feature2-body",
                    DisplayName = "Services - Feature 2 Description",
                    Content = "Access your projects on the go with our mobile-friendly interface and native mobile apps.",
                    SectionOrder = 59,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-feature3-title",
                    DisplayName = "Services - Feature 3 Title",
                    Content = "Time Tracking",
                    SectionOrder = 60,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-feature3-body",
                    DisplayName = "Services - Feature 3 Description",
                    Content = "Track time spent on tasks and projects with our integrated time tracking feature.",
                    SectionOrder = 61,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-feature4-title",
                    DisplayName = "Services - Feature 4 Title",
                    Content = "Reporting",
                    SectionOrder = 62,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "services-feature4-body",
                    DisplayName = "Services - Feature 4 Description",
                    Content = "Generate comprehensive reports and insights to make informed decisions.",
                    SectionOrder = 63,
                    CreatedAt = DateTime.UtcNow
                },
                
                // Contact Page Sections (21 sections total)
                new HomePageSection
                {
                    SectionKey = "contact-hero-title",
                    DisplayName = "Contact - Hero Title",
                    Content = "<h1 class=\"display-4 fw-bold mb-4\">Contact Us</h1>",
                    SectionOrder = 64,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-hero-body",
                    DisplayName = "Contact - Hero Description",
                    Content = "<p class=\"lead\">Get in touch with our team. We're here to help!</p>",
                    SectionOrder = 65,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-hero-image",
                    DisplayName = "Contact - Hero Image",
                    Content = "~/images/logo-combined1.png",
                    SectionOrder = 66,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-form-title",
                    DisplayName = "Contact - Form Title",
                    Content = "Send us a Message",
                    SectionOrder = 67,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-address-title",
                    DisplayName = "Contact - Address Title",
                    Content = "Address",
                    SectionOrder = 68,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-address-content",
                    DisplayName = "Contact - Address Content",
                    Content = "123 Business Street<br>New York, NY 10001<br>United States",
                    SectionOrder = 69,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-phone-title",
                    DisplayName = "Contact - Phone Title",
                    Content = "Phone",
                    SectionOrder = 70,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-phone-content",
                    DisplayName = "Contact - Phone Content",
                    Content = "+1 (555) 123-4567<br>+1 (555) 987-6543",
                    SectionOrder = 71,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-email-title",
                    DisplayName = "Contact - Email Title",
                    Content = "Email",
                    SectionOrder = 72,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-email-content",
                    DisplayName = "Contact - Email Content",
                    Content = "support@doable.com<br>sales@doable.com",
                    SectionOrder = 73,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-faq-title",
                    DisplayName = "Contact - FAQ Title",
                    Content = "Frequently Asked Questions",
                    SectionOrder = 74,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-faq1-question",
                    DisplayName = "Contact - FAQ 1 Question",
                    Content = "How do I get started with Doable?",
                    SectionOrder = 75,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-faq1-answer",
                    DisplayName = "Contact - FAQ 1 Answer",
                    Content = "Getting started is easy! Simply create an account, choose your plan, and you can begin managing your projects right away. Our intuitive interface will guide you through the setup process.",
                    SectionOrder = 76,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-faq2-question",
                    DisplayName = "Contact - FAQ 2 Question",
                    Content = "Can I upgrade or downgrade my plan?",
                    SectionOrder = 77,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-faq2-answer",
                    DisplayName = "Contact - FAQ 2 Answer",
                    Content = "Yes, you can change your plan at any time. Simply go to your account settings and select the new plan you'd like to switch to. Changes will be reflected in your next billing cycle.",
                    SectionOrder = 78,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-faq3-question",
                    DisplayName = "Contact - FAQ 3 Question",
                    Content = "What payment methods do you accept?",
                    SectionOrder = 79,
                    CreatedAt = DateTime.UtcNow
                },
                new HomePageSection
                {
                    SectionKey = "contact-faq3-answer",
                    DisplayName = "Contact - FAQ 3 Answer",
                    Content = "We accept all major credit cards (Visa, MasterCard, American Express), PayPal, and bank transfers for annual plans. All payments are processed securely through our payment partners.",
                    SectionOrder = 80,
                    CreatedAt = DateTime.UtcNow
                }
            };

            // Add only sections that don't already exist
            var existingKeys = await context.HomePageSections.Select(s => s.SectionKey).ToListAsync();
            var sectionsToAdd = sections.Where(s => !existingKeys.Contains(s.SectionKey)).ToList();
            
            if (sectionsToAdd.Any())
            {
                await context.HomePageSections.AddRangeAsync(sectionsToAdd);
                await context.SaveChangesAsync();
            }
        }
    }
}