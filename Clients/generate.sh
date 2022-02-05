docker run --rm -v "${PWD}:/local" openapitools/openapi-generator-cli generate \                                                                                                                           ±[●][separation]
    -i /local/McConnect.json \
    -g csharp \
    -o /local/McConnect --additional-properties=packageName=Coflnet.Sky.McConnect.Client
