# HELIOS – REST API Documentation

## 1. Overview

HELIOS provides an ASP.NET Core REST API for authentication, machine management, production simulations, and system monitoring.

The API uses JSON for data exchange, JWT bearer authentication for protected endpoints, and ASP.NET Core SignalR for real-time notifications.

**API version:** 0.1.0

**Local Docker base URL:** `http://localhost:8080`

**Direct API URL:** `http://localhost:5001`

The Docker frontend uses Nginx to proxy `/api/` and `/hubs/` requests to the backend.

## 2. Authentication

HELIOS uses ASP.NET Core Identity and JSON Web Tokens (JWT).

Protected API requests must include an Authorization header:

```http
Authorization: Bearer <access_token>
```

### Authentication Endpoints

| Method | Endpoint | Authentication |
|---|---|---|
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/login` | Public |
| GET | `/api/auth/me` | Required |

### POST /api/auth/register

Registers a new account.

**Request body:**

```json
{
  "email": "example@example.com",
  "password": "ExamplePassword123!"
}
```

Passwords must comply with the configured ASP.NET Core Identity policy:

- Minimum length: 12 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one non-alphanumeric character

Newly registered users automatically receive the `Viewer` role.

**Success:** `201 Created`

Example response:

```json
{
  "accessToken": "<JWT_TOKEN>",
  "expiresAtUtc": "2026-10-22T18:00:00Z",
  "email": "example@example.com",
  "roles": ["Viewer"]
}
```

**Possible errors:**

| Status | Description |
|---|---|
| 400 | Missing credentials, duplicate email, or invalid password |
| 500 | User role assignment failed or another internal error |

If assigning the Viewer role fails, the backend attempts to delete the newly created account to avoid leaving an account without a role.

**Note:** Role records must already exist in the database. Initial role provisioning is not yet fully automated for fresh Production Docker installations.

### POST /api/auth/login

Authenticates an existing user.

**Request body:**

```json
{
  "email": "example@example.com",
  "password": "ExamplePassword123!"
}
```

**Success:** `200 OK`

Returns an authentication response containing:

- `accessToken`
- `expiresAtUtc`
- `email`
- `roles`

**Possible errors:**

| Status | Description |
|---|---|
| 400 | Email or password is missing |
| 401 | Invalid credentials or sign-in rejected |
| 500 | Unexpected server or token-generation error |

The login process uses `SignInManager.CheckPasswordSignInAsync` and enables account lockout counting for failed attempts.

### GET /api/auth/me

Returns the current authenticated user's identity claims.

**Authentication:** Required

**Success:** `200 OK`

Example response:

```json
{
  "userId": "f3e34b00-0000-4000-8000-000000000001",
  "email": "example@example.com",
  "roles": ["Viewer"]
}
```

The endpoint reads identity information from the authenticated claims principal.

**Possible errors:**

- `401 Unauthorized` – Missing or invalid authentication token

## 3. Authorization

HELIOS uses role-based access control.

The available roles are:

| Role | Purpose |
|---|---|
| Viewer | Read-only access to protected system data |
| Operator | Operational access to supported machine and simulation controls |
| Admin | Administrative role with operational permissions |

Read endpoints require authentication.

Write operations use the `CanOperate` authorization policy configured in `Program.cs`.

Authorization is enforced by the backend independently of frontend interface restrictions.

Typical authorization responses:

- `401 Unauthorized` – Authentication is required or invalid
- `403 Forbidden` – Authenticated user lacks the required permission

## 4. System API

### GET /api/system/status

Returns basic information about the HELIOS application.

**Authentication:** Required

**Success:** `200 OK`

Example response:

```json
{
  "name": "HELIOS",
  "version": "0.1.0",
  "status": "ONLINE",
  "description": "Industrial Digital Twin Platform"
}
```

The endpoint reports the application's declared system status. It is not a comprehensive infrastructure health check.

## 5. Machine API

### Machine Endpoints

| Method | Endpoint | Authorization |
|---|---|---|
| GET | `/api/machines` | Authenticated |
| GET | `/api/machines/{id}` | Authenticated |
| POST | `/api/machines/{id}/start` | CanOperate |
| POST | `/api/machines/{id}/stop` | CanOperate |

Machine identifiers use the GUID format.

### GET /api/machines

Returns all machines registered in HELIOS.

**Success:** `200 OK`

Machine data contains fields used by the frontend, including:

- `id`
- `name`
- `type`
- `status`
- `createdAt`

Machine types and statuses are represented numerically in the API model and converted to readable descriptions by the frontend.

### GET /api/machines/{id}

Returns a machine by its GUID.

**Success:** `200 OK`

**Not found:** `404 Not Found`

Example error response:

```json
{
  "message": "Machine not found."
}
```

### POST /api/machines/{id}/start

Manually starts a machine.

**Authorization:** CanOperate

This operation does not require a request body.

**Success:** `200 OK`

Example response:

```json
{
  "message": "Machine started successfully.",
  "machineId": "f3e34b00-0000-4000-8000-000000000001"
}
```

**Possible errors:**

| Status | Description |
|---|---|
| 401 | Authentication required |
| 403 | Insufficient permissions |
| 404 | Machine not found |
| 409 | A production simulation is currently active |

Manual machine controls are unavailable while a simulation is active.

On success, HELIOS publishes a `MachinesChanged` SignalR notification.

### POST /api/machines/{id}/stop

Manually stops a machine.

**Authorization:** CanOperate

This operation does not require a request body.

**Success:** `200 OK`

Example response:

```json
{
  "message": "Machine stopped successfully.",
  "machineId": "f3e34b00-0000-4000-8000-000000000001"
}
```

Possible errors include `401`, `403`, `404`, and `409`.

As with starting a machine, manual control is rejected while a production simulation is active.

On success, HELIOS publishes a `MachinesChanged` SignalR notification.

## 6. Simulation API

### Simulation Endpoints

| Method | Endpoint | Authorization |
|---|---|---|
| GET | `/api/simulations` | Authenticated |
| GET | `/api/simulations/{id}` | Authenticated |
| POST | `/api/simulations` | CanOperate |
| POST | `/api/simulations/{id}/stop` | CanOperate |
| POST | `/api/simulations/{id}/advance` | CanOperate |

### GET /api/simulations

Returns all simulation runs.

**Success:** `200 OK`

The frontend uses simulation properties including:

- `id`
- `startTime`
- `endTime`
- `status`
- `producedItems`
- `currentStage`

### GET /api/simulations/{id}

Returns a specific simulation run.

**Success:** `200 OK`

**Not found:** `404 Not Found`

### POST /api/simulations

Starts a new production simulation.

**Authorization:** CanOperate

No request body is required.

**Success:** `201 Created`

The response contains the newly created simulation and a `Location` header referencing:

```http
/api/simulations/{id}
```

**Possible errors:**

| Status | Description |
|---|---|
| 401 | Authentication required |
| 403 | Insufficient permissions |
| 409 | Simulation cannot start in the current state |

On success, HELIOS publishes a `SimulationsChanged` SignalR notification.

### POST /api/simulations/{id}/stop

Cancels a simulation run.

**Authorization:** CanOperate

**Success:** `200 OK`

Example response:

```json
{
  "message": "Simulation cancelled."
}
```

**Possible errors:**

| Status | Description |
|---|---|
| 401 | Authentication required |
| 403 | Insufficient permissions |
| 404 | Simulation not found |
| 409 | Simulation cannot be cancelled in its current state |

After successful cancellation, the backend resets machine states to the waiting stage.

Both `SimulationsChanged` and `MachinesChanged` notifications are published.

### POST /api/simulations/{id}/advance

Manually advances a simulation to its next production stage.

**Authorization:** CanOperate

**Success:** `200 OK`

Returns the updated simulation.

**Possible errors:**

| Status | Description |
|---|---|
| 401 | Authentication required |
| 403 | Insufficient permissions |
| 404 | Simulation not found |
| 409 | Invalid simulation state transition |

The domain model enforces sequential stage transitions.

This endpoint publishes a `SimulationsChanged` notification.

**Implementation note:** Unlike the background worker, this endpoint does not explicitly call the machine-state update service. Machine-state consistency during manual advancement is therefore a potential improvement area.

## 7. Production Stages

The HELIOS production workflow follows this sequence:

```text
Waiting
   |
   v
