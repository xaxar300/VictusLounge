# VictusLounge

WPF coursework project for managing a computer club: users, roles, computers, bookings, sessions, payments, shifts and tariffs.

## Requirements

- Windows
- .NET 9 SDK with WPF support
- Microsoft SQL Server available locally
- Default database name: `victus_lounge`

## Database

The default connection string is stored in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=victus_lounge;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

You can override it with the `VICTUS_DB` environment variable.

Common alternatives for another PC:

```powershell
$env:VICTUS_DB="Server=.\SQLEXPRESS;Database=victus_lounge;Trusted_Connection=True;TrustServerCertificate=True"
$env:VICTUS_DB="Server=(localdb)\MSSQLLocalDB;Database=victus_lounge;Trusted_Connection=True;TrustServerCertificate=True"
```

If the database does not exist, either let the application create it through Entity Framework Core or run:

```sql
Scripts/CreateDatabase.sql
```

## Run

```powershell
dotnet build VictusLounge.sln
dotnet run --project VictusLounge.csproj
```

## Seed Accounts

- Owner: `owner` / `owner123`
- Admin: `admin1` / `admin123`
- Admin: `admin2` / `admin456`
- Client: `client1` / `client123`

## Implementation Notes

- The WPF application targets `net9.0-windows`; it is intended to run on Windows.
- Localization focuses on navigation and main screen text; many operational toast messages remain Russian-only.
- `Shift.EmployeeName` is stored as display text instead of a foreign key to `User` to keep the coursework schema simple.
- `Payment.Amount` is used for both income and expenses; negative values represent cash expenses in the finance flow.
- Foreign keys use `Restrict` delete behavior so users/computers with history cannot be deleted accidentally.
- Seed data uses deterministic numeric IDs so related sample users, bookings and sessions stay stable between launches.
- User passwords are stored as SHA-256 hashes with a shared application salt.
- The admin operation log is persisted as service records in the payments history.
- Language switching walks the current WPF visual tree and updates visible controls in place.
