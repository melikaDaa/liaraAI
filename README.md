# Liara AI Assistant

AI-powered documentation assistant for Liara Cloud.

The solution follows Clean Architecture:

```
API  -> Application -> Domain
Infrastructure -> Application -> Domain
```

## Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download) (project currently targets `net9.0`)
- [Docker](https://www.docker.com/) + Docker Compose

## Local Infrastructure

Local development uses Docker Compose to provide:

- **PostgreSQL** with the `pgvector` extension (image `pgvector/pgvector:pg16`) — persisted in the `postgres-data` volume.
- **Redis** 7 (append-only persistence enabled) — persisted in the `redis-data` volume.

### 1. Configure environment variables

Copy the example file and set a local password. **Never commit the real `.env`.**

```bash
cp .env.example .env
```

Edit `.env` and change `POSTGRES_PASSWORD` (and update the password inside
`ConnectionStrings__Postgres` to match).

The following variables are consumed:

| Variable                     | Used by         | Purpose                                  |
| ---------------------------- | --------------- | ---------------------------------------- |
| `POSTGRES_USER`              | docker-compose  | PostgreSQL username                      |
| `POSTGRES_PASSWORD`          | docker-compose  | PostgreSQL password (required)           |
| `POSTGRES_DB`                | docker-compose  | PostgreSQL database name                 |
| `POSTGRES_PORT`              | docker-compose  | Host port mapped to Postgres (5432)      |
| `REDIS_PORT`                 | docker-compose  | Host port mapped to Redis (6379)         |
| `ConnectionStrings__Postgres`| ASP.NET Core    | EF Core / Npgsql connection string       |
| `ConnectionStrings__Redis`   | ASP.NET Core    | Redis connection string                  |
| `AvalAI__BaseUrl`            | ASP.NET Core    | AvalAI OpenAI-compatible base URL        |
| `AvalAI__ApiKey`             | ASP.NET Core    | AvalAI API key (**secret** — never commit)|
| `AvalAI__EmbeddingModel`     | ASP.NET Core    | Embedding model id (1536-dim)            |

> `appsettings.json` ships with local-development defaults for the connection
> strings. Environment variables (from `.env` or the shell) override them and
> are the correct place for anything sensitive.

### 2. Start the infrastructure

```bash
docker compose up -d
```

Both services expose Docker health checks; verify they are healthy with:

```bash
docker compose ps
```

### 3. Run the API

```bash
dotnet run --project src/LiaraAI.Api
```

### 4. Verify health

```bash
curl http://localhost:5000/health
```

The `/health` endpoint aggregates three checks: `api` (liveness), `postgres`,
and `redis`. A healthy response looks like:

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "api", "status": "Healthy" },
    { "name": "postgres", "status": "Healthy" },
    { "name": "redis", "status": "Healthy" }
  ]
}
```

### Stopping

```bash
docker compose down          # stop containers, keep volumes
docker compose down -v       # stop containers and remove data volumes
```

## Build & Test

```bash
dotnet build
dotnet test
```

## Database

The schema is managed with EF Core migrations. The `AppDbContext` lives in
`LiaraAI.Infrastructure` and maps two tables:

- `documents` — one row per ingested documentation page.
- `document_chunks` — many rows per document; the retrieval unit. The
  `Embedding` column is a nullable pgvector `vector(1536)` (sized for
  `text-embedding-3-small`) and is populated by the embedding backfill (see below).

Apply migrations against a running database (see infra above):

```bash
# connection string comes from ConnectionStrings__Postgres (env or appsettings)
dotnet ef database update --project src/LiaraAI.Infrastructure --startup-project src/LiaraAI.Api
```

Create a new migration after model changes:

```bash
dotnet ef migrations add <Name> --project src/LiaraAI.Infrastructure --output-dir Persistence/Migrations
```

## Embeddings (AvalAI)

Chunk embeddings are generated with [AvalAI](https://docs.avalai.ir/fa/)'s
OpenAI-compatible embeddings API.

- **Endpoint:** `POST {AvalAI:BaseUrl}/embeddings` (default `https://api.avalai.ir/v1/embeddings`)
- **Model:** `text-embedding-3-small` — returns **1536 dimensions**, matching the
  `vector(1536)` column exactly.
- **Auth:** `Authorization: Bearer <AvalAI:ApiKey>`.

> **Never commit a real API key.** Set `AvalAI__ApiKey` only in your local `.env`
> (git-ignored) or secure configuration. Keys are never logged.

### Configuration

`appsettings.json` ships with non-secret defaults:

```json
"AvalAI": {
  "BaseUrl": "https://api.avalai.ir/v1",
  "ApiKey": "",
  "EmbeddingModel": "text-embedding-3-small"
},
"Embeddings": {
  "BatchSize": 64,
  "MaxRetries": 3,
  "RetryBaseDelayMs": 1000,
  "Dimensions": 1536
}
```

Provide the key via environment variable:

```bash
export AvalAI__ApiKey="your-avalai-api-key"
```

### Run embedding generation locally (Development only)

Ensure infrastructure is up, migrations applied, and the API is running in the
Development environment, then trigger the backfill:

```bash
curl -X POST http://localhost:5199/admin/embed
```

The endpoint is **Development-only** (not mapped in Production) to prevent
accidental unauthenticated execution. It is idempotent: only chunks with a
`NULL` embedding are processed, and existing embeddings are never overwritten.
It returns a summary: chunks embedded, failures, batches, and total duration.

### Verify embedding counts

```bash
docker exec -it liaraai-postgres psql -U liaraai -d liaraai -c "
SELECT
    COUNT(*) AS total_chunks,
    COUNT(*) FILTER (WHERE \"Embedding\" IS NOT NULL) AS embedded_chunks,
    COUNT(*) FILTER (WHERE \"Embedding\" IS NULL) AS pending_embeddings
FROM document_chunks;
"
```

### Verify embedding dimensions

```bash
docker exec -it liaraai-postgres psql -U liaraai -d liaraai -c "
SELECT
    array_length(string_to_array(\"Embedding\"::text, ','), 1) AS dimension
FROM document_chunks
WHERE \"Embedding\" IS NOT NULL
LIMIT 1;
"
```


