# Mainty — Project Handoff / Technical Context for Another AI

## 0. Purpose of this document

This document is a handoff for the **Mainty** project. It is intended to be given to another AI together with the current source code from GitHub.

The purpose is not to replace the repository. The **GitHub repository is the source of truth for the current code**. This document explains the architecture, decisions, concepts, development history, known problems, and the point at which development stopped.

The next AI should read this document first and then inspect the repository before changing code.

---

# 1. Project identity

**Project name:** Mainty

Mainty is a maintenance engineering / industrial maintenance management application.

The application is being developed as a modern web application with:

- .NET backend / Web API
- Entity Framework Core
- SQL database
- React frontend
- JWT authentication
- Role-based access
- Maintenance work orders / breakdowns
- Dashboard
- Shift Tasks
- PPM (Planned Preventive Maintenance)

The project has been developed incrementally in multiple long conversations. The current state is not a clean greenfield project: some parts originated from an older authentication mechanism and have been migrated toward JWT.

---

# 2. Important instruction for the next AI

Do NOT assume that every old piece of code is still correct.

The project has recently moved from an older custom authentication approach based around:

    X-UserId
    HttpContext.Items["CurrentUser"]

to JWT authentication.

Therefore, whenever authentication/security code is encountered, check whether it still belongs to the old authentication mechanism.

A particularly important example is `RequireRoles<T>()` in the WorkOrders controller. At the point where development stopped, that method was still using the old `HttpContext.Items["CurrentUser"]` mechanism even though the frontend was already using JWT.

This is a known migration point and should be handled carefully.

---

# 3. High-level architecture

Conceptually the application is:

    React Frontend
          |
          | HTTP + Authorization: Bearer <JWT>
          v
    ASP.NET Core Web API
          |
          v
    Entity Framework Core
          |
          v
    SQL Database

Authentication flow:

    Login page
       |
       v
    POST /api/auth/login
       |
       v
    Backend validates credentials
       |
       v
    Backend creates JWT
       |
       v
    React stores JWT in localStorage
       |
       v
    Axios request interceptor attaches JWT
       |
       v
    ASP.NET Core JWT authentication
       |
       v
    Controllers / [Authorize] / role checks
       |
       v
    Database

---

# 4. Frontend technology and structure

The frontend is React.

The project uses React Router and Axios.

Important frontend concepts:

- `AuthContext`
- `RequireAuth`
- `RequireRole`
- `MainLayout`
- `Dashboard`
- `WorkOrders`
- API endpoints through `apiClient`

Known files from the development history include:

    src/
      auth/
        AuthContext.jsx
        RequireAuth.jsx
        RequireRole.jsx

      api/
        apiClient.js
        endpoints.js

      layouts/
        MainLayout.jsx

      pages/
        Dashboard.jsx
        WorkOrders.jsx

There may be additional files in the GitHub repository. The repository should always be inspected for the exact current structure.

---

# 5. JWT authentication on the frontend

The frontend uses Axios.

The known `apiClient` implementation is approximately:

```js
import axios from 'axios';
import { API_BASE_URL } from '../config/env';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
});
```

## Request interceptor

The frontend reads:

    localStorage.getItem('accessToken')

and attaches:

    Authorization: Bearer <token>

to requests, except the login endpoint.

Known logic:

```js
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('accessToken');

    const url = (config?.url || '').toLowerCase();
    const isAuthLogin = url.includes('/api/auth/login');

    if (token && !isAuthLogin) {
      config.headers = config.headers || {};
      config.headers['Authorization'] = `Bearer ${token}`;
    }

    return config;
  },
  (error) => Promise.reject(error)
);
```

## Response interceptor

The current frontend treats HTTP 401 as an invalid/expired authentication token.

Known behavior:

```js
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error?.response?.status === 401) {
      localStorage.removeItem('accessToken');
      window.location.href = '/login';
    }

    return Promise.reject(error);
  }
);
```

This is important.

### Consequence

Any backend endpoint that incorrectly returns 401 will cause the frontend to:

1. delete the JWT
2. navigate to `/login`

Therefore, when debugging an unexpected login redirect, inspect the actual API response first.

A 401 can mean:

- token missing
- token invalid
- token expired
- JWT configuration mismatch
- backend authentication failure
- an old custom authorization helper incorrectly returning 401

A 403 normally means:

- user is authenticated
- but the user's role/permissions are not allowed

