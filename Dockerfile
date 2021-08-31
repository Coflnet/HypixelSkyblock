FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
WORKDIR /build/skyblock
COPY hypixel.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c release

FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app

COPY --from=build /build/skyblock/bin/Release/netcoreapp3.1/publish/ .
RUN mkdir -p ah/files
#COPY --from=frontend /build/build/ /data/files

ENTRYPOINT ["dotnet", "hypixel.dll", "/data", "f"]

VOLUME /data

