# FGA Studio

A web-based management UI for [OpenFGA](https://openfga.dev/) — browse stores, inspect authorization models, and manage relationship tuples through a clean interface. Built with .NET 10 and Razor Pages.

---

## Features

### Connections
- **Multiple named connections** — save and switch between any number of OpenFGA connections
- **Two connection modes** — connect via the OpenFGA HTTP API or directly to the underlying PostgreSQL database
- **Three authentication methods** — None, API Token (Bearer), or OAuth2 Client Credentials (for hosted instances like Okta FGA)
- **Connection testing** — verify a connection before saving it
- **Masked credentials** — passwords and connection strings are masked in the UI

### Dashboard
- At-a-glance view of the active connection, store, and authorization model
- Store name, ID, creation date, and last-updated timestamp
- Authorization model ID, schema version, type/condition counts, and creation date
- Cached tuple count with last-refresh timestamp
- Quick-action shortcuts to the most-used pages

### Stores & Authorization Models
- **Store browser** — list all stores with creation and update timestamps; set the active store in one click
- **Authorization model list** — view all models for the active store with schema version and type counts
- **Model detail view**
  - Interactive JSON schema viewer with expand/collapse per node
  - Expand all / Collapse all controls
  - Copy schema to clipboard
  - Download schema as a `.json` file
  - Type definitions tree — collapsible list of every type and its relations

### Tuples
- **Tuple list** — paginated table of all relationship tuples in the active store
- **Filtering** — filter by user, relation, and/or object simultaneously
- **Sorting** — click any column header (User, Relation, Object, Timestamp) to sort ascending or descending
- **Configurable page size** — 10, 25, 50, or 100 rows per page
- **Write tuple** — add a new relationship tuple via a simple form
- **Delete tuple** — remove a tuple directly from the list with a confirmation prompt
- **Tuple cache** — tuples from HTTP connections are cached locally for fast sorting/filtering; refresh on demand

### Tuple Tree
- Visual tree of relationships grouped by **object** or **user**
- Filter by a specific object or user to see all their relationships
- Switch perspectives: click any member to pivot to their view
- Entities are grouped by type prefix in a sidebar browser
- Expand all / Collapse all controls
- Truncation warning when more than 500 tuples match

### UI
- **Light and dark mode** — toggle in the sidebar; preference saved to `localStorage`
- **Responsive layout** — works on mobile with a collapsible sidebar drawer
- Monospace rendering for IDs, tuples, and connection strings

---

## Installation

### Option 1 — Docker Compose (recommended)

No .NET installation required.

```bash
git clone https://github.com/yourname/fgastudio.git
cd fgastudio
docker compose up
```

Open [http://localhost:8080](http://localhost:8080).

Data is persisted in a named Docker volume (`fgastudio-data`) and survives container restarts.

### Option 2 — Docker

```bash
docker build -t fgastudio .
docker run -p 8080:8080 -v fgastudio-data:/data fgastudio
```

### Option 3 — .NET CLI

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10).

```bash
cd FgaStudio.Web
dotnet run
```

The app will be available at `https://localhost:5001` / `http://localhost:5000`.

---

## Cloud Deployment

FGA Studio is packaged as a Docker image and can be deployed to any container host. Each user deploys their own private instance — all connection credentials and data stay in that instance's local SQLite database.

### Railway

1. Fork or push this repo to GitHub
2. Create a new project in [Railway](https://railway.app) and select **Deploy from GitHub repo**
3. Railway will detect the `Dockerfile` automatically
4. Add a volume mount at `/data` to persist the database across deploys

### Render

1. Create a new **Web Service** and connect your GitHub repo
2. Set **Environment** to `Docker`
3. Add a **Persistent Disk** mounted at `/data`

### Fly.io

```bash
fly launch          # generates fly.toml
fly volumes create fgastudio_data --size 1
fly deploy
```

Add to `fly.toml`:
```toml
[mounts]
  source = "fgastudio_data"
  destination = "/data"
```

---

## Configuration

### Environment variables

| Variable | Default | Description |
|---|---|---|
| `FgaStudio__DbPath` | `<app root>/fgastudio.db` | Path to the SQLite database file |
| `ASPNETCORE_URLS` | `http://+:8080` | Listening address (set automatically in Docker) |

### Docker Compose override

To change the host port or add environment variables, create a `docker-compose.override.yml`:

```yaml
services:
  fgastudio:
    ports:
      - "3000:8080"
    environment:
      - FgaStudio__DbPath=/data/fgastudio.db
```

---

## Connection Modes

### URL / HTTP API

Connects to a running OpenFGA server using the official `OpenFga.Sdk`.

| Field | Description |
|---|---|
| API URL | Base URL of your OpenFGA server, e.g. `http://localhost:8080` |
| Authentication | None, API Token, or Client Credentials |
| API Token | Bearer token (for `ApiToken` auth) |
| Client ID / Secret | OAuth2 client credentials (for hosted FGA, e.g. Okta FGA) |
| API Audience | Token audience URL, e.g. `https://api.us1.fga.dev/` |
| Token Issuer | OAuth2 token issuer hostname, e.g. `auth.fga.dev` |

Supports full read and write operations. Authorization model schemas and type definitions are fully available.

### Direct Database

Connects directly to the PostgreSQL database used by OpenFGA via Npgsql — useful when you have database access but no HTTP API endpoint.

| Field | Description |
|---|---|
| Connection String | Npgsql connection string, e.g. `Host=localhost;Database=openfga;Username=postgres;Password=secret` |

> **Note:** Full schema JSON and type definitions are not available in DB mode because OpenFGA stores the authorization model as a protobuf blob that cannot be decoded without the SDK. All tuple operations are fully supported.

---

## Project Structure

```
FgaStudio.Web/
├── Models/
│   └── Models.cs                 # ConnectionConfig, TupleKey, ViewModels, enums
├── Services/
│   ├── IFgaService.cs            # Service interface
│   ├── FgaHttpService.cs         # OpenFGA SDK HTTP implementation
│   ├── FgaDbService.cs           # Direct Npgsql PostgreSQL implementation
│   ├── ConnectionManager.cs      # Persists connections to SQLite, builds services
│   └── TupleCacheService.cs      # Local tuple cache (SQLite) for HTTP connections
├── Pages/
│   ├── Index                     # Dashboard
│   ├── Connection/
│   │   ├── Index                 # List and manage connections
│   │   └── Configure             # Add / edit a connection
│   ├── Stores/
│   │   └── Index                 # Browse stores and authorization models
│   ├── AuthModels/
│   │   └── Details               # Authorization model schema and type definitions
│   ├── Tuples/
│   │   ├── Index                 # List, filter, sort, paginate, delete tuples
│   │   └── Write                 # Write a new tuple
│   └── TupleTree/
│       └── Index                 # Visual relationship tree
└── wwwroot/css/site.css          # Theme styles (light + dark mode)
```

---

## Prerequisites

To run via Docker: [Docker Desktop](https://www.docker.com/products/docker-desktop) (no .NET needed).

To run locally: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10).

An OpenFGA server or PostgreSQL database to connect to. You can run a local OpenFGA instance with:

```bash
docker run -p 8080:8080 openfga/openfga run
```