The distinction matters.

---

# 6. AuthContext

The frontend has an `AuthContext`.

Known responsibilities:

- hold `me`
- hold authentication loading state
- restore authentication after page reload
- perform login
- store JWT
- retrieve current user
- logout

Known structure:

```js
const [me, setMe] = useState(null);
const [loading, setLoading] = useState(true);
```

## Application startup

On startup:

```js
const token = localStorage.getItem('accessToken');

if (token) {
  refreshMe();
} else {
  setLoading(false);
}
```

Therefore the application does not merely trust the existence of the token.

It calls `getMe()` to establish the current user.

## refreshMe()

Known logic:

```js
const refreshMe = useCallback(async () => {
  setLoading(true);

  try {
    const res = await getMe();
    setMe(res.data);
  } catch {
    setMe(null);
    localStorage.removeItem('accessToken');
  } finally {
    setLoading(false);
  }
}, []);
```

This means an unsuccessful `getMe()` causes the application to forget the user and remove the token.

## Login

The backend returns an object containing approximately:

```text
userId
fullName
role
token
expiresInMinutes
```

The frontend stores:

```js
localStorage.setItem(
  'accessToken',
  String(res.data.token || '')
);
```

Then it calls:

```js
await refreshMe();
```

So the normal flow is:

    login()
      -> receive JWT
      -> save accessToken
      -> getMe()
      -> populate me
      -> application considers user authenticated

---

# 7. RequireAuth

Known component:

```js
import { Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

export default function RequireAuth({ children }) {
  const { me, loading } = useAuth();

  if (loading) return <div style={{ padding: 16 }}>Loading...</div>;

  if (!me) return <Navigate to="/login" replace />;

  return children;
}
```

Meaning:

- while authentication is being restored -> show Loading
- if there is no authenticated user -> redirect to Login
- otherwise render the protected page

Important:

`RequireAuth` does not validate the JWT itself. It relies on `AuthContext`.

---

# 8. RequireRole

Known component:

```js
import { Navigate } from 'react-router-dom';
import { useAuth } from './AuthContext';

export default function RequireRole({ roles, children }) {
  const { me, loading } = useAuth();

  if (loading) return <div style={{ padding: 16 }}>Loading...</div>;
  if (!me) return <Navigate to="/login" replace />;

  const roleName = me?.role?.name ?? me?.role ?? '';
  const allowed = Array.isArray(roles) && roles.includes(roleName);

  if (!allowed) return <Navigate to="/" replace />;

  return children;
}
```

Important distinction:

- no user -> `/login`
- authenticated but wrong role -> `/`

This is frontend UI protection.

It is NOT a replacement for backend authorization.

The backend must still enforce authorization.

---

# 9. Role representation on frontend

There are two possible shapes seen in the project:

### Object

```js
me.role.name
```

Example:

```json
{
  "role": {
    "name": "Engineer"
  }
}
```

### String

```js
me.role
```

Example:

```json
{
  "role": "Engineer"
}
```

The safer role extraction used in `MainLayout` and `RequireRole` is:

```js
const roleName = me?.role?.name ?? me?.role ?? '';
```

Some existing pages use:

```js
const role = me?.role.name || '';
```

At the time of the conversation this was considered acceptable because the current returned `me.role` was expected to be an object.

Do not change this blindly. First inspect the actual `/api/auth/me` response and backend DTO.

---

# 10. MainLayout

Known file:

    src/layouts/MainLayout.jsx

Purpose:

- application header
- navigation
- display current user
- display role
- display UserId
- logout
- render child pages through `<Outlet />`

Known code structure:

```jsx
const { me, logout } = useAuth();

const roleName = me?.role?.name ?? me?.role ?? '';
```

Navigation:

```jsx
<Link to="/">Dashboard</Link>
```

Breakdowns link:

```jsx
{(roleName === 'Engineer' || roleName === 'EngineeringManager') && (
  <Link to="/workorders">Breakdowns</Link>
)}
```

ShiftTasks:

```jsx
{(roleName === 'Engineer' || roleName === 'EngineeringManager') && (
  <Link to="/shifttasks">ShiftTasks</Link>
)}
```

PPM:

```jsx
{(roleName === 'Engineer' || roleName === 'EngineeringManager') && (
  <Link to="/ppm">PPM</Link>
)}
```

Important:

This navigation only controls what links are visible.

It does NOT itself secure the route.

