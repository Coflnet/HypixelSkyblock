FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app

COPY  ./bin/Release/netcoreapp3.1/publish/ .

ENTRYPOINT ["dotnet", "hypixel.dll", "/data", "f"]

VOLUME /data