Conveyor Belt
   |
   v
Scanning
   |
   v
Hydraulic Press
   |
   v
Robot Arm
   |
   v
Finished
```

The `SimulationWorker` attempts to advance an active simulation approximately every three seconds.

Simulation state is persisted in PostgreSQL.

The worker also updates machine states and notifies connected dashboards of changes.

The frontend renders corresponding animations for each production stage.

## 8. Real-Time API – SignalR

**Hub:** `/hubs/helios`

**Authentication:** Required

HELIOS uses SignalR to inform connected clients of machine and simulation state changes.

### Server-to-Client Events

| Event | Description |
|---|---|
| `MachinesChanged` | Machine state has changed |
| `SimulationsChanged` | Simulation state has changed |
| `SystemMessage` | General system message |

The change events indicate that relevant state has changed. The frontend obtains the latest information through the corresponding REST endpoints.

### Connection Behavior

The frontend uses automatic SignalR reconnection.

JWT authentication is provided through SignalR's access-token mechanism.

After reconnecting, the dashboard attempts to reload:

- System status
- Machine data
- Simulation data

A failed synchronization can produce a dashboard warning about potentially outdated data.

## 9. Common HTTP Status Codes

| Code | Meaning |
|---|---|
| 200 | Request completed successfully |
| 201 | Resource successfully created |
| 400 | Invalid request |
| 401 | Authentication required or failed |
| 403 | Insufficient permissions |
| 404 | Requested resource not found |
| 409 | Operation conflicts with the current state |
| 500 | Unexpected internal server error |
| 502 | Reverse proxy could not reach the backend |

`502 Bad Gateway` may occur during a backend outage when accessing the API through Nginx.

## 10. API Usage Examples

### Obtain an Access Token

```http
POST /api/auth/login HTTP/1.1
Host: localhost:8080
Content-Type: application/json