Actual route protection must happen through route guards and, most importantly, backend authorization.

---

# 11. React Router and Outlet concept

`MainLayout` contains:

```jsx
<Outlet />
```

The layout itself does not directly call the WorkOrders page.

A link such as:

```jsx
<Link to="/workorders">Breakdowns</Link>
```

only navigates to:

    /workorders

If the router defines:

```jsx
<Route path="/workorders" element={<WorkOrders />} />
```

then React Router renders `WorkOrders` inside:

```jsx
<Outlet />
```

This distinction was explicitly discussed during development.

---

# 12. Dashboard

Known page:

    src/pages/Dashboard.jsx

The Dashboard uses:

```js
const { me, loading: authLoading } = useAuth();
```

It loads dashboard data using:

```js
getDashboard(10)
```

The response is stored in:

```js
setData(res.data);
```

The Dashboard waits for authentication:

```js
if (!authLoading && me) load();
```

If there is no authenticated user:

```jsx
if (!me) return <div>Unauthorized</div>;
```

---

# 13. Dashboard roles

The Dashboard currently recognizes:

```text
Engineer
EngineeringManager
Operator
QA
ProductionManager
```

Known checks:

```js
const isEngineer = role === 'Engineer';
const isManager = role === 'EngineeringManager';
const isOperator = role === 'Operator';
const isQa = role === 'QA';
const isProductionManager = role === 'ProductionManager';

const supported =
  isEngineer ||
  isManager ||
  isOperator ||
  isQa ||
  isProductionManager;
```

This indicates that the product is intended to have role-specific dashboards.

---

# 14. Engineer Dashboard

For an Engineer, the Dashboard shows:

## My Open Breakdowns

Data:

```js
data?.myOpen || []
```

Link:

```text
/workorders
```

## Unassigned New

Data:

```js
data?.unassignedNew || []
```

Link:

```text
/workorders
```

## My Shift Tasks

Currently marked:

```text
MVP next
```

Link:

```text
/shifttasks
```

## PPM Plans

Currently marked:

```text
MVP next
```

Link:

```text
/ppm
```

---

# 15. Engineering Manager Dashboard

For `EngineeringManager`, the Dashboard currently provides navigation:

- Manage Breakdowns
- ShiftTasks
- PPM

Counts/overview were marked as future work.

---

# 16. Operator Dashboard

Operator sees:

```text
My Open Breakdowns (Reported by Me)
```

using:

```js
data?.myOpenReported || []
```

The Operator is intentionally not given the full WorkOrders list in the current UI design.

---

# 17. QA Dashboard

QA sees:

```text
My Open Breakdowns (Reported by Me)
```

plus:

```text
Open QA Requests
```

The QA requests section was marked as future/MVP later because there was not yet a list endpoint.

---

# 18. Production Manager Dashboard

Production Manager sees:

```text
All Open Breakdowns
```

using:

```js
data?.allOpen || []
```

and can navigate to:

```text
/workorders
```

The WorkOrders page uses a different endpoint for Production Manager and displays only open breakdowns.

---

# 19. WorkOrders / Breakdowns page

Known file:

    src/pages/WorkOrders.jsx

Purpose:

- list breakdowns / work orders
- allow Engineers and Engineering Managers to see the full list
- allow Production Manager to see open work orders
- prevent Operator/QA from using the list page
- navigate to individual work order details

Known imports:

```js
import React, { useEffect, useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getWorkOrders, getOpenWorkOrders } from '../api/endpoints';
```

---

# 20. WorkOrders frontend role rules

The page calculates:

```js
const isEngineer = role === 'Engineer';
const isManager = role === 'EngineeringManager';
const isProductionManager = role === 'ProductionManager';
const isOperator = role === 'Operator';
const isQa = role === 'QA';

const canAccessList =
  isEngineer ||
  isManager ||
  isProductionManager;
```

Therefore:

### Allowed full/list access

- Engineer
- EngineeringManager

### Allowed restricted list access

- ProductionManager

Production Manager uses open-only data.

### Not allowed to use list page

- Operator
- QA

The UI shows a message and a Dashboard link for these users.

---

# 21. WorkOrders API calls

For Production Manager:

```js
await getOpenWorkOrders(200)
```

For Engineer / EngineeringManager:

```js
await getWorkOrders()
```

Therefore there is a deliberate difference between:

    full WorkOrders list

and:

    open WorkOrders list

