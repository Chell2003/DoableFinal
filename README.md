# DoableFinal

DoableFinal is a comprehensive project management system built with ASP.NET Core MVC and Entity Framework Core. It provides a robust platform for managing projects, tasks, and team collaboration with role-based access control.

---

## 🚀 Features

### User Management
- Role-based authentication (Admin, Project Manager, Employee, Client)
- User profile management with customizable settings
- Account archiving and reactivation system
- Email notifications system with toggle options

### Project Management
- Comprehensive project lifecycle management
- Project archiving system
- Real-time project progress tracking
- Team member assignment and management
- Project status monitoring and updates

### Task Management
- Task creation and assignment system
- Priority-based task organization
- Task proof submission and approval workflow
- Task commenting and collaboration features
- Task archiving functionality

### Dashboard Features
- Role-specific dashboards for all user types
- Real-time project progress visualization
- Task completion statistics
- Team performance metrics
- Overdue task monitoring

---

## 📥 Installation

### Prerequisites
Before you begin, ensure you have the following installed:
- **.NET 7.0 SDK** or later
- **SQL Server LocalDB** (included with SQL Server Express)
- **Visual Studio 2022** (recommended) or **VS Code** with C# extension
- **Git** for version control

### 1️⃣ **Clone and Setup**
1. Clone the repository:
```sh
git clone https://github.com/yourusername/DoableFinal.git
cd DoableFinal
```

2. Install required .NET tools:
```sh
# Install Entity Framework Core tools
dotnet tool install --global dotnet-ef

# Install HTTPS development certificate
dotnet dev-certs https --trust
```

### 2️⃣ **Dependencies and Configuration**
1. Restore NuGet packages:
```sh
dotnet restore
```

2. Create and configure settings:
```sh
# Copy example settings file
cp appsettings.example.json appsettings.json

# Create user secrets (for development)
dotnet user-secrets init
```

3. Configure email settings in user secrets (optional):
```sh
dotnet user-secrets set "EmailSettings:SmtpServer" "your-smtp-server"
dotnet user-secrets set "EmailSettings:Port" "587"
dotnet user-secrets set "EmailSettings:Username" "your-email"
dotnet user-secrets set "EmailSettings:Password" "your-password"
```

### 3️⃣ **Database Setup**
1. Verify LocalDB installation:
```sh
sqllocaldb info
```

2. Create and update database:
```sh
# Create initial migration if not exists
dotnet ef migrations add InitialCreate

# Apply migrations to create/update database
dotnet ef database update
```

3. Seed initial data (if needed):
```sh
dotnet run /seed
```

### 4️⃣ **Development Environment Setup**
1. Install recommended VS Code extensions:
   - C# Dev Kit
   - Entity Framework Core Tools
   - SQL Server Tools
   - Azure Tools

2. Configure user secrets in VS Code:
   - Install "Azure Key Vault" extension
   - Configure development secrets

---

## ▶️ Running the Application

### Development Mode
1. Using .NET CLI:
```sh
dotnet watch run
```

2. Using Visual Studio:
   - Open `DoableFinal.sln`
   - Set environment to "Development"
   - Press `F5` to run with debugging
   - Press `Ctrl + F5` to run without debugging

### Production Mode
1. Build the application:
```sh
dotnet publish -c Release
```

2. Run the published application:
```sh
cd bin/Release/net7.0/publish
dotnet DoableFinal.dll
```

### Docker Support (Optional)
1. Build the Docker image:
```sh
docker build -t doablefinal .
```

2. Run the container:
```sh
docker run -p 8080:80 doablefinal
```

---

## ⚙️ Configuration

### **Setting Up `appsettings.json`**
Since `appsettings.json` is ignored in the repository, you need to create it manually.

1. **Copy the example file**:
   ```sh
   cp appsettings.example.json appsettings.json
   ```
   or manually rename `appsettings.example.json` to `appsettings.json`.

2. **Modify `appsettings.json`** if needed:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=DoableFinal;Trusted_Connection=True;"
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

---

## 🔧 Troubleshooting

### ❓ **Database Issues**
If `dotnet ef database update` fails, ensure:
- You have **SQL Server LocalDB** installed  
- Run this command to check your LocalDB:
  ```sh
  sqllocaldb info
  ```
- Check your database connection string in `appsettings.json`
- Ensure you have necessary permissions to create/modify databases

### ❓ **Missing Dependencies**
If the app doesn't build, try:
```sh
dotnet clean
dotnet restore
dotnet build
```

### ❓ **Common Issues**
1. **User Authentication Issues**
   - Clear browser cookies and cache
   - Check user role assignments in the database
   - Verify email confirmation status if required

2. **File Upload Problems**
   - Check folder permissions for upload directories
   - Verify file size limits in configuration
   - Ensure proper MIME types are allowed

3. **Performance Issues**
   - Check database indexing
   - Monitor database connection pool
   - Review query performance with logging enabled

---

## 🔐 Security Features
- Role-based access control (RBAC)
- Secure password hashing
- Cross-Site Request Forgery (CSRF) protection
- SQL injection prevention
- XSS attack protection
- Secure file upload validation

---

## 👨‍💻 Contributors
- **Chell2003** - [GitHub](https://github.com/Chell2003)

---

## 📩 Need Help?
- Check our [Wiki](https://github.com/Chell2003/DoableFinal/wiki) for detailed documentation
- Open an issue on GitHub for bug reports or feature requests
- Contact the contributors for direct assistance
- Review our [Contributing Guidelines](CONTRIBUTING.md) if you want to contribute

---

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
