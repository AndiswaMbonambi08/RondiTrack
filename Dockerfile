# syntax=docker/dockerfile:1

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first so the restore layer is cached
COPY RondiTrack.csproj ./
COPY RondiTrack.Domain/RondiTrack.Domain.csproj RondiTrack.Domain/
COPY RondiTrack.Infrastructure/RondiTrack.Infrastructure.csproj RondiTrack.Infrastructure/
RUN dotnet restore RondiTrack.csproj

# Now copy the source (filtered by .dockerignore) and publish
COPY . .
RUN dotnet publish RondiTrack.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Built-in non-root user of the aspnet image
USER $APP_UID

ENTRYPOINT ["dotnet", "RondiTrack.dll"]