The exact endpoint URLs should be taken from the current `src/api/endpoints.js` in GitHub.

---

# 22. WorkOrders table

Current known columns:

```text
ID
Description
Status
UnitId
AreaId
ReportedBy
CreatedAt
```

Each row is clickable:

```js
navigate(`/workorders/${x.workOrderId ?? x.id}`)
```

So the individual WorkOrder detail route is expected to be:

```text
/workorders/{id}
```

The exact detail page and route should be inspected in the repository.

---

# 23. WorkOrderCard / Dashboard cards

Dashboard cards display:

```text
Breakdown #{workOrderId}
Status
Priority
Unit
Area
Created
```

They contain:

```jsx
<Link to={`/workorders/${x.workOrderId}`}>Open</Link>
```

This means the Dashboard and WorkOrders page share the same conceptual WorkOrder entity.

---

# 24. Backend WorkOrders controller

The backend contains a WorkOrders controller with a list endpoint approximately:

```csharp
[HttpGet]
public async Task<ActionResult<List<WorkOrderResponse>>> List(
    [FromQuery] int? status = null,
    [FromQuery] int? unitId = null)
{
    var deny = RequireRoles<List<WorkOrderResponse>>(
        "Engineer",
        "EngineeringManager");

    if (deny != null)
        return deny;

    var q = _db.WorkOrders.AsNoTracking();

    if (status.HasValue)
        q = q.Where(x => (int)x.Status == status.Value);

    if (unitId.HasValue)
        q = q.Where(x => x.UnitId == unitId.Value);

    var ids = await q
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => x.WorkOrderId)
        .Take(200)
        .ToListAsync();

    var result = new List<WorkOrderResponse>();

    foreach (var id in ids)
        result.Add(await LoadResponseAsync(id));

    return Ok(result);
}
```

Important behavior:

- optional `status` filter
- optional `unitId` filter
- newest work orders first
- maximum 200 IDs
- then each ID is converted into a `WorkOrderResponse` using `LoadResponseAsync`

The exact implementation in GitHub should be considered authoritative.

---

# 25. IMPORTANT KNOWN JWT MIGRATION BUG

This was the last concrete problem being investigated.

The WorkOrders controller still had:

```csharp
var deny = RequireRoles<List<WorkOrderResponse>>(
    "Engineer",
    "EngineeringManager");

if (deny != null)
    return deny;
```

The helper was:

```csharp
private ActionResult<T>? RequireRoles<T>(params string[] roles)
{
    var u = HttpContext.Items["CurrentUser"];

    if (u == null)
        return Unauthorized("Missing X-UserId");

    var p =
        u.GetType().GetProperty("RoleName")
        ?? u.GetType().GetProperty("Role");

    var role = p?.GetValue(u)?.ToString();

    if (string.IsNullOrWhiteSpace(role))
        return Unauthorized("Missing role");

    if (!roles.Contains(role))
        return Forbid();

    return null;
}
```

This helper belongs to the OLD authentication model.

It expects:

```text
HttpContext.Items["CurrentUser"]
```

and the old system used `X-UserId`.

The frontend, however, has already moved to:

```text
JWT
Authorization: Bearer <token>
```

Therefore, unless some middleware still deliberately populates `HttpContext.Items["CurrentUser"]`, `u` will be null.

That produces:

```text
401 Unauthorized
"Missing X-UserId"
```

The frontend Axios interceptor sees the 401 and does:

```js
localStorage.removeItem('accessToken');
window.location.href = '/login';
```

That is why clicking Breakdowns can result in being sent back to Login.

This was identified as the direct cause.

---

# 26. Correct conceptual solution for the WorkOrders authorization

The backend should no longer depend on:

```csharp
HttpContext.Items["CurrentUser"]
```

for JWT role authorization.

JWT authentication creates an authenticated `ClaimsPrincipal`, available through:

```csharp
HttpContext.User
```

and commonly through:

```csharp
User
```

in a controller.

Roles should be obtained from claims or, preferably, authorization should be handled using ASP.NET Core's standard authorization mechanism.

Preferred modern approach:

```csharp
[Authorize(Roles = "Engineer,EngineeringManager")]
```

on the endpoint or controller.

However, before changing the code, inspect the existing JWT generation and configuration because the role claim must be correctly configured.

---

# 27. JWT claims must be verified before changing authorization

The next AI must inspect where the JWT is generated.

Look for code containing things such as:

