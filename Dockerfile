# Production image for Render (and `docker compose up` locally).
# SDK tag matches global.json so the container builds with the same SDK as CI.
FROM mcr.microsoft.com/dotnet/sdk:9.0.313 AS build
WORKDIR /src

# Restore first so the package layer is cached until a .csproj changes.
COPY global.json ./
COPY BusinessObjects/BusinessObjects.csproj BusinessObjects/
COPY DataAccessObjects/DataAccessObjects.csproj DataAccessObjects/
COPY Repositories/Repositories.csproj Repositories/
COPY Services/Services.csproj Services/
COPY QuizManagement/QuizManagement.csproj QuizManagement/
RUN dotnet restore QuizManagement/QuizManagement.csproj

COPY . .
RUN dotnet publish QuizManagement/QuizManagement.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
# Workstation GC keeps memory well under Render's 512 MB free-tier limit.
# PORT is overridden by Render; 8080 is the default for local containers.
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0 \
    PORT=8080
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuizManagement.dll"]
