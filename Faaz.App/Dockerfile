FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Faaz.App/Faaz.App.fsproj", "Faaz.App/"]
RUN dotnet restore "Faaz.App/Faaz.App.fsproj"
COPY . .
WORKDIR "/src/Faaz.App"
RUN dotnet build "Faaz.App.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Faaz.App.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Faaz.App.dll"]
