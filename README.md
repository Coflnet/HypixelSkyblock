# HypixelSkyblock
This is the back-end for https://sky.coflnet.com 
You can get the same data and play around with it by using this project.

Some endpoins are exposed via REST, see the open-api docs: https://sky.coflnet.com/api


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
Hello there fellow developer. Development of this project is done with the following docker-compose file.
1. create a new folder, enter and clone this repository with `git clone --depth=1 -b separation https://github.com/Coflnet/HypixelSkyblock.git dev`
2. copy this and paste it into the file called `docker-compose.yml`
3. Now clone whatever project you like to develop with/need (indicated by `depends_on`). 
eg. `git clone https://github.com/Coflnet/SkyUpdater.git` and start it with `docker-compose up updater`
or `git clone https://github.com/Coflnet/SkyCommands.git` and start it with `docker-compose up commands`

```
version: '3.7'
services:
  mariadb:
    image: 'docker.io/bitnami/mariadb:10.3-debian-10'
    volumes:
      - './server/test_data:/bitnami'
    ports:
      - '3306:3306'
    environment:
      - MARIADB_ROOT_PASSWORD=takenfrombitnami
      - MARIADB_EXTRA_FLAGS=--innodb-buffer-pool-size=3G --key-buffer-size=2G --connect-timeout=101
  phpmyadmin:
    image: 'docker.io/bitnami/phpmyadmin:5-debian-10'
    ports:
      - '8038:8080'
      - '4438:8443'
    depends_on:
      - mariadb
  indexer:
    build: dev
    ports:
    - '8007:8008'
    - '1234:80'
    volumes:
    - ./server/ah:/data
    restart: always
    depends_on:
      - mariadb
      - kafka
      - redis
    environment: 
      FRONTEND_PROD: "frontend"
      JAEGER_SERVICE_NAME: "hypixel-skyblock-core"
      JAEGER_AGENT_HOST: "jaeger"
      KAFKA_HOST: "kafka:9092"
      SKYCOMMANDS_HOST: "skycommands:8008"
  commands:
    build: SkyCommands
    environment: 
      - JAEGER_AGENT_HOST=jaeger
      - KAFKA_HOST=kafka:9092
    ports:
    - "8008:8008"
    restart: always
    depends_on:
      - mariadb
      - kafka
      - redis
      - frontend
  redis:
    image: "redis:alpine"
    environment:
      - REDIS_REPLICATION_MODE=master
  imgproxy:
    image: willnorris/imageproxy
    command: -addr 0.0.0.0:80 -cache memory -allowHosts sky.shiiyu.moe,mc-heads.net,crafatar.com
  frontend:
    build: Hypixel-react
  jaeger:
    image: "jaegertracing/all-in-one:1.22"
    ports:
      - "16686:16686"
  zookeeper:
    image: docker.io/bitnami/zookeeper:3.7
    ports:
      - "2181:2181"
    volumes:
      - "zookeeper_data:/bitnami"
    environment:
      - ALLOW_ANONYMOUS_LOGIN=yes
  kafka:
    image: docker.io/bitnami/kafka:2
    ports:
      - "9092:9092"
    volumes:
      - "kafka_data:/bitnami"
    environment:
      - KAFKA_CFG_ZOOKEEPER_CONNECT=zookeeper:2181
      - ALLOW_PLAINTEXT_LISTENER=yes
    depends_on:
      - zookeeper
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 1500M
  mcconnect:
    build: SkyMcConnect
  subscription:
    build: SkySubscriptions
  updater:
    build: SkyUpdater
    environment: 
      SLOWDOWN_MS: 800
```