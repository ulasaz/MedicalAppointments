FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Menu.API/Menu.API.csproj", "Menu.API/"]
COPY ["Shared.Contracts/Shared.Contracts.csproj", "Shared.Contracts/"]
RUN dotnet restore "Menu.API/Menu.API.csproj"
COPY . .
WORKDIR "/src/Menu.API"
RUN dotnet build "./Menu.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Menu.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
USER root
COPY --from=publish /app/publish .
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN chmod +x /app/.playwright/node/linux-x64/node \
    && /app/.playwright/node/linux-x64/node /app/.playwright/package/cli.js install chromium --with-deps \
    && chmod -R o+rx /ms-playwright
USER $APP_UID
ENTRYPOINT ["dotnet", "Menu.API.dll"]
