# CarConsumer

CarConsumer je worker, ki shranjuje podatke iz azure event hubs vrste doloèene z okoljsko spremenljivko v Timescale podatkovno bazo.

## Postavitev

Najlažje se postavi v docker container, ki ga doloèa Dockerfile. Pri tem rabimo nujno nastavit te okoljske spremenljivke:

- REGION : ime vrste iz katere bo bral
- TIMESCALE_CONN_STRING : povezava do podatkovne baze (*tehnièno ne rabi biti timescale, a je bila aplikacija narejena z njo v mislih)
- EVENT_HUBS_CONNECTION_STRING : connection string do Azure event hubs
- BLOB_CONNECTION_STRING : connection string do Azure blob storage, ki se uporablja za checkpointing
- BLOB_NAME : ime Azure blob storage containerja

**Druge**
- Logging_LogLevel_Default : Lahko spremenimo log level, kot vse ASP Net core aplikacije