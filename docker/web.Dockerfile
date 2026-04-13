FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY web/KotobaColiseum.Web/KotobaColiseum.Web.csproj web/KotobaColiseum.Web/
RUN dotnet restore web/KotobaColiseum.Web/KotobaColiseum.Web.csproj

COPY web/KotobaColiseum.Web/ web/KotobaColiseum.Web/
RUN dotnet publish web/KotobaColiseum.Web/KotobaColiseum.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY configs/runtime.defaults.json /app/config/runtime.defaults.json
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "KotobaColiseum.Web.dll"]
