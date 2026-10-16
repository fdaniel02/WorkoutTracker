FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/WorkoutTracker/WorkoutTracker.csproj src/WorkoutTracker/packages.lock.json src/WorkoutTracker/
RUN dotnet restore src/WorkoutTracker/WorkoutTracker.csproj --locked-mode
COPY src/ src/
RUN dotnet publish src/WorkoutTracker/WorkoutTracker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "WorkoutTracker.dll"]
