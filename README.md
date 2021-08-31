# HypixelSkyblock
This is the back-end for https://sky.coflnet.com 
You can get the same data and play around with it by using this project.

Some endpoints are exposed via REST, see the open-api docs: https://sky.coflnet.com/api

# Requests
### General
> **Deprecatation WARNING** The command endpoints are getting deprecated please use the REST-endpoints (/api)

The backend uses a concept called `commands`.
All commands are based on the format `type` `data` as `JSON` or `MessagePack`. The content of `data` depends on the `type` and usually is another escaped and `base64` encoded `json` object.
Example:
```
{
    "type":"auctionDetails",
    "data":"2194c8c742b0452d9a3ecd3bd1835013"
}
```
You can find a bare bone example frontend inside the file `test_frontend.html`.
It has no UI and you have to interact with it via the console and the `sendData` comand.
1. Copy the script 
2. Make sure you use the hosted backend if you are not running it locally
3. Open the file in chrome
3. Open the browser console
4. Try `sendData("filterFor","ASPECT_OF_THE_END")` 


## AuctionDetails
```
"type":"auctionDetails",
"data":"2194c8c742b0452d9a3ecd3bd1835013"
```

Respnse
```
"type":"auctionDetailsResponse",
"data":{
    "uuid":"2194c8c742b0452d9a3ecd3bd1835013",
    "count":64,
    "startingBid":10,
    "tier":"common",
    "category":"blocks",
    "itemName":"Cobblestone",
    "start":"12:13:00 26.11.2019",
    "end":"13:00:00 26.11.2019",
    "auctioneer":"8d1c61f0704c4d9ab5f8dfa735247e34",
    "profileId":"8d1c61f0704c4d9ab5f8dfa735247e34",
    "bids":[],
    "anvilUses":0,
    "enchantments":[]
}
```

# ItemDetails
Finds item Details and returns them
```
"type":"itemDetails",
"data":"Cobblestone"
```
Response (unfinished)
```
{"type":"itemDetailsReponse",
"data":"{\"Name\":\"Cobblestone\",
    \"Description\":\"§f§lCOMMON\",
    \"IconUrl\":\"https://skyblock-backend.coflnet.com/static/4-0.png\",
    \"Category\":\"blocks\",
    \"Extra\":\"Cobblestone Cobblestone\",
    \"Tier\":\"COMMON\",
    \"MinecraftType\":\"Cobblestone\",
    \"color\":null}","mId":0}
}

```


## Paginated Player auctions
```
{
    "type":"playerAuctions",
    "data":"{uuid:"c2e675a014774842a169335019da9f40",amount:1,offset:2}"
}
```
Response
```
"type":"playerAuctionsResponse",
"data":[{"uuid":"f177ad57aba64d51aa7bffbed5d5abd7","highestBid":383333,"itemName":"Super Compactor 3000","end":"2019-12-18T00:21:23.3130000Z","bin":false}]

```

## Paginated Player Bids
```
{
    "type":"playerBids",
    "data":"{uuid:"c2e675a014774842a169335019da9f40",amount:2,offset:2}"
}
```
Response
```
"type":"playerBidsResponse",
"data":"[{"highestOwn":10,"highestBid":10,"itemName":"◆ White Spiral Rune I","uuid":"72cf3a968a404c0a8d3090bb65a551ca","end":"2019-12-26T00:25:29.8750000Z","bin":false},
{"highestOwn":661,"highestBid":23131,"itemName":"Tarantula Web","uuid":"050c3a119cdf4c5387c077482d58510c","end":"2019-12-26T00:22:35.2780000Z","bin":false}]"

```

## FullSearch
```
{
    "type":"fullSearch",
    "data":"li"
}
```
Response
```
data: "[{"name":"Lion","id":"PET_LION","type":"item","iconUrl":"URL",
{"name":"lik0","id":"35a475f5e5ee4e9eb95fba146e2d868a","type":"player","iconUrl":"URL"},
{"name":"lior","id":"12f1abcc952d46b0915fc6b0a067a73b","type":"player","iconUrl":"URL"},
{"name":"lij_","id":"1a55161fddc34b2990faacf2db198434","type":"player","iconUrl":"URL",
{"name":"Liam","id":"2c8e8bb81a5845b9b023e060a0296e15","type":"player","iconUrl":"URL"}]"
type: "searchResponse"
```

