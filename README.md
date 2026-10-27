# BudgetFlow

A banking application for managing a monthly budget, built with ASP.NET Core 8, Entity Framework and Redis Cache.

## Features

- **Dashboard** with statistics and charts for expenses and income
- **Bills** with the option to pay directly from the account
- **Recurring payments** processed automatically every day at 08:00 by a Background Worker
- **Monthly reports** with Redis cache (1h TTL) — the UI clearly indicates whether the data comes from the cache or is calculated live

---

## Demo Accounts

| Email | Password | Role |
|---|---|---|
| admin@bankingapp.com | Admin@123 | Admin |
| demo@bankingapp.com | Demo@123 | User |

---

## Setup

### 1. Configure the connection string

Edit `src/BankingApp.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=BankingAppDb;Trusted_Connection=True;TrustServerCertificate=True;",
    "Redis": "localhost:6379"
  }
}
```

### 2. Database migration

```powershell
cd src/BankingApp.API
dotnet ef migrations add InitialCreate --project "../BankingApp.Infrastructure" --startup-project "."
dotnet ef database update --project "../BankingApp.Infrastructure" --startup-project "."
```

### 3. Redis (optional)

The application works without Redis — if it is not available, reports are calculated directly, without caching.

```bash
# With Docker:
docker run -d -p 6379:6379 redis
```

### 4. Run

```bash
dotnet run --project src/BankingApp.API
```

Navigate to `https://localhost:5001`

---

## Architecture

```
BankingApp/
├── src/
│   ├── BankingApp.Core/           # Entities, DTOs, Interfaces, Enums, Exceptions
│   ├── BankingApp.Infrastructure/ # EF Core, Repositories, Services, Workers
│   │   ├── Data/                  # AppDbContext, SeedData
│   │   ├── Repositories/
│   │   ├── Services/
│   │   │   └── ReportService.cs   # Redis cache logic
│   │   └── Workers/
│   │       └── RecurringPaymentWorker.cs  # Background Worker (daily at 08:00)
│   └── BankingApp.API/            # Controllers, Razor Pages, wwwroot
```

---

## Technical Details

### Background Worker

`RecurringPaymentWorker` extends `BackgroundService` and runs daily at 08:00:

- processes all due recurring payments
- on the first day of the month, generates the reports for the previous month

Registered in `Program.cs` with `AddHostedService<RecurringPaymentWorker>()`.

### Redis Cache

`ReportService` caches monthly reports with the key `report:{accountId}:{year}:{month}` and a TTL of 1 hour.

- **Cache HIT** → instant response, the UI displays a green banner *"From Redis cache"*
- **Cache MISS** → live calculation, saved to Redis and the DB, the UI displays a purple banner *"Calculated now"*
- If Redis is not available, the application continues without cache (graceful fallback, warning in the log)

---

## NuGet Packages

### BankingApp.Core
| Package | Version |
|---|---|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.11 |
| Microsoft.EntityFrameworkCore | 8.0.11 |

### BankingApp.Infrastructure
| Package | Version |
|---|---|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.11 |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 |
| Microsoft.EntityFrameworkCore.Tools | 8.0.11 |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.1 |
| Serilog.AspNetCore | 8.0.0 |
| Serilog.Sinks.Console | 5.0.0 |
| Serilog.Sinks.File | 5.0.0 |
| StackExchange.Redis | 2.8.16 |

### BankingApp.API
| Package | Version |
|---|---|
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.11 |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 |
| Serilog.AspNetCore | 8.0.0 |
| Serilog.Sinks.Console | 5.0.0 |
| Serilog.Sinks.File | 5.0.0 |
