FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar el archivo del proyecto
COPY DriverAI.API.csproj .
RUN dotnet restore

# Copiar el resto del código y publicar
COPY . .
RUN dotnet publish DriverAI.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# ==========================================
# INSTALAR LIBRERÍAS PARA POSTGRESQL
# ==========================================
RUN apt-get update && apt-get install -y \
    libkrb5-3 \
    libgssapi-krb5-2 \
    libssl3 \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Forzar confianza SSL
ENV PGSSLMODE=require

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "DriverAI.API.dll"]