
# ------------------------------------------
# HELIOS API - BUILD STAGE
# ------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy project files and source code
COPY . .

# Restore dependencies
RUN dotnet restore "backend/Helios.Api/Helios.Api.csproj"

# Publish the application
RUN dotnet publish "backend/Helios.Api/Helios.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ------------------------------------------
# HELIOS API - RUNTIME STAGE
# ------------------------------------------

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Copy published application
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Helios.Api.dll"]
