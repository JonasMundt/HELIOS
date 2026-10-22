# HELIOS – Development Guide

## 1. Overview

This document describes how to set up, run, test, and maintain the HELIOS Industrial Digital Twin Platform.

HELIOS consists of a React frontend, an ASP.NET Core backend, and a PostgreSQL database.

Two development workflows are supported:

- **Docker Compose:** Runs the frontend, API, and PostgreSQL in containers.
- **Local Development:** Runs React and ASP.NET Core directly while using PostgreSQL through Docker.


> **Database initialization:** HELIOS automatically applies pending EF Core migrations and seeds the required roles and demonstration machines when `Helios__InitializeDatabase=true` is configured. This process has been verified against a fresh PostgreSQL database.

## 2. Prerequisites

### Docker Environment

- Docker Desktop
- Docker Compose
- Git

### Local Development

- .NET 9 SDK
- Node.js 24
- npm
- Docker Desktop with PostgreSQL 17
- Visual Studio Code or another suitable IDE

## 3. Clone the Repository

```bash
git clone https://github.com/JonasMundt/HELIOS.git
cd HELIOS
```

## 4. Environment Configuration

Create a `.env` file in the project root.

```dotenv
POSTGRES_PASSWORD=your_secure_database_password
JWT_KEY=your_base64_encoded_jwt_key
JWT_ISSUER=HELIOS
JWT_AUDIENCE=HELIOS.Frontend
```

The JWT key must contain at least 32 cryptographically random bytes encoded as Base64.

The Docker Compose API configuration also sets the JWT expiration time to 60 minutes.

Never commit `.env`, JWT signing keys, or database passwords.

The `.env` file is excluded from Git through `.gitignore` and excluded from Docker image builds through `.dockerignore`.

For local ASP.NET Core development, use the .NET configuration system and development user secrets rather than committing sensitive values to configuration files.

## 5. Running HELIOS with Docker

From the repository root:

```bash
docker compose config -q
docker compose up -d --build
```

The first command validates the Docker Compose configuration. The second builds and starts the application.

### Local Service Addresses

| Service | Address |
|---|---|
| React Dashboard | http://localhost:8080 |
| Backend API | http://localhost:5001 |
| PostgreSQL | localhost:5432 |

The frontend uses Nginx to forward API and SignalR traffic to the backend.

### Check Container Status

```bash
docker compose ps
```

Expected services:

- `helios-postgres`
- `helios-api`
- `helios-frontend`

PostgreSQL should report a healthy status.

### View Application Logs

```bash
docker compose logs api --tail 100
docker compose logs frontend --tail 100
docker compose logs postgres --tail 100
```

### Stop the Application

```bash
docker compose down
```

The database volume remains intact.

Do not use `docker compose down -v` unless deleting the persisted database is intentional.

### Database Initialization Limitation

A fresh PostgreSQL instance requires the database schema, required roles, and initial users to be provisioned before the complete application can be used.

The current Production-mode API container does not automatically perform the complete initialization process.

### First-Time Database Setup

The Docker Compose configuration enables automatic database initialization through `Helios__InitializeDatabase=true`.

When the API starts, it applies pending migrations, creates the required roles, and seeds the demonstration machines.

No user accounts or default passwords are created automatically.

Register an initial account using `POST /api/auth/register`. New accounts receive the `Viewer` role.

To promote the first registered user to `Operator`, execute:

```bash
docker compose run --rm --no-deps api --bootstrap-operator your-email@example.com
```

The command requires local Docker access and only succeeds when no Operator or Admin account exists.

The bootstrap procedure was successfully tested against an isolated, freshly initialized database.

## 6. Local Development

### Start PostgreSQL

From the repository root:

```bash
docker compose up -d postgres
```

Ensure the backend has a valid local database connection string and JWT configuration.

### Start the Backend

```bash
dotnet run --project backend/Helios.Api --urls http://localhost:5000
```

The backend provides REST API endpoints and the SignalR hub.

Development-mode initialization includes development database seeding and role setup.

### Start the Frontend

Open a separate terminal:

```bash
cd frontend
npm ci
npm run dev
```

Open:

http://localhost:5173

The Vite development server forwards `/api` and `/hubs` traffic to the local backend on port 5000.

### Important: Background Worker Concurrency

Avoid running the local backend and Docker API simultaneously against the same PostgreSQL database.

Each instance contains a background simulation worker.

Without coordination between workers, multiple instances can advance the same simulation independently, causing production stages to execute faster than expected.

Only one HELIOS backend instance should process simulations in the current development setup.

## 7. Backend Development

The backend is organized into separate projects:

- `Helios.Api` – HTTP endpoints, SignalR, authentication, and hosted services
- `Helios.Application` – Application services and interfaces
- `Helios.Domain` – Domain entities and business rules
- `Helios.Infrastructure` – Persistence, Entity Framework Core, and Identity

The backend solution is located at `HELIOS.sln`.