```csharp
JwtSecurityToken
```

or:

```csharp
SecurityTokenDescriptor
```

or:

```csharp
new Claim(...)
```

or:

```csharp
ClaimTypes.Role
```

or:

```csharp
"role"
```

The token should contain enough identity information to authorize the request.

Typically:

```text
sub / NameIdentifier -> user ID
role -> role name
name -> display name
```

The exact claim names depend on the current implementation.

Do not assume the role claim is `ClaimTypes.Role` until the repository has been checked.

---

# 28. Recommended authorization model

The intended architecture should eventually be:

    JWT authentication
           |
           v
    ClaimsPrincipal
           |
           +---- user ID claim
           |
           +---- role claim
           |
           v
    ASP.NET Core [Authorize]
           |
           +---- authenticated?
           |
           +---- correct role?
           |
           v
    Controller action

Avoid mixing:

```text
JWT authentication
+
X-UserId
+
HttpContext.Items["CurrentUser"]
+
manual role reflection
```

unless there is a specific architectural reason.

The project was in the middle of this migration when development stopped.

---

# 29. Important distinction: 401 vs 403

The next AI should preserve this semantic distinction.

### 401 Unauthorized

The request is not successfully authenticated.

Typical reasons:

- no JWT
- invalid JWT
- expired JWT
- bad issuer
- bad audience
- invalid signature
- authentication configuration problem

### 403 Forbidden

The JWT is valid and the user is authenticated, but the user's role is not allowed.

Example:

```text
Operator -> /workorders
```

If Operator is authenticated but not authorized for the full WorkOrders list, the correct result is generally:

```text
403
```

not:

```text
401
```

The frontend should not normally treat every 403 as "go to login".

---

# 30. Why the current frontend redirect is sensitive

Current Axios behavior:

```js
if (error?.response?.status === 401) {
  localStorage.removeItem('accessToken');
  window.location.href = '/login';
}
```

This is reasonable for a genuine JWT failure.

But if a backend controller incorrectly returns 401 for a role denial, it causes a false logout.

Therefore backend authorization needs to be corrected first.

---

# 31. Existing business roles

Known roles in the frontend:

```text
Engineer
EngineeringManager
Operator
QA
ProductionManager
```

These roles have different intended access.

Current high-level concept:

### Engineer

- Dashboard
- Breakdowns
- ShiftTasks
- PPM
- own/open breakdown information

### EngineeringManager

- Dashboard
- Manage Breakdowns
- ShiftTasks
- PPM

### Operator

- Dashboard
- own reported/open breakdowns
- should not have full WorkOrders list

### QA

- Dashboard
- own reported/open breakdowns
- QA requests area
- should not have full WorkOrders list

### ProductionManager

- Dashboard
- open breakdowns
- restricted WorkOrders list

Exact permissions should be verified against the current backend routes and repository.

---

# 32. WorkOrders authorization inconsistency to check

The frontend says ProductionManager can access the WorkOrders list:

```js
const canAccessList =
  isEngineer ||
  isManager ||
  isProductionManager;
```

and then calls:

```js
getOpenWorkOrders(200)
```

However, the shown backend `List()` endpoint only allows:

```text
Engineer
EngineeringManager
```

through:

```csharp
RequireRoles("Engineer", "EngineeringManager")
```

That is intentional if ProductionManager has a separate open endpoint.

The next AI must inspect `getOpenWorkOrders()` and its backend endpoint before modifying authorization.

Do not simply add ProductionManager to every WorkOrders endpoint.

---

# 33. Dashboard data endpoint

Dashboard calls:

```js
getDashboard(10)
```

The exact endpoint and backend implementation should be inspected from:

```text
src/api/endpoints.js
```

and the corresponding backend controller/service.

Known Dashboard response concepts:

```text
myOpen
unassignedNew
myOpenReported
allOpen
```

These correspond to different roles.

The next AI should preserve these semantics unless the business requirements have changed.

---

# 34. PPM

PPM means:

**Planned Preventive Maintenance**

It is one of the main Mainty modules.

At the point captured in this handoff:

- PPM navigation existed
- Engineer and EngineeringManager had PPM navigation
- the Dashboard showed a PPM Plans section
- that section was marked MVP/future work

The exact current PPM implementation must be taken from GitHub.

---

# 35. ShiftTasks

ShiftTasks is another main module.

At the point captured:

