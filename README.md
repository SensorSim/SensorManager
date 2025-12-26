# SensorManager

**Purpose:** Owns sensor configuration (types, units, operating/warning ranges, interval, enabled/simulate).

This service is the *source of truth* for sensor setup. Other services must not access its database directly.

---

## What it owns

- Sensor configuration records
- Validation rules for configuration fields
- Publishing “config changed” events to Kafka

Database:
- `postgres-sensormanager` (Docker Compose)

Kafka:
- Produces to topic `sensor-config-events`

---

## API

Swagger:
- `http://localhost:8083/swagger`

Common endpoints:
- `GET /sensors` (paging + filters)
- `POST /sensors`
- `PUT /sensors/by-sensorId/{sensorId}`
- `DELETE /sensors/by-sensorId/{sensorId}`
- `GET /health/live`
- `GET /health/ready`

---

## Configuration (env vars)

- `ConnectionStrings__Postgres` – Postgres connection string
- Kafka bootstrap settings (see `appsettings.json` / compose env)

---

## Local run (without Docker)

```bash
dotnet run
```

(For local run you still need Postgres + Kafka running; easiest is Docker Compose.)
