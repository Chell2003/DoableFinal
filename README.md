# DoableFinal

DoableFinal is an ASP.NET Core MVC application using Entity Framework Core with SQL Server LocalDB.  
This guide will help you set up the project after cloning it from GitHub.

---

## 🚀 Features
- User authentication and profile management  
- Task and project management  
- Admin and client dashboards  
- Database integration with Entity Framework Core  

---

## 📥 Installation

### 1️⃣ **Clone the Repository**
Open a terminal and run:
```sh
git clone https://github.com/yourusername/DoableFinal.git
cd DoableFinal
```

### 2️⃣ **Restore Dependencies**
Run the following command to restore NuGet packages:
```sh
dotnet restore
```

### 3️⃣ **Set Up the Database**
This project uses **Entity Framework Core** and **LocalDB**.

#### ✅ **Apply Migrations**
Run the following command to create the database:
```sh
dotnet ef database update
```
> ⚠️ **Note:** Ensure you have the EF Core tools installed. If not, install them using:
```sh
dotnet tool install --global dotnet-ef
```

---

## ▶️ Running the Application

### 4️⃣ **Start the Application**
Run the following command:
```sh
dotnet run
```
or if using Visual Studio:
1. Open `DoableFinal.sln`
2. Press `Ctrl + F5` to run

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

### ❓ **Missing Dependencies**
If the app doesn't build, try:
```sh
dotnet clean
dotnet restore
dotnet build
```

---

## 👨‍💻 Contributors
- **Your Name** - [GitHub](https://github.com/Chell2003)

---

## 📩 Need Help?
Open an issue on GitHub or contact the contributors!
