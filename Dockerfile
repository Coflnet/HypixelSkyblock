FROM mcr.microsoft.com/dotnet/sdk:5.0 as build
WORKDIR /build/skyblock
COPY hypixel.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c release

FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app

COPY --from=build /build/skyblock/bin/Release/net5.0/publish/ .
RUN mkdir -p ah/files
#COPY --from=frontend /build/build/ /data/files

ENTRYPOINT ["dotnet", "hypixel.dll", "/data", "f"]

VOLUME /data

