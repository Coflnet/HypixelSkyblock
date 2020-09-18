# HypixelSkyblock
This is the back-end for https://skyblock.coflnet.com 
You can get the same data and play around with it by following using this project.
If you want historic data you can purchase it from me (to cover my server bills).

### Installation
1. Install dotnet core and docker + docker-compose
2. Clone the project `git clone https://github.com/Ekwav/HypixelSkyblock.git`
3. Copy the file `appsettings.json` and name it `custom.conf.json`
4. get your api-key by typing `/api` while logged in on the server and write it into `custom.conf.json`
4. Start it all up with `docker-compose up -d`

#### Uninstall
* run `docker-compose down`
* remove the folder 
