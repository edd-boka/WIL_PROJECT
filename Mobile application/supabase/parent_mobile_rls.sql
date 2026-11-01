-- Run this in the Supabase SQL Editor after the API has created its tables.
-- The mobile app uses only the public/anon key; these policies are the access
-- control boundary for every direct database read made by a parent.

CREATE OR REPLACE FUNCTION public.current_verified_parent_email()
RETURNS text
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $$
  SELECT lower(u.email)
  FROM auth.users AS u
  WHERE u.id = (SELECT auth.uid())
    AND u.email_confirmed_at IS NOT NULL
$$;

REVOKE ALL ON FUNCTION public.current_verified_parent_email() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION public.current_verified_parent_email() TO authenticated;

GRANT USAGE ON SCHEMA public TO authenticated;
GRANT SELECT ON TABLE
  public.learner_parent_emails,
  public.learners,
  public.marks,
  public.assessments,
  public.subjects
TO authenticated;
REVOKE SELECT ON TABLE
  public.learner_parent_emails,
  public.learners,
  public.marks,
  public.assessments,
  public.subjects
FROM anon, PUBLIC;

ALTER TABLE public.learner_parent_emails ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.learners ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.marks ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.assessments ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.subjects ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "parents_read_matching_learner_emails"
  ON public.learner_parent_emails;
CREATE POLICY "parents_read_matching_learner_emails"
  ON public.learner_parent_emails
  FOR SELECT
  TO authenticated
  USING (email = (SELECT public.current_verified_parent_email()));

DROP POLICY IF EXISTS "parents_limit_learner_email_rows"
  ON public.learner_parent_emails;
CREATE POLICY "parents_limit_learner_email_rows"
  ON public.learner_parent_emails
  AS RESTRICTIVE
  FOR SELECT
  TO authenticated
  USING (email = (SELECT public.current_verified_parent_email()));

DROP POLICY IF EXISTS "parents_read_learners_with_matching_email"
  ON public.learners;
CREATE POLICY "parents_read_learners_with_matching_email"
  ON public.learners
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.learner_parent_emails AS parent_email
      WHERE parent_email.learner_id = learners.id
        AND parent_email.email = (SELECT public.current_verified_parent_email())
    )
  );

DROP POLICY IF EXISTS "parents_limit_learner_rows"
  ON public.learners;
CREATE POLICY "parents_limit_learner_rows"
  ON public.learners
  AS RESTRICTIVE
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.learner_parent_emails AS parent_email
      WHERE parent_email.learner_id = learners.id
        AND parent_email.email = (SELECT public.current_verified_parent_email())
    )
  );

DROP POLICY IF EXISTS "parents_read_marks_for_matching_email"
  ON public.marks;
CREATE POLICY "parents_read_marks_for_matching_email"
  ON public.marks
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.learner_parent_emails AS parent_email
      WHERE parent_email.learner_id = marks.learner_id
        AND parent_email.email = (SELECT public.current_verified_parent_email())
    )
  );

DROP POLICY IF EXISTS "parents_limit_mark_rows"
  ON public.marks;
CREATE POLICY "parents_limit_mark_rows"
  ON public.marks
  AS RESTRICTIVE
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.learner_parent_emails AS parent_email
      WHERE parent_email.learner_id = marks.learner_id
        AND parent_email.email = (SELECT public.current_verified_parent_email())
    )
  );

DROP POLICY IF EXISTS "parents_read_assessments_with_visible_marks"
  ON public.assessments;
CREATE POLICY "parents_read_assessments_with_visible_marks"
  ON public.assessments
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.marks AS visible_mark
      WHERE visible_mark.assessment_id = assessments.id
    )
  );

DROP POLICY IF EXISTS "parents_limit_assessment_rows"
  ON public.assessments;
CREATE POLICY "parents_limit_assessment_rows"
  ON public.assessments
  AS RESTRICTIVE
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.marks AS visible_mark
      WHERE visible_mark.assessment_id = assessments.id
    )
  );

DROP POLICY IF EXISTS "parents_read_subjects_with_visible_marks"
  ON public.subjects;
CREATE POLICY "parents_read_subjects_with_visible_marks"
  ON public.subjects
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.marks AS visible_mark
      WHERE visible_mark.subject_id = subjects.id
    )
  );

DROP POLICY IF EXISTS "parents_limit_subject_rows"
  ON public.subjects;
CREATE POLICY "parents_limit_subject_rows"
  ON public.subjects
  AS RESTRICTIVE
  FOR SELECT
  TO authenticated
  USING (
    EXISTS (
      SELECT 1
      FROM public.marks AS visible_mark
      WHERE visible_mark.subject_id = subjects.id
    )
  );

-- Return only subject averages for grades in which the signed-in parent has
-- a linked learner. Individual classmate records remain hidden by the RLS
-- policies above.
CREATE OR REPLACE FUNCTION public.parent_grade_subject_averages()
RETURNS TABLE (
  grade text,
  subject_id text,
  subject_name text,
  class_average double precision
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = ''
AS $$
  WITH parent_grades AS (
    SELECT DISTINCT learner.grade
    FROM public.learners AS learner
    JOIN public.learner_parent_emails AS parent_email
      ON parent_email.learner_id = learner.id
    WHERE parent_email.email = (
      SELECT public.current_verified_parent_email()
    )
  ),
  learner_subject_averages AS (
    SELECT
      learner.grade,
      mark.learner_id,
      COALESCE(assessment.subject_id, mark.subject_id) AS subject_id,
      AVG(
        CASE
          WHEN mark.total > 0 THEN mark.score / mark.total * 100
          ELSE 0
        END
      ) AS learner_average
    FROM public.learners AS learner
    JOIN parent_grades
      ON parent_grades.grade = learner.grade
    JOIN public.marks AS mark
      ON mark.learner_id = learner.id
    LEFT JOIN public.assessments AS assessment
      ON assessment.id = mark.assessment_id
    GROUP BY
      learner.grade,
      mark.learner_id,
      COALESCE(assessment.subject_id, mark.subject_id)
  )
  SELECT
    learner_subject_averages.grade,
    learner_subject_averages.subject_id::text,
    subject.name,
    AVG(learner_subject_averages.learner_average)::double precision
  FROM learner_subject_averages
  JOIN public.subjects AS subject
    ON subject.id = learner_subject_averages.subject_id
  GROUP BY
    learner_subject_averages.grade,
    learner_subject_averages.subject_id,
    subject.name
  HAVING COUNT(*) >= 3
$$;

REVOKE ALL ON FUNCTION public.parent_grade_subject_averages()
  FROM PUBLIC, anon;
GRANT EXECUTE ON FUNCTION public.parent_grade_subject_averages()
  TO authenticated;
