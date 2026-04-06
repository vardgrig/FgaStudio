FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["FgaStudio.Web/FgaStudio.Web.csproj", "FgaStudio.Web/"]
RUN dotnet restore "FgaStudio.Web/FgaStudio.Web.csproj"
COPY . .
RUN dotnet publish "FgaStudio.Web/FgaStudio.Web.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Data directory for the SQLite database
RUN mkdir -p /data

ENV ASPNETCORE_URLS=http://+:8080
ENV FgaStudio__DbPath=/data/fgastudio.db

ENTRYPOINT ["dotnet", "FgaStudio.Web.dll"]