### Build the Backend

```bash
dotnet restore HELIOS.sln
dotnet build HELIOS.sln --configuration Release
```

### Run Backend Tests

```bash
dotnet test HELIOS.sln --configuration Release
```

The backend test suite includes domain, application, and API integration testing.

### Database Changes

HELIOS uses Entity Framework Core migrations.

Schema changes should be implemented through version-controlled migrations rather than manual database modifications.

Fresh-database provisioning has been verified using PostgreSQL 17 and the Dockerized .NET 9 API. All four EF Core migrations, three Identity roles, and four demonstration machines were created successfully. The first-Operator bootstrap and its duplicate-execution protection were also tested.

## 8. Frontend Development

The frontend uses React, TypeScript, Vite, Three.js, React Three Fiber, and Drei.

### Install Dependencies

```bash
cd frontend
npm ci
```

### Start Development Server

```bash
npm run dev
```

### Run ESLint

```bash
npm run lint
```

### Run Frontend Tests

```bash
npm test
```

### Create Production Build

```bash
npm run build
```

The production files are generated in `frontend/dist`.

### 3D Factory

The interactive factory uses custom Blender models stored under `frontend/public/models`.

The main 3D scene is implemented in `FactoryScene.tsx`.

The scene is loaded through React lazy loading to reduce the initial JavaScript bundle size.

React Suspense handles the loading state, while `FactoryErrorBoundary` handles supported component rendering errors.

## 9. Production Simulation

HELIOS simulates the following process:

1. Conveyor Belt
2. Scanner Station
3. Hydraulic Press
4. Robot Arm
5. Finished

The simulation state is controlled by the backend and persisted in PostgreSQL.

A background service attempts to advance active simulations approximately every three seconds.

The frontend visualizes the current production stage through machine animations and status indicators.

## 10. Authentication and Roles

HELIOS uses ASP.NET Core Identity and JWT bearer authentication.

Supported roles include:

- Viewer
- Operator
- Admin

New users are registered with the Viewer role.

Operational endpoints require the `CanOperate` authorization policy.

The frontend uses role information to display or disable appropriate controls, while the backend independently enforces authorization.

Authentication sessions are maintained in frontend memory.

## 11. Real-Time Updates

HELIOS uses SignalR at:

`/hubs/helios`

Relevant events include:

- `MachinesChanged`
- `SimulationsChanged`
- `SystemMessage`

The frontend retrieves updated data from the REST API when machine or simulation changes are reported.

Automatic SignalR reconnection is enabled.

Following a successful reconnection, the dashboard refreshes system, machine, and simulation data.

## 12. Continuous Integration

GitHub Actions automatically validates changes pushed to `main` and pull requests targeting `main`.

The repository contains:

- `.github/workflows/backend-ci.yml`
- `.github/workflows/frontend-ci.yml`

### Backend CI

- Restore NuGet dependencies
- Build the .NET solution
- Execute automated backend tests

### Frontend CI

- Install npm dependencies
- Run ESLint
- Execute Vitest tests
- Build the production frontend

Both pipelines must pass before changes are considered fully validated.

## 13. Troubleshooting

### Docker Login Returns HTTP 500

Verify that the Docker API receives the required JWT configuration:

- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__ExpiresMinutes`

An invalid or missing expiration setting can prevent JWT token generation.

### Login Returns HTTP 401

Check that the user exists, the credentials are correct, and the account satisfies the configured ASP.NET Core Identity sign-in requirements.

### API Returns HTTP 502

Nginx may be unable to reach the API container.

Check:

```bash
docker compose ps
docker compose logs api --tail 100
```

### Simulation Skips Production Stages

Verify that only one backend instance is processing simulations.

Do not run the local .NET API alongside the Docker API while both connect to the same database.

### SignalR Disconnects

Short disconnections may happen during an API restart.

The frontend attempts automatic reconnection and refreshes dashboard information afterward.

Longer interruptions may require a page reload.

### Frontend Build Reports a Large JavaScript Chunk

The Three.js factory scene is the largest JavaScript component.

Lazy loading already separates it from the initial application bundle.

Further code splitting and asset optimization remain possible.

## 14. Development Workflow

Recommended workflow:

1. Create a feature branch.
2. Implement the required changes.
3. Run the relevant local tests.
4. Run frontend linting and production build checks.
5. Review Git changes for unintended files and secrets.
6. Commit and push the changes.
7. Verify GitHub Actions.
8. Merge validated changes into the main branch.

## 15. Known Development Limitations

The current version is a functional portfolio MVP.

Important remaining improvements include:

- Automated initialization of fresh databases
- Coordinated background processing across backend instances
- Additional integration and frontend tests
- Expanded error handling and production monitoring
- HTTPS and deployment security hardening
- Further optimization of the 3D rendering bundle

For architecture details, see `architecture.md`.

For REST endpoint documentation, see `api.md`.
