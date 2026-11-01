# Parent Mobile App

Flutter app for parents to create an account or sign in with an email address
and password, and view marks for learners whose
`learner_parent_emails.email` matches their account email.
Learner and mark data is read directly from Supabase using the publishable
(anon) key and row-level security; no database password or service-role key is
included in the app.

## Supabase setup

1. Apply the API's EF Core migrations to the Supabase project so the
   `learner_parent_emails`, `learners`, `marks`, `assessments`, and `subjects`
   tables exist.
2. In Supabase **Authentication → Providers → Email**, turn off **Confirm
   email** to allow immediate sign-up without email verification. The app uses
   password-based authentication and does not send verification links or codes.
3. Run [`supabase/parent_mobile_rls.sql`](supabase/parent_mobile_rls.sql) in
   the Supabase SQL Editor. This grants authenticated users read-only access
   to records matched by their verified email, and installs a function that
   returns only subject averages for a linked learner's grade. Class averages
   are shown only when at least three learners have marks for that subject.
   The policies deliberately do not trust an email address supplied by the
   app.
4. Copy the project's **Project URL** and **publishable/anon key** from
   Supabase **Project Settings → API**. Never use a database password or the
   `service_role` key in the mobile app.

The mobile app uses Supabase email-and-password authentication. The API's
password accounts in `public.users` remain a separate login flow; the API
continues to use Supabase email OTP. The mobile app matches the signed-in
account email against
`learner_parent_emails`; no row in `public.parents` or
`public.parent_learner_links` is required for its read-only queries. The API
can continue using its existing account and linking behavior.

## Run

From this directory, pass the project settings at runtime:

```powershell
flutter pub get
flutter run --dart-define=SUPABASE_URL=https://YOUR_PROJECT.supabase.co --dart-define=SUPABASE_PUBLISHABLE_KEY=YOUR_PUBLISHABLE_OR_ANON_KEY
```

For a release build, provide the same two `--dart-define` values to
`flutter build apk` or `flutter build ios`. These values are public client
configuration; access to learner data is controlled by Supabase Auth and the
RLS policies, not by hiding the key.

The app shows learners whose parent-email row matches the signed-in email,
subject averages (using the web app's 50% pass threshold), and assessments in
date order. The learner profile includes a progress report with assessment
counts, overall average, pass rate, subject averages, pass/review counts, and a
subject assessment table. Subject cards also compare the learner's average
with the average of all learners in the same grade, classifying results more
than 5 percentage points above/below as above/below and differences within
that band as within range. The database function returns only aggregates, not
other learners' records. Use **Dark screen** or **Light screen** in the app bar
to switch themes; the selected appearance is saved on the device. Pull down or
tap refresh after a teacher adds a matching email or records new marks.