## TrackSearch
Should be called when a search suggestion (from `fullSearch` is selected).
Used to order future search response suggestions.
The values are:
 * `id` the id from `fullSearch`
 * `type` the type from `fullSarch` 
```
{
    type:"trackSearch",
    data:"{"id":"0938d042abba47cfb74db7ad8839f28d","type":"player"}"
}
```
This command doesn't generate a response

## PlayerName
Returns the Name of a player
```
{
    type:"playerName",
    data:"0938d042abba47cfb74db7ad8839f28d"
}
```
Response:
```
{
    type:"nameResponse",
    data:"mar0x"
}
```



## PricerDicer
Next generation itemPrices. Requires the Tag of the Item in the name field.
Returns faster if no filter options are passed.  
```
{
    "type":"pricerdicer",
    "data":"{"name":"ASPECT_OF_THE_END","filter":{"Enchantment":"sharpness","EnchantLvl":"6"}}"
}
```
Options for filters are retrievable with `getFilter`.

Response:
```
{
  "filterable":true,
  "bazaar":false,
  filters: ["EndBefore", "EndAfter", "Reforge", "Enchantment", "EnchantLvl"]
 "prices":[
    ...
    {"min":1000000,"max":1079960,"avg":1046950,"volume":15141,"time":"2020-12-31T00:00:00.0000000Z"},
    {"min":980002,"max":1066680,"avg":1023300,"volume":14537,"time":"2021-01-01T00:00:00.0000000Z"}
]}
```

## SetConId
Sets a unique connectionId that should be the same if you reconnect. This is used to preserve subscriptions over reopens. It should be saved and reused for at least the session length.
```
{
    "type":"setConId",
    "data":"randomUuid"
}
```

## PremiumExpiration
Gets the timestamp until a given user has their Premium subscription expired.
```
{
    "type":"premiumExpiration",
    "data":"userId"
}
```
Response:
```
{
    "type":"premiumExpiration",
    "data":"2021-01-04T00:00:00.0000000Z"
}
```
`data` can also be `null` if the user never had premium before.

## PaymentSession
Creates a checkout session for a given product
```
{
    "type":"paymentSession",
    "data":"productId"
}
```
Response:
```
{
    "type":"checkoutSession",
    "data":"stripeSessionId"
}
```

## Version
Returns the app version. All caches should be cleared, when the response of this command changes. 
```
{
    "type":"version"
    "data":""
}
```
Response
```
{
    "type":"version",
    "data":"6"
}
```

## SetGoogle
Sets the google account id for this connection. It expects the `id_token` of the GoogleLogin response as string parameter. (`tokenObj.id_token`)
```
{
    type:"setGoogle",
    data:"eyJh..."
}
```
No response

## Subscribe
Subscribes to some event to receive push notifications.
```
{
    type:"subscribe",
    data:{"topic":"BOOSTER_COOKIE","price":0,type:1}
}
```
`type` is the following `ENUM`
```
enum SubType {
    NONE = 0,
    PRICE_LOWER_THAN = 1,
    PRICE_HIGHER_THAN = 2,
    OUTBID = 4,
    SOLD = 8,
    BIN = 16,
    USE_SELL_NOT_BUY = 32
}
```


## Subscriptions
List current subscriptions
```
{
    type:"subscriptions",
    data:""
}
```
Response:
```
    type: "subscriptions",
    data: "[{"topicId":"BOOSTER_COOKIE","price":0,"type":2}]"
```

## Unsubscribe
Unsubscribes from a subscription. Expects the format returned by `subscriptions`. Will unsubscribe from multiple subscriptions if they are the same.
```
    type:"unsubscribe",
    data: {"topic":"BOOSTER_COOKIE","price":0,type:2}
```
Response:
```
{
    type: "unsubscribed", 
    data: "1"
}
```
Returns the amount of remove subscriptions

