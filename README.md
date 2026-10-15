# WorkoutTracker

[![CI](https://github.com/fdaniel02/WorkoutTracker/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/fdaniel02/WorkoutTracker/actions/workflows/ci.yml)

## Project layout

```
WorkoutTracker.slnx
src/WorkoutTracker/          the app (Blazor Server)
tests/WorkoutTracker.Tests/  unit, component (bUnit) and SQL Server tests (xUnit v3)
scripts/                     SQL script for the database login
.github/workflows/ci.yml     build and test on every pull request and push to main
```

NuGet versions are locked in `packages.lock.json` (enabled in `Directory.Build.props`); CI restores in locked mode.
After changing a package, commit the updated lock file too.

## Build and test

```powershell
dotnet build
dotnet run --project src/WorkoutTracker
dotnet test --coverlet
```

Tests run on Microsoft Testing Platform (set in `global.json`). `--coverlet` writes an OpenCover report
to `TestResults/coverage.opencover.*.xml`; the settings are in `tests/WorkoutTracker.Tests/testconfig.json`.

Some tests run against a real SQL Server in a container (Testcontainers), so Docker must be running.
The first run downloads the SQL Server image (about 1.5 GB).

## Configuration

The app needs the connection string `ConnectionStrings:Default`. It is not stored in `appsettings.json`;
the app refuses to start without it.

The database login is `workouttracker`, created by [scripts/create-sql-login.sql](scripts/create-sql-login.sql).
It only has rights to the `WorkoutTracker` database. Don't use `sa`.

### Time zone

"Today", "N days ago" and the heatmap use the time zone in the `TimeZone` setting (an IANA id,
default `Europe/Amsterdam` in `appsettings.json`), not the server's. Override it in Docker with `-e TimeZone=...`.

### Logging

Logs go to stdout as one JSON object per line, for a log collector (e.g. Grafana Alloy → Loki) to pick up.
Locally (`Development`) they use the plain console format. Each log message has a fixed event id (see `src/WorkoutTracker/Log.cs`).

At startup the app applies database migrations. While SQL Server is unreachable it tries 15 times, 4 seconds apart, and logs
each failed attempt as a warning. If the host doesn't answer at all, each attempt also waits for the connect timeout (15 s by default). Errors that waiting won't fix (wrong password, missing database, locked or expired login) stop it at once.
A startup failure is logged as `Critical` and the process exits with code 1.

### Local development

The connection string lives in user-secrets (stored in `%APPDATA%\Microsoft\UserSecrets\`, outside the project):

```powershell
dotnet user-secrets set --project src/WorkoutTracker "ConnectionStrings:Default" "Server=<sql-host>;Database=WorkoutTracker;User Id=workouttracker;Password=<password>;TrustServerCertificate=True"
```

User-secrets are only loaded when `ASPNETCORE_ENVIRONMENT=Development` (the default in `launchSettings.json`).

### Docker

Build the image from the repository root:

```powershell
docker build -t workouttracker .
```

Pass the connection string as an environment variable (`__` stands for `:`):

```powershell
docker run -d -p 8080:8080 `
  -e ConnectionStrings__Default="Server=<sql-host>;Database=WorkoutTracker;User Id=workouttracker;Password=<password>;TrustServerCertificate=True" `
  workouttracker
```

To keep the password out of your shell history, put it in a file and use `--env-file`:

```powershell
docker run -d -p 8080:8080 --env-file workouttracker.env workouttracker
```

`workouttracker.env` contains a single line, without quotes:

```
ConnectionStrings__Default=Server=<sql-host>;Database=WorkoutTracker;User Id=workouttracker;Password=<password>;TrustServerCertificate=True
```

Keep that file outside the project folder, so it can't end up in the image or in git.
