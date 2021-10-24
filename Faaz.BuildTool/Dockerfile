FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["Faaz.BuildTool/Faaz.BuildTool.fsproj", "Faaz.BuildTool/"]
RUN dotnet restore "Faaz.BuildTool/Faaz.BuildTool.fsproj"
COPY . .
WORKDIR "/src/Faaz.BuildTool"
RUN dotnet build "Faaz.BuildTool.fsproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Faaz.BuildTool.fsproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Faaz.BuildTool.dll"]
