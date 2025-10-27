# DoableFinal

DoableFinal is a comprehensive project management system built with ASP.NET Core MVC and Entity Framework Core. This system enables efficient project management, task tracking, and team collaboration with role-based access control.

## � Step-by-Step Setup Guide

### Step 1: Prerequisites Installation
Before starting, make sure you have installed:
1. **.NET 7.0 SDK** or later
   - Download from: https://dotnet.microsoft.com/download
   - Verify installation: `dotnet --version`

2. **SQL Server LocalDB**
   - Download SQL Server Express: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - Make sure LocalDB is included in the installation
   - Verify installation: `sqllocaldb info`

3. **IDE Setup**
   - Option 1: Visual Studio 2022 (Recommended)
     - Download Community Edition: https://visualstudio.microsoft.com/
     - Required Workloads:
       - ASP.NET and web development
       - .NET desktop development
   
   - Option 2: VS Code
     - Download: https://code.visualstudio.com/
     - Required Extensions:
       - C# Dev Kit
       - .NET Runtime Install Tool
       - SQL Server

4. **Git**
   - Download: https://git-scm.com/downloads
   - Verify installation: `git --version`

### Step 2: Project Setup

1. **Clone the Repository**
```sh
git clone https://github.com/Chell2003/DoableFinal.git
cd DoableFinal
```

2. **Install Required .NET Tools**
```sh
# Install Entity Framework Core tools globally
dotnet tool install --global dotnet-ef

# Trust the HTTPS development certificate
dotnet dev-certs https --trust
```

3. **Restore Dependencies**
```sh
# Restore NuGet packages
dotnet restore

# Verify the restore was successful
dotnet build
```

### Step 3: Configure Application Settings

1. **Setup Application Configuration**
```sh
# Create a copy of example settings (if exists)
copy appsettings.example.json appsettings.json
```

2. **Configure appsettings.json**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DoableFinal;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

3. **Setup User Secrets (Development Only)**
```sh
# Initialize user secrets
dotnet user-secrets init

# Configure email settings (if needed)
dotnet user-secrets set "EmailSettings:SmtpServer" "your-smtp-server"
dotnet user-secrets set "EmailSettings:Port" "587"
dotnet user-secrets set "EmailSettings:Username" "your-email"
dotnet user-secrets set "EmailSettings:Password" "your-password"
```

### Step 4: Database Setup

1. **Verify Database Connection**
```sh
# Check LocalDB status
sqllocaldb info
```

2. **Create and Update Database**
```sh
# Create initial database structure
dotnet ef database update

# Verify database creation
sqllocaldb info
```

3. **Seed Initial Data**
```sh
# Run the application with seed flag
dotnet run /seed
```

### Step 5: Running the Application

1. **Development Mode**

Using Visual Studio 2022:
- Open `DoableFinal.sln`
- Set startup project to `DoableFinal`
- Press `F5` to run with debugging
- Press `Ctrl + F5` to run without debugging

Using VS Code or Command Line:
```sh
# Run with hot reload
dotnet watch run

# Or run normally
dotnet run
```

2. **Access the Application**
- Open browser and navigate to: `https://localhost:5001`
- Default admin credentials (after seeding):
  - Username: admin@doable.com
  - Password: Admin123!

### Step 6: Common Issues & Troubleshooting

1. **Database Connection Issues**
   - Verify LocalDB is running: `sqllocaldb info`
   - Check connection string in appsettings.json
   - Ensure SQL Server service is running
   - Try recreating LocalDB instance:
     ```sh
     sqllocaldb stop "MSSQLLocalDB"
     sqllocaldb delete "MSSQLLocalDB"
     sqllocaldb create "MSSQLLocalDB"
     ```

2. **Build Errors**
   If you encounter build errors:
   ```sh
   dotnet clean
   dotnet restore
   dotnet build
   ```

3. **Migration Issues**
   If database update fails:
   ```sh
   # Remove existing migrations
   dotnet ef database drop -f
   dotnet ef migrations remove
   
   # Recreate migrations
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

4. **Runtime Errors**
   - Clear browser cache and cookies
   - Check application logs in:
     - Visual Studio Output window
     - `logs/` directory
     - Windows Event Viewer

### Step 7: Project Features

1. **User Management**
   - Role-based authentication (Admin, Project Manager, Employee, Client)
   - Profile management
   - Account archiving
   - Email notifications

2. **Project Management**
   - Project lifecycle tracking
   - Team assignment
   - Progress monitoring
   - Status updates

3. **Task Management**
   - Task creation and assignment
   - Priority management
   - Proof submission workflow
   - Task commenting
   - Archiving system

4. **Dashboard**
   - Role-specific views
   - Progress visualization
   - Performance metrics
   - Task monitoring

### Step 8: Security Features

1. **Authentication & Authorization**
   - Role-based access control (RBAC)
   - Secure password hashing
   - Session management

2. **Data Protection**
   - CSRF protection
   - SQL injection prevention
   - XSS protection
   - Secure file uploads

## � Support & Documentation

- **Documentation**
  - Check our [Wiki](https://github.com/Chell2003/DoableFinal/wiki)
  - Review code comments
  - See inline documentation

- **Issues & Help**
  - Create GitHub issues for bugs
  - Contact contributors
  - Check commit history for updates

- **Contributing**
  - Fork the repository
  - Create feature branches
  - Submit pull requests

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👨‍💻 Contributors
- **Chell2003** - [GitHub](https://github.com/Chell2003)
