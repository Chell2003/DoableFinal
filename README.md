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
### 2️⃣ **Restore Dependencies
Run the following command to restore NuGet packages:
```sh
dotnet restore
```
###3️⃣ **Set Up the Database
This project uses Entity Framework Core and LocalDB.
✅ Apply Migrations
Run the following command to create the database:
```sh
dotnet ef database update
```
⚠️ Note: Ensure you have the EF Core tools installed. If not, install them using:
```sh
dotnet tool install --global dotnet-ef
```
▶️ Running the Application
