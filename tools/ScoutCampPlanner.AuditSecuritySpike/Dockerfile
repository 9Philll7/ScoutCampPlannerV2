FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /source

COPY Directory.Build.props Directory.Packages.props ./
COPY tools/ScoutCampPlanner.AuditSecuritySpike/ScoutCampPlanner.AuditSecuritySpike.csproj tools/ScoutCampPlanner.AuditSecuritySpike/
RUN dotnet restore tools/ScoutCampPlanner.AuditSecuritySpike/ScoutCampPlanner.AuditSecuritySpike.csproj

COPY tools/ScoutCampPlanner.AuditSecuritySpike/ tools/ScoutCampPlanner.AuditSecuritySpike/

ENTRYPOINT ["dotnet", "run", "--configuration", "Release", "--no-restore", "--project", "tools/ScoutCampPlanner.AuditSecuritySpike/ScoutCampPlanner.AuditSecuritySpike.csproj"]
