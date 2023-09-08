#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base

ENV CONN_DB='User ID=postgres;Password=postgres;Host=localhost;Port=5432;Database=vest;Pooling=true;Connection Lifetime=0;'

WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

WORKDIR /src
COPY ["EPS_Vest_TC/EPS_Vest_TC.csproj", "EPS_Vest_TC/"]
RUN dotnet restore "EPS_Vest_TC/EPS_Vest_TC.csproj"
COPY . .
WORKDIR "/src/EPS_Vest_TC"
RUN dotnet build "EPS_Vest_TC.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EPS_Vest_TC.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EPS_Vest_TC.dll"]
CMD [ "arg0" ]