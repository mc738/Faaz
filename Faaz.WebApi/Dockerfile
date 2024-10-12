FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Faaz.WebApi/Faaz.WebApi.fsproj", "Faaz.WebApi/"]
RUN dotnet restore "Faaz.WebApi/Faaz.WebApi.fsproj"
COPY . .
WORKDIR "/src/Faaz.WebApi"
RUN dotnet build "Faaz.WebApi.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Faaz.WebApi.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Faaz.WebApi.dll"]
