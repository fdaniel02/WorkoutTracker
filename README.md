# WorkoutTracker

[![CI](https://github.com/fdaniel02/WorkoutTracker/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/fdaniel02/WorkoutTracker/actions/workflows/ci.yml)

## Project layout

```
WorkoutTracker.slnx
src/WorkoutTracker/          the app (Blazor Server)
tests/WorkoutTracker.Tests/  unit tests (xUnit v3)
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

## Configuration

The app needs the connection string `ConnectionStrings:Default`. It is not stored in `appsettings.json`;
the app refuses to start without it.

The database login is `workouttracker`, created by [scripts/create-sql-login.sql](scripts/create-sql-login.sql).
It only has rights to the `WorkoutTracker` database. Don't use `sa`.

### Time zone

"Today", "N days ago" and the heatmap use the time zone in the `TimeZone` setting (an IANA id,
default `Europe/Amsterdam` in `appsettings.json`), not the server's. Override it in Docker with `-e TimeZone=...`.

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