- Engineer and EngineeringManager had ShiftTasks navigation
- Engineer Dashboard contained "My Shift Tasks (Today)"
- that section was marked MVP/future work

The exact current implementation must be taken from GitHub.

---

# 36. WorkOrder concept

The main maintenance entity is the Work Order / Breakdown.

Known properties used by the frontend include:

```text
workOrderId
description
status
priority
unitId
areaId
reportedByUserId
createdAt
```

The WorkOrder response is:

```text
WorkOrderResponse
```

The backend has:

```csharp
LoadResponseAsync(id)
```

which suggests the final response is assembled from the work order and related data.

The next AI should inspect the actual entity, DTO, related entities and `LoadResponseAsync()` before making database/API changes.

---

# 37. Database / EF Core

The backend uses Entity Framework Core.

Known usage:

```csharp
_db.WorkOrders
```

and:

```csharp
AsNoTracking()
```

Queries are performed using LINQ.

The exact DbContext, entities, migrations and database schema should be read from the repository.

Do not reconstruct the database schema from this document alone.

---

# 38. Development style / working rules

The project was developed incrementally, with explicit phases and steps.

The user prefers:

- step-by-step work
- one file at a time when modifying code
- full files rather than fragments when a file is being replaced
- first ask for the old file before providing a complete replacement
- avoid changing unrelated files
- explain exactly what changed
- code comments and identifiers should be in English
- do not make broad architectural changes without checking the current code

When continuing the project, respect this workflow.

If replacing a file, first request the current file from the user if it is not available in the repository/tool context.

---

# 39. Important historical authentication context

Before JWT, the project used a custom user context mechanism.

The old approach involved:

```text
X-UserId
```

and:

```csharp
HttpContext.Items["CurrentUser"]
```

The project also had a `CurrentUser` concept with properties such as:

```text
RoleName
Role
```

This old system should be considered legacy unless the current repository proves that part of it is still intentionally used.

The project has now moved toward JWT.

---

# 40. Security migration checklist

When continuing development, inspect these areas in order:

1. JWT generation
2. JWT validation configuration
3. JWT claims
4. `Program.cs`
5. authentication middleware
6. authorization middleware
7. `/api/auth/login`
8. `/api/auth/me`
9. frontend `AuthContext`
10. Axios interceptors
11. `RequireAuth`
12. `RequireRole`
13. WorkOrders controller
14. any remaining `X-UserId`
15. any remaining `HttpContext.Items["CurrentUser"]`

Search the repository for:

```text
X-UserId
```

```text
CurrentUser
```

```text
RequireRoles
```

```text
ClaimTypes.Role
```

```text
"role"
```

```text
Authorize
```

This will reveal which parts of the old authentication system remain.

---

# 41. How the Breakdowns login bug should be debugged

The exact known scenario was:

1. User is logged in.
2. Dashboard works.
3. User clicks "Breakdowns".
4. WorkOrders page starts.
5. Backend WorkOrders list calls:
   ```csharp
   RequireRoles(...)
   ```
6. `RequireRoles()` checks:
   ```csharp
   HttpContext.Items["CurrentUser"]
   ```
7. Because JWT is now being used, `CurrentUser` is null.
8. Backend returns:
   ```text
   401 Missing X-UserId
   ```
9. Axios response interceptor sees 401.
10. It removes:
    ```text
    accessToken
    ```
11. It executes:
    ```js
    window.location.href = '/login';
    ```
12. User is returned to Login.

The key point is:

**The problem is not that the React WorkOrders page itself navigates to Login. The API's 401 triggers the global Axios interceptor.**

---

# 42. Correct debugging sequence

When the next AI investigates an authentication problem:

### Step 1

Check browser Network -> Fetch/XHR.

Ignore requests such as:

```text
/src/pages/WorkOrders.jsx?t=...
```

with:

```text
304 Not Modified
```

Those are Vite module requests and are not the API authorization problem.

### Step 2

Find the API request.

### Step 3

Check:

```text
Request Headers
Authorization: Bearer ...
```

### Step 4

Check response:

```text
200
401
403
500
```

### Step 5

If 401, inspect backend authentication/legacy authorization.

### Step 6

If 403, inspect role configuration.

---

# 43. Do not confuse Vite 304 with API failure

A request such as:

```text
http://localhost:5173/src/pages/WorkOrders.jsx?t=...
```

returning:

```text
304 Not Modified
```

is normal.

It means the browser/Vite development server determined that the module has not changed relative to the cached version.