## Token alias addDevice
Adds a device with firebase token to push notification to
```
{
    "addDevice",
    data:{"name":"unique name or connectionId",token:"--token--"}
}
```
Errors:
```
{
    type: "error", 
    data: "{"Slug":"no_premium","Message":"You need premium to add multiple devices"}"
```
Multiple devices is currently a premium feature.

## GetReforges
Returns an array of known Reforges.
```
{
    type: "getReforges",
    data: ""
}
```
Response:
```
{
    type: "getReforgesResponse",
    data: [{label: "None", id: 0},
          {label: "Demonic", id: 1},
          {label: "Forceful", id: 2}, 
           ... 
          {label: "Any", id: 126}
    ]
}
```

## GetEnchantments
Returns an array of known Enchantments with their ids.
```
{
    type: "getEnchantments",
    data: ""
}
```
Response:
```
{
    type: "getReforgesResponse",
    data: [{"label":"unknown","id":0},
           {"label":"cleave","id":1}, 
           ... 
           {label: "None", id: 98},
           {label: "Any", id: 126}
    ]
}
```

## getDevices
Get a list of all device of a user
```
{
    type: "getDevices"
}
```
Requires the [SetGoogle](#SetGoogle) Command to be called first

Response:
```
{type: "devices", 
data: [{"conId":null,"name":"","token":"dLS9L9-GkIrJ6gea…RQl1vYxKgo_qGb9qpdU"}]
```
## deleteDevice
Deletes a device of an user
```
{
    type: "getDevices",
    data: "deviceName"
}
```
Requires the [SetGoogle](#SetGoogle) Command to be called first.
The deviceName is the `name` property of the array elements of [getDevices](#getDevices).
Also the `name` property passed with Command [Token](#Token)

## TestNotification
Sends a test notification to a device
```
{
    type:"testNotification",
    data: "deviceName"
}
```

## RecentAuctions
Returns the most recent auction of a filterquery. (5 auctions by default)
```
{
    type:"recentAuctions",
    data:{"name":"ASPECT_OF_THE_END","start":1611247400,"reforge":126,"enchantments":[[98,7]]}
}
```
Response:
```
{ 
    type:"auctionResponse",
    data: [{"end":"2021-01-21T21:39:38.0Z","price":199542,"seller":"6...4e820","uuid":"783f0e381...f02"}]
}
```

## GetFilter
Returns a filter's optons
```
{
    type:"getFilter",
    data:"PetLevel"
}
```
Response:
```
data: {
   longType: "Equal, NUMERICAL"
   name: "PetLevel"
   options: ["1", "100"]
   type: 17
}
```
In case the filter is marked as NUMERICAL or DATE, the options specify a range. otherwise there should be a dropdown.
The `longType` is for reference only it will be removed in the future.
```
    public enum FilterType
    {
        Equal = 1,
        HIGHER = 2,
        LOWER = 4,
        DATE = 8,
        NUMERICAL = 16
    }
```

## SubFlip
Subscribes to the flipper. This will push the command `flip` with the same format as `getFlips`.
```
{
    type:"subFlip"
}
```

## UnSubFlip
Unsubscibes from the flipper. should be called when the flipper is no longer visible
```
{
    type:"unsubFlip"
}
```

## getFlips
Returns a list of recent flips
```
{
    type:"getFlips"
}
```
Response:
```
{
    type:"flips"
    data:[{median: 196000, cost: 170000, uuid: "a25bbaf9e0504a31adc0b63eeb8b502e", name: "Potion Affinity Artifact", volume: 59.999943}]
}
```
The volume is the 24 hour volume of a given flip item.
It is capped at 60 and if it is almost 60 should be noted in the ui that it is "more than 60".

## newPlayers
Returns the most recently updated/found players
```
{
    type:"newPlayers"
}
```
Response:
```
{
    type:"newPlayersResponse"
    data:[{name: "ratooon0000",time: "2021-06-11T14:30:17.0000000Z",uuid: "497dda468de744edb1b214be3c269ad5"}]
}
```

## newItems
Returns the most recently found items
```
{
    type:"newItems"
}
```
Response:
```
{
    type:"newItemsResponse"
    data:[{icon: "https://sky.coflnet.com/static/261-0.png", name: "Terminator", tag: "TERMINATOR"}]
}
```


## popularSearches
Returns popular/trending sites
```
{
    type:"popularSearches"
}
```
Response:
```
{
    data:[{title: "display title", url: "/item/STONE"}]
}
```

## endedAuctions
Returns just ended auctions
```
{
    type:"endedAuctions"
}
```
Response:
```
{
    data:[{end: "2021-06-11T19:01:32.0000000Z"
        playerName: "SirGli1tchWhales"
        price: 0
        seller: "f37c9f4dd1504e02a6274a1442717d8c"
        uuid: "ca037db34aeb46248f8c482f95bdc616"}]
}
```

## newAuctions
Returns new auctions (recently created)
```
{
    type:"newAuctions"
}
```
Response:
```
{
    data:[{end: "2021-06-11T19:01:32.0000000Z"
        playerName: "Trayyyo"
        price: 0
        seller: "6b6c790a1cd84c00b94693cd9f3fda42"
        uuid: "ca037db34aeb46248f8c482f95bdc616"}]
}
```
## getRefInfo
Returns information about referred users 
```
{
    type:"getRefInfo"
}
```
Response:
```
{
    refId:"uniqueId",
    count:0,
    receivedTime:"timespan",
    bougthPremium:0
}
```
`bougthPremium` is the count of users that bought premium. If a referred user buys premium, the user who created the link receives 3 extra days.

## setRef
Sets the users who referred this user.  
Caution: this can only be executed once for every user.
```
{
    type:"setRef",
    data:"uniqueId"
}
```
The `uniqueId` has to match `refId` from `getRefInfo`


## activeAuctions
Filterable command that returns active auctions.
It has the extra option of `order` wich determines in what order auctions are returned.
Valid options for `order` are:
* `0` order by relevance, currently same as highest price
* `1` HIGHEST_PRICE
* `2` LOWEST_PRICE
* `4` ENDING_SOON

```
{
    type:"activeAuctions",
    data:{"name":"ASPECT_OF_THE_END","order":2,"filter":{}}
}
```
Response:
```
[{
end: "2021-07-08T19:07:27.0000000Z"
playerName: "Bactar1on"
price: 300000
seller: "69ca793c1feb42ca9bfcc8c1095958ac"
uuid: "aff519437ff842fb9c68acc86d083389"
},...]
```

## conMc
Connect an user to a minecraft account to allow for advanced features.
```
{
    type:"conMc",
    data:"playerUuid"
}
```
Response:
```
{
    "bid":123
}
```
The response is a validation challenge. 
The player has to create an auction or bid on another with the last 3 digits set to that value within 10 min to validate the account connection.   
Valid Options 
* `123` bid on an auction
* `8439123` bid on an auction
* creating auction for `123`
* creating an auction for `53123`

## filterFor
Returns the filters with options for a given item.
```
{
    type:"filterFor",
    data:"ASPECT_OF_THE_END"
}
```
Response:
```
[{
longType: "Equal"
name: "Reforge"
options: ["ambered", "ancient", "Auspicious", "awkward", "Bizarre", …]
type: 1
}[
```
The response is the same as with `getFilter` just as an array because all options are returned at once.
It is planned to filter the available options at some point (currently that is not the case)

## accountInfo
Get Account information about the currently logged in user (no data required)
```
{
    type:"accountInfo"
}
```
Response:
```
{
    "email": "email@example.com",
    "token": "access token for external use"
    "mcId": "mcUUid if connected",
    "mcName": "the name of the mcId"
}
```

## loginExt
Login via another tool that has no google login
```
{
    type:"loginExt",
    data: 
    {
        "email", "account email",
        "token": "token from accountInfo"
    }
}
```
Response:
```
"you were logged in"
```
