# FGA Studio

A web-based management UI for [OpenFGA](https://openfga.dev/) built with .NET 10 and Razor Pages.

## Features

- **Multiple connections** — connect to OpenFGA via HTTP API or directly to a PostgreSQL database
- **Connection management** — add, edit, delete, and switch connections; persisted to `appsettings.json`
- **Store browser** — list stores, select active store and authorization model
- **Tuple management** — list, filter, paginate, write, and delete relationship tuples
- **Light / dark mode** — toggle in the sidebar, preference saved to `localStorage`
- **Responsive UI** — mobile-friendly layout with a collapsible sidebar drawer

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)
- An OpenFGA server (URL mode) or PostgreSQL database (DB mode)

### Run

```bash
cd FgaStudio.Web
dotnet run
```

The app will be available at `https://localhost:5001`.

---

## Connection Modes

### URL / HTTP API

Connects to a running OpenFGA server using the official `OpenFga.Sdk`.

| Setting | Description |
|---|---|
| API URL | Base URL of your OpenFGA server, e.g. `http://localhost:8080` |
| API Token | Bearer token (optional, if auth is enabled) |

### Direct Database

Connects directly to the PostgreSQL database used by OpenFGA via Npgsql.

| Setting | Description |
|---|---|
| Connection String | Npgsql connection string, e.g. `Host=localhost;Database=openfga;Username=postgres;Password=secret` |

> **Note:** Write operations in DB mode use raw SQL matching the OpenFGA schema.
> Use URL mode for production write operations where possible.

---

## Project Structure

```
FgaStudio.Web/
├── Configuration/
│   └── FgaStudioSettings.cs     # Settings POCO bound from appsettings.json
├── Models/
│   └── Models.cs                # ConnectionConfig, TupleKey, ViewModels, etc.
├── Services/
│   ├── IFgaService.cs           # Service interface
│   ├── FgaHttpService.cs        # OpenFGA SDK HTTP implementation
│   ├── FgaDbService.cs          # Direct Npgsql DB implementation
│   └── ConnectionManager.cs     # Resolves active connection, persists config
├── Pages/
│   ├── Index                    # Dashboard
│   ├── Connection/
│   │   ├── Index                # List connections
│   │   └── Configure            # Add / edit connection
│   ├── Stores/
│   │   └── Index                # Browse stores & authorization models
│   └── Tuples/
│       ├── Index                # List & filter tuples
│       └── Write                # Write new tuple
└── wwwroot/css/site.css         # Theme styles (dark + light mode)
```

---

## Configuration

Connections are persisted to `appsettings.json` under the `FgaStudio` key:

```json
{
  "FgaStudio": {
    "activeConnectionName": "Local Dev",
    "connections": [
      {
        "name": "Local Dev",
        "type": "Url",
        "apiUrl": "http://localhost:8080",
        "apiToken": null,
        "storeId": "01J...",
        "authorizationModelId": "01J..."
      }
    ]
  }
}
```