It does not mean the WorkOrders API succeeded or failed.

---

# 44. Frontend route protection vs backend authorization

There are two different layers.

## Frontend

Examples:

```text
RequireAuth
RequireRole
conditional navigation links
```

These improve user experience and prevent inappropriate UI access.

## Backend

Must enforce real security.

Examples:

```text
[Authorize]
[Authorize(Roles = "...")]
```

or an equivalent correctly implemented authorization policy.

A malicious user can bypass frontend routing, so backend authorization is mandatory.

---

# 45. Current architectural goal

The clean target architecture is:

```text
React
 |
 | Authorization: Bearer JWT
 v
ASP.NET Core JWT Authentication
 |
 | authenticated ClaimsPrincipal
 v
ASP.NET Core Authorization
 |
 | role/policy
 v
Controller
 |
 v
Application/business logic
 |
 v
EF Core
 |
 v
Database
```

Not:

```text
React
 |
 | JWT
 v
JWT authentication
 |
 v
Controller
 |
 | X-UserId
 v
CurrentUser in HttpContext.Items
```

unless there is a deliberate compatibility layer.

---

# 46. Repository-first rule

The next AI should NOT assume that snippets in this document are the exact latest files.

The user has the code in GitHub.

The correct process is:

1. Read this handoff.
2. Inspect the GitHub repository.
3. Identify the current branch/commit.
4. Compare the repository with the historical information here.
5. Treat current repository code as authoritative.
6. Ask for clarification only when business intent cannot be inferred from the repository/history.

---

# 47. Suggested first repository inspection

Before writing code, inspect:

```text
frontend package.json
frontend src/App.jsx
frontend src/auth/AuthContext.jsx
frontend src/auth/RequireAuth.jsx
frontend src/auth/RequireRole.jsx
frontend src/api/apiClient.js
frontend src/api/endpoints.js
frontend src/layouts/MainLayout.jsx
frontend src/pages/Dashboard.jsx
frontend src/pages/WorkOrders.jsx
```

Backend:

```text
Program.cs
appsettings.json
Auth controller/service
JWT service
WorkOrders controller
DbContext
WorkOrder entity
WorkOrderResponse DTO
authentication/authorization configuration
```

Then search globally for:

```text
X-UserId
CurrentUser
RequireRoles
Authorize
Jwt
ClaimTypes
Role
accessToken
```

---

# 48. Current known stopping point

The project was not stopped because the entire architecture was broken.

The concrete issue at the stopping point was the JWT migration inconsistency around WorkOrders authorization.

The frontend already had JWT handling:

```text
accessToken
Authorization: Bearer JWT
AuthContext
getMe()
RequireAuth
RequireRole
```

but at least one backend authorization helper was still using:

```text
HttpContext.Items["CurrentUser"]
X-UserId
```

That is the first area to reconcile.

---

# 49. Important caution about fixing the problem

Do not immediately replace every `RequireRoles` call with `[Authorize]`.

First determine:

- where the JWT is generated
- what claims are included
- how the role claim is configured
- whether `User.IsInRole()` works
- whether the application has a custom authorization policy
- which endpoints need which roles
- whether some endpoints intentionally use a separate role

Then migrate systematically.

---

# 50. Likely clean implementation

If the existing JWT is configured with the standard role claim, an endpoint could eventually look like:

```csharp
[Authorize(Roles = "Engineer,EngineeringManager")]
[HttpGet]
public async Task<ActionResult<List<WorkOrderResponse>>> List(
    [FromQuery] int? status = null,
    [FromQuery] int? unitId = null)
{
    var q = _db.WorkOrders.AsNoTracking();

    if (status.HasValue)
        q = q.Where(x => (int)x.Status == status.Value);

    if (unitId.HasValue)
        q = q.Where(x => x.UnitId == unitId.Value);

    var ids = await q
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => x.WorkOrderId)
        .Take(200)
        .ToListAsync();

    var result = new List<WorkOrderResponse>();

    foreach (var id in ids)
        result.Add(await LoadResponseAsync(id));

    return Ok(result);
}
```

But this is a **target example**, not a command to overwrite the current file without inspection.

---

# 51. User's preferred development process

The user wants controlled incremental development.

When the user asks to modify a file:

