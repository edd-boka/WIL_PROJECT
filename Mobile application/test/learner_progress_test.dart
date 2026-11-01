import 'package:flutter_test/flutter_test.dart';
import 'package:parent_mobile/models/learner_progress.dart';

void main() {
  group('LearnerProgress', () {
    test('calculates overall and per-subject averages', () {
      final learner = LearnerProgress(
        id: 'learner-1',
        firstName: 'Ari',
        lastName: 'Example',
        grade: '5',
        marks: [
          ParentMark(
            assessmentTitle: 'Fractions',
            assessmentType: 'Test',
            subjectName: 'Mathematics',
            score: 8,
            total: 10,
            dateRecorded: DateTime(2026, 5, 1),
          ),
          ParentMark(
            assessmentTitle: 'Geometry',
            assessmentType: 'Assignment',
            subjectName: 'Mathematics',
            score: 4,
            total: 10,
            dateRecorded: DateTime(2026, 5, 2),
          ),
          ParentMark(
            assessmentTitle: 'Reading',
            assessmentType: 'Test',
            subjectName: 'English',
            score: 6,
            total: 10,
            dateRecorded: DateTime(2026, 5, 3),
          ),
        ],
      );

      expect(learner.overallAverage, closeTo(60, 0.001));
      expect(learner.subjects.map((subject) => subject.name), [
        'English',
        'Mathematics',
      ]);
      expect(learner.subjects[1].average, closeTo(60, 0.001));
      expect(learner.subjects[1].passed, isTrue);
    });

    test('does not pass a zero-total assessment', () {
      final mark = ParentMark(
        assessmentTitle: 'Invalid total',
        assessmentType: 'Test',
        subjectName: 'English',
        score: 0,
        total: 0,
        dateRecorded: DateTime(2026, 5, 1),
      );

      expect(mark.percent, 0);
      expect(mark.passed, isFalse);
    });

    test('compares subject averages within a five percentage-point band', () {
      const within = SubjectProgress(
        name: 'Mathematics',
        marks: [],
        average: 80,
        classAverage: 75,
      );
      const above = SubjectProgress(
        name: 'Mathematics',
        marks: [],
        average: 80,
        classAverage: 74.9,
      );
      const lowerBoundary = SubjectProgress(
        name: 'Mathematics',
        marks: [],
        average: 80,
        classAverage: 85,
      );
      const below = SubjectProgress(
        name: 'Mathematics',
        marks: [],
        average: 80,
        classAverage: 85.1,
      );
      const unavailable = SubjectProgress(
        name: 'Mathematics',
        marks: [],
        average: 80,
      );

      expect(within.peerComparison, PeerComparison.within);
      expect(lowerBoundary.peerComparison, PeerComparison.within);
      expect(above.peerComparison, PeerComparison.above);
      expect(below.peerComparison, PeerComparison.below);
      expect(unavailable.peerComparison, PeerComparison.unavailable);
    });
  });
}
