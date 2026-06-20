# syntax=docker/dockerfile:1

# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY websocket-server.csproj ./
RUN dotnet restore websocket-server.csproj
COPY . .
RUN dotnet publish websocket-server.csproj -c Release -o /app /p:UseAppHost=false

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://0.0.0.0:5001
EXPOSE 5001
ENTRYPOINT ["dotnet", "websocket-server.dll"]
