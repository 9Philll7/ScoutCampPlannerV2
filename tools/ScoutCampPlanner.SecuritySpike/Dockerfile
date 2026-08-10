FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /source

COPY Directory.Build.props Directory.Packages.props ./
COPY tools/ScoutCampPlanner.SecuritySpike/ScoutCampPlanner.SecuritySpike.csproj tools/ScoutCampPlanner.SecuritySpike/
RUN dotnet restore tools/ScoutCampPlanner.SecuritySpike/ScoutCampPlanner.SecuritySpike.csproj

COPY tools/ScoutCampPlanner.SecuritySpike/ tools/ScoutCampPlanner.SecuritySpike/

ENTRYPOINT ["dotnet", "run", "--configuration", "Release", "--no-restore", "--project", "tools/ScoutCampPlanner.SecuritySpike/ScoutCampPlanner.SecuritySpike.csproj"]