{
  "email": "example@example.com",
  "password": "ExamplePassword123!"
}
```

The response contains a JWT access token.

### Retrieve Machines

```http
GET /api/machines HTTP/1.1
Host: localhost:8080
Authorization: Bearer <access_token>
```

### Start a Simulation

```http
POST /api/simulations HTTP/1.1
Host: localhost:8080
Authorization: Bearer <access_token>
```

The authenticated user must satisfy the `CanOperate` policy.

### Retrieve Simulations

```http
GET /api/simulations HTTP/1.1
Host: localhost:8080
Authorization: Bearer <access_token>
```

## 11. Security Considerations

HELIOS includes:

- ASP.NET Core Identity password hashing
- Configurable password requirements
- Account lockout handling
- JWT authentication
- Role-based authorization
- Protected SignalR connections
- Separation of application configuration from source code

The React frontend stores its current authentication session in memory.

Credentials and JWT signing keys must not be committed to Git.

The current HTTP-based Docker configuration is intended for local demonstration. HTTPS, secret management, deployment hardening, and additional operational controls would be required for an internet-facing production environment.

## 12. Known Limitations

- Multi-instance simulation worker coordination is not yet implemented.
- Initial roles and user provisioning are not fully automated for a clean Docker Production installation.
- Manual simulation advancement does not explicitly synchronize machine states through the same path as the background worker.
- The API currently focuses on core demonstration functionality rather than industrial telemetry ingestion.
- Rate limiting and production monitoring are potential future improvements.

---

**HELIOS – Industrial Digital Twin Platform**

REST API documentation for the functional portfolio MVP.