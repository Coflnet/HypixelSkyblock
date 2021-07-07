FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
WORKDIR /build
RUN git clone https://github.com/Ekwav/websocket-sharp
RUN mkdir -p /build/skyblock/External/api
RUN git clone https://github.com/Ekwav/Hypixel.NET.git /build/skyblock/External/api
WORKDIR /build/skyblock
COPY . .
RUN touch keyfile.p12 
RUN cp -n appsettings.json custom.conf.json
RUN dotnet restore
RUN dotnet publish -c release

FROM node:alpine as frontend
RUN apk add git
WORKDIR /build
RUN git clone https://github.com/Coflnet/hypixel-react.git .
RUN npm i
RUN npm run build
RUN ls


FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app

COPY --from=build /build/skyblock/bin/Release/netcoreapp3.1/publish/ .
RUN mkdir -p ah/files
COPY --from=frontend /build/build/ /data/files

ENTRYPOINT ["dotnet", "hypixel.dll", "/data", "f"]

VOLUME /data

