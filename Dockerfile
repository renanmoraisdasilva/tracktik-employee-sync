FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props global.json ./
COPY src/EmployeeSync.Domain/EmployeeSync.Domain.csproj src/EmployeeSync.Domain/
COPY src/EmployeeSync.Application/EmployeeSync.Application.csproj src/EmployeeSync.Application/
COPY src/EmployeeSync.Infrastructure/EmployeeSync.Infrastructure.csproj src/EmployeeSync.Infrastructure/
COPY src/EmployeeSync.Api/EmployeeSync.Api.csproj src/EmployeeSync.Api/

RUN dotnet restore src/EmployeeSync.Api/EmployeeSync.Api.csproj

COPY src/ src/
RUN dotnet publish src/EmployeeSync.Api/EmployeeSync.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /app/publish .
USER app

ENTRYPOINT ["dotnet", "EmployeeSync.Api.dll"]
