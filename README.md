# WorkoutTracker

[![CI](https://github.com/fdaniel02/WorkoutTracker/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/fdaniel02/WorkoutTracker/actions/workflows/ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=fdaniel02_WorkoutTracker&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=fdaniel02_WorkoutTracker)

## Project layout

```
WorkoutTracker.slnx
src/WorkoutTracker/          the app (Blazor Server)
tests/WorkoutTracker.Tests/  unit, component (bUnit) and PostgreSQL tests (xUnit v3)
.github/workflows/ci.yml     build and test on every pull request and push to main
.github/dependabot.yml       weekly update pull requests for NuGet packages and GitHub Actions
```

NuGet versions are locked in `packages.lock.json` (enabled in `Directory.Build.props`); CI restores in locked mode.
After changing a package, commit the updated lock file too.

The lock file also holds a package the SDK adds implicitly, whose version follows the SDK. So when you change the
SDK version in `global.json`, regenerate the lock files in the same pull request:

```powershell
dotnet restore --force-evaluate
```

The Docker image builds with the newest .NET 10 SDK and runtime (`sdk:10.0` and `aspnet:10.0`) and restores without
locked mode, so it picks up .NET patches on every build without depending on `global.json`.

## Build and test

```powershell
dotnet build
dotnet run --project src/WorkoutTracker
dotnet test --coverlet
```

Tests run on Microsoft Testing Platform (set in `global.json`). `--coverlet` writes an OpenCover report
to `TestResults/coverage.opencover.*.xml`; the settings are in `tests/WorkoutTracker.Tests/testconfig.json`.

Some tests run against a real PostgreSQL in a container (Testcontainers, image `postgres:18`), so Docker must be running.

## Configuration

The app needs the connection string `ConnectionStrings:Default`. It is not stored in `appsettings.json`;
the app refuses to start without it.

In the cluster the app uses PostgreSQL: the database `workouttracker`, owned by the login `workouttracker`, which has
no rights elsewhere. Both are created by the cluster (cluster-gitops, `infrastructure/postgres`), not by the app; the
app only applies its migrations. The cluster archives the database to object storage continuously, so it can be
restored to any point in time within 30 days.

Workout type and routine names are compared without regard to case ("Push ups" and "push ups" are the same name),
through the ICU collation `case_insensitive` that the first migration creates.

### Time zone

"Today", "N days ago" and the heatmap use the time zone in the `TimeZone` setting (an IANA id,
default `Europe/Amsterdam` in `appsettings.json`), not the server's. Override it in Docker with `-e TimeZone=...`.

### Logging

Logs go to stdout as one JSON object per line, for a log collector (e.g. Grafana Alloy → Loki) to pick up.
Locally (`Development`) they use the plain console format. Each log message has a fixed event id (see `src/WorkoutTracker/Log.cs`).

At startup the app applies database migrations. While PostgreSQL is unreachable it tries 15 times, 4 seconds apart, and logs
each failed attempt as a warning. If the host doesn't answer at all, each attempt also waits for the connect timeout (15 s by default). Errors that waiting won't fix (wrong password, missing database, no permission to connect) stop it at once.
A startup failure is logged as `Critical` and the process exits with code 1.

### Local development

Run PostgreSQL in Docker; the app creates the database and its tables on the first start:

```powershell
docker run -d --name workouttracker-db -p 5432:5432 -e POSTGRES_USER=workouttracker -e POSTGRES_PASSWORD=<password> postgres:18
```

The connection string lives in user-secrets (stored in `%APPDATA%\Microsoft\UserSecrets\`, outside the project):

```powershell
dotnet user-secrets set --project src/WorkoutTracker "ConnectionStrings:Default" "Host=localhost;Database=workouttracker;Username=workouttracker;Password=<password>"
```

User-secrets are only loaded when `ASPNETCORE_ENVIRONMENT=Development` (the default in `launchSettings.json`).

The cluster's database isn't reachable from the LAN. For a one-off look or fix, forward it to this machine; it stays
reachable on `localhost:5433` while the command runs. The second command prints the login's password:

```sh
kubectl port-forward -n postgres svc/postgres-rw 5433:5432
kubectl get secret -n postgres postgres-workouttracker -o jsonpath='{.data.password}' | base64 -d; echo
```

### Docker

Every push to `main` publishes the image to GitHub Container Registry, tagged `sha-<short commit sha>` and `latest`,
after the tests pass and a Trivy scan finds no fixable critical vulnerabilities:

```powershell
docker pull ghcr.io/fdaniel02/workouttracker:latest
```

Or build the image yourself from the repository root:

```powershell
docker build -t workouttracker .
```

The container runs as the non-root `app` user.

Pass the connection string as an environment variable (`__` stands for `:`):

```powershell
docker run -d -p 8080:8080 `
  -e ConnectionStrings__Default="Host=<db-host>;Database=workouttracker;Username=workouttracker;Password=<password>" `
  workouttracker
```

To keep the password out of your shell history, put it in a file and use `--env-file`:

```powershell
docker run -d -p 8080:8080 --env-file workouttracker.env workouttracker
```

`workouttracker.env` contains a single line, without quotes:

```
ConnectionStrings__Default=Host=<db-host>;Database=workouttracker;Username=workouttracker;Password=<password>
```

Keep that file outside the project folder, so it can't end up in the image or in git.