1. Identify the exact file.
2. Ask for the current/old file if it has not been supplied or retrieved.
3. Explain the intended change briefly.
4. Provide the complete replacement file, not a partial snippet, when replacement is requested.
5. Change only what is necessary.
6. Wait for the user to confirm the result.
7. Continue to the next file only after confirmation.

Do not dump changes for ten files at once unless the user explicitly asks for that.

---

# 52. Communication style for continuing work

The user generally prefers Bulgarian explanations.

Technical identifiers, code, variable names and comments should remain in English.

Avoid unnecessary praise such as:

```text
Много хубав въпрос
```

Prefer direct explanations such as:

```text
Причината е тук:
```

or:

```text
Този метод прави следното:
```

When debugging, show the exact chain of events.

---

# 53. What another AI should NOT assume

Do not assume:

- the current GitHub code is identical to every historical snippet
- every planned feature is implemented
- every role has final permissions
- PPM is complete
- ShiftTasks is complete
- Dashboard data is final
- JWT claims are configured correctly
- old authentication code has been completely removed
- route definitions are exactly as described above

Verify these against the repository.

---

# 54. Product direction

Mainty is intended to become a real maintenance engineering platform rather than just a CRUD work-order application.

Core concepts include:

- breakdown reporting
- work orders
- engineers
- engineering managers
- operators
- QA
- production management
- shift tasks
- planned preventive maintenance
- dashboard views
- role-based workflows

The architecture should therefore remain extensible and should avoid tightly coupling UI permissions to database implementation details.

---

# 55. Current mental model of the application

Think of Mainty as:

```text
USER
 |
 +--> Login
 |      |
 |      +--> JWT
 |
 +--> Dashboard
 |      |
 |      +--> Engineer
 |      |      +--> My Open Breakdowns
 |      |      +--> Unassigned New
 |      |      +--> ShiftTasks
 |      |      +--> PPM
 |      |
 |      +--> EngineeringManager
 |      |      +--> Manage Breakdowns
 |      |      +--> ShiftTasks
 |      |      +--> PPM
 |      |
 |      +--> Operator
 |      |      +--> My reported/open breakdowns
 |      |
 |      +--> QA
 |      |      +--> My reported/open breakdowns
 |      |      +--> QA Requests
 |      |
 |      +--> ProductionManager
 |             +--> All/Open Breakdowns
 |
 +--> Breakdowns
        |
        +--> WorkOrders list
        |
        +--> WorkOrder detail
```

---

# 56. Final handoff summary

The most important facts for continuing Mainty are:

1. **Mainty is a React + ASP.NET Core maintenance management application.**
2. **JWT authentication is now being used.**
3. The frontend stores the JWT as:
   ```text
   localStorage.accessToken
   ```
4. Axios attaches:
   ```text
   Authorization: Bearer <JWT>
   ```
5. Axios redirects to Login when an API returns 401.
6. `AuthContext` calls `/auth/me`/`getMe()` to restore the current user.
7. `RequireAuth` protects authenticated routes.
8. `RequireRole` protects frontend routes by role.
9. Main roles currently known:
   ```text
   Engineer
   EngineeringManager
   Operator
   QA
   ProductionManager
   ```
10. Dashboard behavior is role-specific.
11. WorkOrders/Breakdowns is the central maintenance list.
12. Engineer and EngineeringManager have the full list.
13. ProductionManager has an open-only view.
14. Operator and QA should not use the full list page.
15. The known Breakdowns -> Login problem was caused by backend `RequireRoles()` still reading:
   ```csharp
   HttpContext.Items["CurrentUser"]
   ```
   despite the migration to JWT.
16. That helper returns 401 when `CurrentUser` is missing.
17. Axios interprets the 401 as an invalid JWT and sends the user to Login.
18. The correct long-term direction is JWT claims + ASP.NET Core authorization, after verifying the actual JWT generation/configuration.
19. The GitHub repository is the authoritative source for the current implementation.
20. Continue development incrementally and carefully, one file at a time when modifying code.

---

# 57. Starting point for the next AI

Give the next AI:

1. this document
2. the GitHub repository URL
3. the current branch/commit if known

Then tell it:

> "Read the Mainty handoff document first. Then inspect the GitHub repository and compare the actual current code with the handoff. Do not modify anything yet. First explain the current architecture and identify whether the JWT migration is complete, especially the WorkOrders authorization path."

This should prevent the new AI from immediately guessing at the architecture or reintroducing the old `X-UserId` authentication mechanism.

---

**End of Mainty handoff document.**
