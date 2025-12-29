# SensorManager

Sensor configuration service.

Owns the sensor config database and exposes CRUD over HTTP. On create/update/delete it publishes a change event to Kafka (`sensor-config-events`).

## Branching

- `dev` – development
- `main` – stable / demo-ready

## Requirements

- .NET SDK (tested with 10.0.101)
- Postgres
- Kafka

## Run

Recommended: run the full stack with Docker Compose (see `infra/docker`).

Local run (you still need Postgres + Kafka running):

```bash
dotnet run
```

## Configuration

Environment variables:

- `ConnectionStrings__Postgres` – Postgres connection string
- `Kafka__BootstrapServers` – Kafka bootstrap servers (e.g. `localhost:9092`)

## API

Swagger (docker default): `http://localhost:8083/swagger`

Main endpoints:

- `GET /sensors` (filters + paging)
- `GET /sensors/{id}`
- `GET /sensors/by-sensorId/{sensorId}`
- `POST /sensors`
- `PUT /sensors/{id}`
- `PUT /sensors/by-sensorId/{sensorId}`
- `DELETE /sensors/{id}`
- `DELETE /sensors/by-sensorId/{sensorId}`

Health:

- `GET /health/live`
- `GET /health/ready`
