import 'package:supabase_flutter/supabase_flutter.dart';

import '../models/learner_progress.dart';

class ParentDataService {
  const ParentDataService(this._client);

  final SupabaseClient _client;

  Future<List<LearnerProgress>> loadLearners(String email) async {
    final normalizedEmail = email.trim().toLowerCase();
    if (normalizedEmail.isEmpty) {
      throw StateError('The signed-in account does not have an email address.');
    }

    final emailRows = await _client
        .from('learner_parent_emails')
        .select('learner_id')
        .eq('email', normalizedEmail);
    final learnerIds = emailRows
        .map((row) => row['learner_id'] as String)
        .toSet()
        .toList();
    if (learnerIds.isEmpty) return const [];

    final learnerRows = await _client
        .from('learners')
        .select('id, first_name, last_name, grade')
        .inFilter('id', learnerIds)
        .order('last_name')
        .order('first_name');
    if (learnerRows.isEmpty) return const [];

    final visibleLearnerIds = learnerRows
        .map((row) => row['id'] as String)
        .toList();
    final markRows = await _client
        .from('marks')
        .select(
          'learner_id, subject_id, assessment_id, assessment_type, '
          'score, total, date_recorded',
        )
        .inFilter('learner_id', visibleLearnerIds);

    final assessmentIds = markRows
        .map((row) => row['assessment_id'])
        .whereType<String>()
        .toSet()
        .toList();
    final assessments = assessmentIds.isEmpty
        ? <Map<String, dynamic>>[]
        : await _client
              .from('assessments')
              .select('id, title, assessment_type, subject_id')
              .inFilter('id', assessmentIds);
    final assessmentById = {
      for (final row in assessments) row['id'] as String: row,
    };

    final subjectIds = {
      ...markRows.map((row) => row['subject_id']).whereType<String>(),
      ...assessments.map((row) => row['subject_id'] as String),
    }.toList();
    final subjects = subjectIds.isEmpty
        ? <Map<String, dynamic>>[]
        : await _client
              .from('subjects')
              .select('id, name')
              .inFilter('id', subjectIds);
    final subjectById = {
      for (final row in subjects) row['id'] as String: row['name'] as String,
    };

    final classAverageRows = await _client.rpc('parent_grade_subject_averages');
    final classAveragesByGrade = <String, List<ClassSubjectAverage>>{};
    for (final row in classAverageRows) {
      final grade = row['grade'] as String;
      classAveragesByGrade
          .putIfAbsent(grade, () => [])
          .add(
            ClassSubjectAverage(
              subjectName: row['subject_name'] as String,
              average: (row['class_average'] as num).toDouble(),
            ),
          );
    }

    final marksByLearner = <String, List<ParentMark>>{};
    for (final row in markRows) {
      final learnerId = row['learner_id'] as String;
      final assessment = assessmentById[row['assessment_id']];
      final subjectId =
          (assessment?['subject_id'] ?? row['subject_id']) as String;
      final type =
          (assessment?['assessment_type'] ?? row['assessment_type']) as String;
      final title = (assessment?['title'] as String?) ?? type;
      final recordedAt = DateTime.parse(row['date_recorded'] as String);

      marksByLearner
          .putIfAbsent(learnerId, () => [])
          .add(
            ParentMark(
              assessmentTitle: title,
              assessmentType: type,
              subjectName: subjectById[subjectId] ?? 'Other subject',
              score: (row['score'] as num).toDouble(),
              total: (row['total'] as num).toDouble(),
              dateRecorded: recordedAt,
            ),
          );
    }

    return learnerRows.map((row) {
      final id = row['id'] as String;
      final marks = List<ParentMark>.of(
        marksByLearner[id] ?? const <ParentMark>[],
      );
      marks.sort((a, b) => b.dateRecorded.compareTo(a.dateRecorded));
      return LearnerProgress(
        id: id,
        firstName: row['first_name'] as String,
        lastName: row['last_name'] as String,
        grade: row['grade'] as String,
        marks: marks,
        classAverages: classAveragesByGrade[row['grade'] as String] ?? const [],
      );
    }).toList();
  }
}
