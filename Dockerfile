# Dockerfile optimisé pour Api_BuildTech
# Structure: Api_BuildTech.csproj à la racine
# ════════════════════════════════════════════════════════════════════════════

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copier le fichier projet
COPY ["Api_BuildTech.csproj", "./"]

# Restaurer les dépendances
RUN dotnet restore "Api_BuildTech.csproj"

# Copier tout le code
COPY . .

# Build en Release
RUN dotnet build "Api_BuildTech.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ════════════════════════════════════════════════════════════════════════════
# Stage 2: Publish
FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "Api_BuildTech.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ════════════════════════════════════════════════════════════════════════════
# Stage 3: Runtime (BASE)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

WORKDIR /app

EXPOSE 8080
EXPOSE 8081

# ✅ IMPORTANT: Installer les fonts ICI (pas dans BUILD!)
# Les fonts doivent être dans le stage FINAL pour que l'app y ait accès
RUN apt-get update && apt-get install -y \
    fonts-dejavu \
    fonts-liberation \
    fonts-liberation2 \
    fonts-noto-core \
    fontconfig \
    && fc-cache -f -v \
    && rm -rf /var/lib/apt/lists/*

# ════════════════════════════════════════════════════════════════════════════
# Stage 4: Final
FROM base AS final

WORKDIR /app

# Copier les fichiers publiés depuis le stage PUBLISH
COPY --from=publish /app/publish .

# ✅ LABELS pour les métadonnées
LABEL maintainer="Wilfried Konan <wilfriedkonan@cocoprojects.com>"
LABEL description="Api_BuildTech - .NET 8.0 API"
LABEL version="1.0"

# ✅ Health check
#HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    #CMD dotnet healthcheck Api_BuildTech.dll || exit 1

# Point d'entrée
ENTRYPOINT ["dotnet", "Api_BuildTech.dll"]
