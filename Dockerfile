FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# Copiar solo el archivo .csproj primero (mejor para caché)
COPY DriverAI.API.csproj .

# Restaurar dependencias
RUN dotnet restore

# Copiar todo el resto del código
COPY . .

# Publicar la aplicación
RUN dotnet publish DriverAI.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

# Copiar los archivos publicados desde la etapa de build
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "DriverAI.API.dll"]