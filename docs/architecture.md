# HELIOS – System Architecture

## 1. Overview

HELIOS is a full-stack industrial simulation platform built as a modular monolith. It combines a React-based monitoring dashboard, an ASP.NET Core backend, PostgreSQL persistence, real-time communication, and an interactive 3D factory.

The platform simulates a virtual production environment. It does not communicate with or control physical industrial equipment.

## 2. System Components

| Component | Technology | Responsibility |
|---|---|---|
| Frontend | React, TypeScript, Vite | Dashboard, user interactions, production visualization |
| 3D Engine | Three.js, React Three Fiber, Drei | Interactive factory scene and machine animations |
| Reverse Proxy | Nginx | Serves frontend assets and forwards API and SignalR traffic |
| Backend | ASP.NET Core 9 | HTTP endpoints, authentication, application coordination |
| Application Layer | C# services | Production workflows and machine operations |
| Domain Layer | C# domain entities | Business rules and simulation state transitions |
| Infrastructure Layer | EF Core, ASP.NET Identity | Database access and identity persistence |
| Database | PostgreSQL 17 | Persistent machines, simulation runs, and users |
| Real-Time Communication | SignalR | Notifies connected dashboards of state changes |
| Background Processing | ASP.NET Core BackgroundService | Advances active simulations |
| Deployment | Docker Compose | Coordinates application services |
| Continuous Integration | GitHub Actions | Automated builds, linting, and tests |

## 3. Backend Architecture

The backend separates responsibilities across four projects.

### Helios.Api

The API project provides the application's entry point and presentation layer.

Responsibilities include:

- HTTP API endpoints
- JWT authentication and authorization
- SignalR hub
- Background simulation worker
- Dependency injection and application configuration
- JWT token generation

### Helios.Application

The application layer coordinates use cases and operations.

Important services include `SimulationService` and `MachineService`.

This layer manages production workflows and delegates persistence to repository abstractions.

### Helios.Domain

The domain layer defines the core business entities and rules.

For example, `SimulationRun` manages the state of a production simulation and validates sequential transitions between production stages.

### Helios.Infrastructure

The infrastructure layer implements technical persistence concerns.

It includes:

- Entity Framework Core database context
- PostgreSQL integration
- Repository implementations
- Database migrations
- ASP.NET Core Identity persistence

This separation keeps domain logic independent from HTTP endpoints and database implementation details.

## 4. Authentication and Authorization

HELIOS uses ASP.NET Core Identity for user management and JWT bearer authentication for protected API operations.

Authentication flow:

1. A user submits credentials through the React login screen.
2. The backend retrieves the user through ASP.NET Identity.
3. ASP.NET Identity verifies the password and sign-in requirements.
4. The backend generates a signed JWT containing identity and role claims.
5. The frontend stores the authentication session in memory.
6. Subsequent protected requests include the JWT in the Authorization header.

HELIOS supports Viewer, Operator, and Admin roles.

The frontend adapts available controls based on user permissions. The backend independently enforces authorization, so access control does not depend on disabled frontend buttons.

## 5. Production Simulation

The production process contains five operational stages:

| Stage | Description |
|---|---|
| Conveyor Belt | Transports the workpiece |
| Scanner Station | Simulates scanning the workpiece |
| Hydraulic Press | Simulates a pressing operation |
| Robot Arm | Performs a visual pick-and-place operation |
| Finished | Completes the production cycle |

The backend owns the authoritative simulation state.

### Background Processing

`SimulationWorker` is an ASP.NET Core hosted background service.

It checks for an active simulation on a three-second timer. When a simulation is active, the worker:

1. Retrieves the active simulation.
2. Advances it through `SimulationService`.
3. Updates machine states through `MachineService`.
4. Publishes real-time change notifications.
5. Records the transition in application logs.

The production workflow progresses sequentially, rather than allowing arbitrary stage jumps.

### Current Concurrency Limitation

The simulation worker is currently designed for a single active backend instance.

Multiple backend instances connected to the same database can attempt to advance the same simulation independently. This was observed during development when a locally launched backend and a Docker backend ran simultaneously.

A future multi-instance deployment would require coordinated worker ownership, such as database-backed locking, optimistic concurrency protection, or an external job queue.

## 6. Real-Time Communication

HELIOS uses SignalR for live dashboard updates.

The SignalR hub is exposed at `/hubs/helios`.

When relevant backend state changes, the application publishes events such as:

- `MachinesChanged`
- `SimulationsChanged`

The React frontend listens for these events and requests updated machine or simulation data through the REST API.

This keeps PostgreSQL and the backend as the authoritative data sources rather than relying on SignalR messages as a permanent source of state.

### Reconnection Handling

The frontend uses automatic SignalR reconnection.

After reconnecting, it refreshes the system status, machine data, and simulation data. If synchronization fails, the dashboard can display a warning that the information may be outdated.

## 7. Interactive 3D Visualization

The factory is rendered with React Three Fiber and Three.js.

Its visual components include Blender-created GLB models for the conveyor, scanner, and hydraulic press, alongside a procedurally constructed robotic arm.

The 3D scene reacts to simulation stage information retrieved from the backend.

Animations visualize production operations but are not the authoritative source of simulation progress.

### Frontend Performance

The factory scene is loaded through React lazy loading.

This separates the 3D rendering libraries from the initial application bundle, reducing the JavaScript required to display the initial login interface.

React Suspense displays a loading state while the scene is being loaded. A dedicated error boundary handles supported React rendering and component-loading errors without requiring the entire dashboard to fail.

## 8. Docker Deployment

The local Docker Compose environment runs three services:

| Service | Internal Port | Local Host Port |
|---|---|---|
| frontend (Nginx) | 80 | 8080 |
| api (ASP.NET Core) | 8080 | 5001 |
| postgres | 5432 | 5432 |

Nginx serves the compiled React application.

Requests to `/api/` are forwarded to the API container, while `/hubs/` supports SignalR and WebSocket traffic.

The API connects to PostgreSQL using the Docker service hostname `postgres`.

A named Docker volume persists PostgreSQL data across container restarts.

The current Compose environment is intended for local development and demonstration, not as a production-hardened public deployment.

## 9. Testing and Continuous Integration

HELIOS includes automated backend and frontend tests.

Backend tests cover application behavior through domain, service, and API integration testing. Frontend tests currently cover login behavior and role permission logic.

GitHub Actions provides separate backend and frontend CI workflows.

The backend pipeline restores dependencies, builds the .NET solution, and executes tests.

The frontend pipeline installs Node.js dependencies, runs ESLint and Vitest, and creates a production build.

## 10. Known Limitations and Future Improvements

The current portfolio MVP has several intentional limitations:

- The simulation operates on virtual data, not physical machine telemetry.
- Multiple simulation workers are not yet coordinated.
- Fresh database initialization and initial account provisioning are not fully automated for Docker Production mode.
- The interactive 3D scene remains the largest JavaScript bundle.
- The local HTTP Docker setup needs additional hardening before internet-facing production deployment.

These limitations provide clear extension points for future work.