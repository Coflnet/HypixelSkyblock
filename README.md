# HypixelSkyblock
This is the back-end for https://sky.coflnet.com 
You can get the same data and play around with it by using this project.

Some endpoints are exposed via REST, see the open-api docs: https://sky.coflnet.com/api


## Kafka topics
This project uses a kafka server to distribute workloads.  
Topics produced are:
* `sky-newauction`
* `sky-newbid`
* `sky-soldauction`
* `sky-canceledauction`
* `sky-endedauction`
* `sky-bazaarprice`  
* `sky-update-player` (players whose names should be updated)
* `sky-updated-player`  (players who got updated)
* `sky-flips`  found flips, producer: flipper, consumer: light-clients

You can modify them by changing appsettings.json or setting the enviroment variables.
To get a full list check appsettings.json.  
Note that to set them as enviroment variables you have to prefix them with `TOPICS__` because you can't add `:` in an env variable.  
Example:  
To set `"MISSING_AUCTION":"sky-canceledauction"` you have to set `TOPICS__MISSING_AUCTION=mycooltopic`

## Get started/usage
Hello there fellow developer. Development of this project is done with docker-compose.
1. Install docker and docker-compose if you are a windows user these come with docker desktop.
1. create a new folder `skyblock`, enter it and clone this repository with `git clone --depth=1 -b separation https://github.com/Coflnet/HypixelSkyblock.git dev`
2. copy `docker-compose.yml` to the `skyblock` folder
3. Now clone whatever project you like to develop with/need (also indicated by `depends_on` in `docker-compose.yml`). 
eg. `git clone https://github.com/Coflnet/SkyUpdater.git` and start it with `docker-compose up updater`
or `git clone https://github.com/Coflnet/SkyCommands.git` and start it with `docker-compose up commands`

For basic website functunality you need
* this repo
* SkyCommands
* Hypixel-react (frontend)
* SkyUpdater (downloading process)

#### Scenario
You update something in `SkyCommands`. You cloned all repos in the right structure. 
Since you only care about the `commands` service you start all others in the background with: `docker-compose up -d indexer updater`
Now you build and start `commands` with `docker-compose up --build commands` 
