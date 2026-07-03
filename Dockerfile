# Dockerfile optimisé pour Api_BuildTech
# Structure: Api_BuildTech.csproj à la racine

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# ✅ COPIER À LA RACINE (pas dans Api_BuildTech/)
COPY ["Api_BuildTech.csproj", "./"]

# Restaurer les dépendances
RUN dotnet restore "Api_BuildTech.csproj"

# Copier tout le code
COPY . .

# Build en Release
RUN dotnet build "Api_BuildTech.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 2: Publish
FROM build AS publish

ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Api_BuildTech.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Stage 4: Final
FROM base AS final

WORKDIR /app

# Copier les fichiers publiés
COPY --from=publish /app/publish .

# Point d'entrée
ENTRYPOINT ["dotnet", "Api_BuildTech.dll"]