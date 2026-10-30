# Real-Time Progress Monitoring System — ASP.NET API Template

A working ASP.NET Web API implementing the full database design: learners,
teachers, parents, subjects, marks, users, and the parent-learner link table
that controls who can see whose marks.

## What's in here

```
ProgressMonitoring.Api/
├── Models/
│   ├── Learner.cs
│   ├── Teacher.cs             # Profile linked 1:1 to a User
│   ├── Parent.cs              # Profile linked 1:1 to a User
│   ├── Subject.cs
│   ├── Mark.cs                # References SubjectId + TeacherId
│   ├── User.cs                # Backs login (email, password hash, role)
│   └── ParentLearnerLink.cs   # The access-control table
├── Data/
│   └── AppDbContext.cs        # DbSets for all seven tables
├── Controllers/
│   ├── LearnersController.cs        # GET/POST learners, GET a learner's marks
│   ├── MarksController.cs           # POST to capture/update a mark (teacher/admin only)
│   ├── SubjectsController.cs        # GET/POST subjects
│   ├── AuthController.cs            # POST login, POST register (dev-only)
│   └── ParentLearnerLinksController.cs  # Create links (admin), view your children (parent)
├── Program.cs                 # DB + JWT auth wiring, Swagger with auth support
├── appsettings.json
└── ProgressMonitoring.Api.csproj
```

## Setup steps

1. **Install the .NET SDK** (8.0+) — confirm with `dotnet --version`.

2. **Restore packages** from inside `ProgressMonitoring.Api/`:
   ```
   dotnet restore
   ```

3. **Create your Supabase project** if you haven't, and get the connection
   string from *Project Settings → Database → Connection string* (Session
   mode pooler).

4. **Store your two secrets:**
   ```
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:Supabase" "your-connection-string"
   dotnet user-secrets set "Jwt:Key" "a-long-random-string-32-chars-or-more"
   ```

5. **Create the database migration** (this is what actually creates the
   tables — skipping it means the app runs but every database call fails):
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

6. **Run it:**
   ```
   dotnet run
   ```
   Open the printed URL + `/swagger`.

## Testing the full access-control flow

This is the part that's worth testing end-to-end, since it's the core of
your legal/ethical feasibility claims (parents only seeing their own
children's marks):

1. `POST /api/auth/register` — create a teacher:
   `{ "email": "teacher@test.com", "password": "Test123!", "role": "teacher", "firstName": "Jane", "lastName": "Smith" }`
2. `POST /api/auth/register` — create a parent the same way with `"role": "parent"`.
3. `POST /api/auth/register` — create an admin the same way with `"role": "admin"` (firstName/lastName can be blank — admins have no profile table).
4. `POST /api/auth/login` as the **admin**, copy the token, click Swagger's
   **Authorize** button and paste `Bearer <token>`.
5. `POST /api/learners` — create a test learner, copy their `id`.
6. `GET /api/parents` doesn't exist yet in this template — for now, note
   the parent's `id` by checking your database directly (e.g. Supabase's
   table editor) after step 2, since `/api/auth/register` doesn't return the
   Parent profile's own id, only the User's id.
7. `POST /api/parent-learner-links` — `{ "parentId": "...", "learnerId": "..." }`
   using the ids from steps 5–6.
8. `POST /api/subjects` — e.g. `{ "name": "Mathematics" }`, copy the `id`.
9. Log out of the admin token, `POST /api/auth/login` as the **teacher**,
   authorize Swagger with that token.
10. `POST /api/marks` — `{ "learnerId": "...", "subjectId": "...", "teacherId": "<teacher's own id>", "assessmentType": "Test", "score": 78, "total": 100 }`
11. Log in as the **parent**, authorize Swagger with that token, then
    `GET /api/learners/{id}/marks` using the learner's id — this should
    succeed **only because of the link created in step 7**. Remove that
    link and the same call should return 403 Forbidden.

## Notes

- **`/api/auth/register` is a development convenience**, not a real signup
  flow. Lock it down or remove it before treating this as production-ready.
- **Teachers and admins currently have unrestricted access** to any
  learner's marks — only the parent role is checked against the link table.
  Tighten this further (e.g. limit a teacher to learners they actually
  teach) if your project scope calls for it.
- There's no `GET /api/parents` or `GET /api/teachers` endpoint yet in this
  template — add one if you need to look up profile ids without querying
  the database directly.
- Do not commit `appsettings.Development.json`, `.env` files, or anything
  containing your Supabase connection string, JWT key, or API keys to
  GitHub, per the assessment brief's instructions.
