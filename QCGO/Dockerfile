# Use the official ASP.NET Core runtime as a base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Use the .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore QCGO.csproj
RUN dotnet publish QCGO.csproj -c Release -o /app/publish

# Build runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Set environment to Production to avoid file watcher/inotify errors
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "QCGO.dll"]
