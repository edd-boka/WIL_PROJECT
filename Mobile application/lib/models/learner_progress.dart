class LearnerProgress {
  const LearnerProgress({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.grade,
    required this.marks,
    this.classAverages = const [],
  });

  final String id;
  final String firstName;
  final String lastName;
  final String grade;
  final List<ParentMark> marks;
  final List<ClassSubjectAverage> classAverages;

  String get fullName => '$firstName $lastName';

  double? get overallAverage {
    if (marks.isEmpty) return null;
    return marks.map((mark) => mark.percent).reduce((a, b) => a + b) /
        marks.length;
  }

  List<SubjectProgress> get subjects {
    final grouped = <String, List<ParentMark>>{};
    for (final mark in marks) {
      grouped.putIfAbsent(mark.subjectName, () => []).add(mark);
    }

    return grouped.entries.map((entry) {
      final subjectMarks = List<ParentMark>.of(entry.value)
        ..sort((a, b) => b.dateRecorded.compareTo(a.dateRecorded));
      final average =
          subjectMarks.map((mark) => mark.percent).reduce((a, b) => a + b) /
          subjectMarks.length;
      double? classAverage;
      for (final comparison in classAverages) {
        if (comparison.subjectName == entry.key) {
          classAverage = comparison.average;
          break;
        }
      }
      return SubjectProgress(
        name: entry.key,
        marks: subjectMarks,
        average: average,
        classAverage: classAverage,
      );
    }).toList()..sort((a, b) => a.name.compareTo(b.name));
  }
}

class ClassSubjectAverage {
  const ClassSubjectAverage({required this.subjectName, required this.average});

  final String subjectName;
  final double average;
}

class SubjectProgress {
  const SubjectProgress({
    required this.name,
    required this.marks,
    required this.average,
    this.classAverage,
  });

  final String name;
  final List<ParentMark> marks;
  final double average;
  final double? classAverage;

  bool get passed => average >= ParentMark.passPercent;

  PeerComparison get peerComparison {
    final comparisonAverage = classAverage;
    if (comparisonAverage == null) return PeerComparison.unavailable;
    final difference = average - comparisonAverage;
    if (difference > 5) return PeerComparison.above;
    if (difference < -5) return PeerComparison.below;
    return PeerComparison.within;
  }
}

enum PeerComparison { above, below, within, unavailable }

class ParentMark {
  const ParentMark({
    required this.assessmentTitle,
    required this.assessmentType,
    required this.subjectName,
    required this.score,
    required this.total,
    required this.dateRecorded,
  });

  static const double passPercent = 50;

  final String assessmentTitle;
  final String assessmentType;
  final String subjectName;
  final double score;
  final double total;
  final DateTime dateRecorded;

  double get percent => total > 0 ? score / total * 100 : 0;

  bool get passed => total > 0 && percent >= passPercent;
}
