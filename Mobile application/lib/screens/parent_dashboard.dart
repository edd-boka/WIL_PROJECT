import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:supabase_flutter/supabase_flutter.dart';

import '../models/learner_progress.dart';
import '../services/parent_data_service.dart';
import '../widgets/edutracker_brand.dart';

class ParentDashboard extends StatefulWidget {
  const ParentDashboard({
    super.key,
    required this.client,
    required this.email,
    required this.isDarkTheme,
    required this.onToggleTheme,
  });

  final SupabaseClient client;
  final String email;
  final bool isDarkTheme;
  final Future<void> Function() onToggleTheme;

  @override
  State<ParentDashboard> createState() => _ParentDashboardState();
}

class _ParentDashboardState extends State<ParentDashboard> {
  late Future<List<LearnerProgress>> _learnersFuture;
  String? _selectedLearnerId;
  String? _selectedSubject;
  bool _isSigningOut = false;

  @override
  void initState() {
    super.initState();
    _learnersFuture = _loadLearners();
  }

  Future<List<LearnerProgress>> _loadLearners() {
    return ParentDataService(widget.client).loadLearners(widget.email);
  }

  String get _signedInParentName {
    final metadata = widget.client.auth.currentUser?.userMetadata;
    final firstName = metadata?['first_name']?.toString().trim() ?? '';
    final lastName = metadata?['last_name']?.toString().trim() ?? '';
    final name = [
      firstName,
      lastName,
    ].where((part) => part.isNotEmpty).join(' ');
    return name.isEmpty ? widget.email : name;
  }

  Future<void> _refresh() async {
    final next = _loadLearners();
    setState(() {
      _learnersFuture = next;
    });
    await next;
  }

  Future<void> _signOut() async {
    setState(() => _isSigningOut = true);
    try {
      await widget.client.auth.signOut();
    } on AuthException catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Could not sign out: ${error.message}')),
        );
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Could not sign out: $error')));
      }
    } finally {
      if (mounted) setState(() => _isSigningOut = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(
        title: const Text('EduTracker Mobile App'),
        actions: [
          TextButton.icon(
            onPressed: () async {
              try {
                await widget.onToggleTheme();
              } catch (error) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('Could not save display setting: $error'),
                    ),
                  );
                }
              }
            },
            icon: Icon(
              widget.isDarkTheme
                  ? Icons.light_mode_outlined
                  : Icons.dark_mode_outlined,
            ),
            label: Text(widget.isDarkTheme ? 'Light screen' : 'Dark screen'),
          ),
          IconButton(
            tooltip: 'Refresh',
            onPressed: _refresh,
            icon: const Icon(Icons.refresh),
          ),
          IconButton(
            tooltip: 'Sign out',
            onPressed: _isSigningOut ? null : _signOut,
            icon: _isSigningOut
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.logout),
          ),
          const SizedBox(width: 8),
        ],
      ),
      body: Column(
        children: [
          const Padding(
            padding: EdgeInsets.fromLTRB(20, 12, 20, 8),
            child: EduTrackerBrand(),
          ),
          Expanded(
            child: FutureBuilder<List<LearnerProgress>>(
              future: _learnersFuture,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting &&
                    !snapshot.hasData) {
                  return const Center(child: CircularProgressIndicator());
                }
                if (snapshot.hasError) {
                  return _ErrorState(error: snapshot.error!, onRetry: _refresh);
                }

                final learners = snapshot.data ?? const <LearnerProgress>[];
                if (learners.isEmpty) {
                  return RefreshIndicator(
                    onRefresh: _refresh,
                    child: ListView(
                      padding: const EdgeInsets.all(24),
                      children: [
                        const SizedBox(height: 64),
                        Icon(
                          Icons.family_restroom_outlined,
                          size: 64,
                          color: colors.primary,
                        ),
                        const SizedBox(height: 20),
                        Text(
                          'No learners linked yet',
                          textAlign: TextAlign.center,
                          style: Theme.of(context).textTheme.headlineSmall
                              ?.copyWith(fontWeight: FontWeight.bold),
                        ),
                        const SizedBox(height: 12),
                        Text(
                          'Your signed-in email (${widget.email}) must match a '
                          'verified parent email entered by the learner’s teacher. '
                          'Once the teacher saves that email, pull down to refresh.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: colors.onSurfaceVariant),
                        ),
                      ],
                    ),
                  );
                }

                final learner = learners.firstWhere(
                  (item) => item.id == _selectedLearnerId,
                  orElse: () => learners.first,
                );
                final subjects = learner.subjects;
                final selectedSubject = subjects.firstWhere(
                  (subject) => subject.name == _selectedSubject,
                  orElse: () =>
                      subjects.isEmpty ? _emptySubject : subjects.first,
                );

                return RefreshIndicator(
                  onRefresh: _refresh,
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
                    children: [
                      Text(
                        _signedInParentName,
                        style: Theme.of(context).textTheme.headlineSmall
                            ?.copyWith(fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 5),
                      Text(
                        widget.email,
                        style: TextStyle(color: colors.onSurfaceVariant),
                      ),
                      const SizedBox(height: 20),
                      if (learners.length > 1) ...[
                        Text(
                          'LEARNERS',
                          style: Theme.of(context).textTheme.labelLarge
                              ?.copyWith(
                                color: colors.onSurfaceVariant,
                                letterSpacing: 1.2,
                              ),
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: learners.map((item) {
                            return ChoiceChip(
                              label: Text(item.fullName),
                              selected: item.id == learner.id,
                              onSelected: (_) => setState(() {
                                _selectedLearnerId = item.id;
                                _selectedSubject = null;
                              }),
                            );
                          }).toList(),
                        ),
                        const SizedBox(height: 18),
                      ],
                      _LearnerSummary(learner: learner),
                      const SizedBox(height: 24),
                      _ReportSection(
                        learner: learner,
                        subjects: subjects,
                        selectedSubject: selectedSubject,
                        onSelectSubject: (subject) =>
                            setState(() => _selectedSubject = subject.name),
                      ),
                    ],
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

const _emptySubject = SubjectProgress(name: '', marks: [], average: 0);

class _ReportSection extends StatelessWidget {
  const _ReportSection({
    required this.learner,
    required this.subjects,
    required this.selectedSubject,
    required this.onSelectSubject,
  });

  final LearnerProgress learner;
  final List<SubjectProgress> subjects;
  final SubjectProgress selectedSubject;
  final ValueChanged<SubjectProgress> onSelectSubject;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final markCount = learner.marks.length;
    final passes = learner.marks.where((mark) => mark.passed).length;
    final passRate = markCount == 0 ? 0.0 : passes / markCount * 100;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Progress report',
          style: theme.textTheme.titleLarge?.copyWith(
            fontWeight: FontWeight.bold,
          ),
        ),
        const SizedBox(height: 12),
        LayoutBuilder(
          builder: (context, constraints) {
            final columns = constraints.maxWidth < 360 ? 1 : 2;
            final spacing = 10.0;
            final cardWidth =
                (constraints.maxWidth - spacing * (columns - 1)) / columns;
            return Wrap(
              spacing: spacing,
              runSpacing: spacing,
              children: [
                _ReportMetric(
                  width: cardWidth,
                  label: 'Subjects',
                  value: '${subjects.length}',
                  icon: Icons.menu_book_outlined,
                ),
                _ReportMetric(
                  width: cardWidth,
                  label: 'Assessments',
                  value: '$markCount',
                  icon: Icons.assignment_outlined,
                ),
                _ReportMetric(
                  width: cardWidth,
                  label: 'Overall average',
                  value: learner.overallAverage == null
                      ? '—'
                      : _formatPercent(learner.overallAverage!),
                  icon: Icons.trending_up,
                ),
                _ReportMetric(
                  width: cardWidth,
                  label: 'Pass rate',
                  value: markCount == 0 ? '—' : _formatPercent(passRate),
                  icon: Icons.check_circle_outline,
                  accent: colors.primary,
                ),
              ],
            );
          },
        ),
        const SizedBox(height: 22),
        Text(
          'Average by subject',
          style: theme.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.bold,
          ),
        ),
        const SizedBox(height: 10),
        if (subjects.isEmpty)
          const _EmptyMarks()
        else
          LayoutBuilder(
            builder: (context, constraints) {
              final columns = constraints.maxWidth < 520 ? 1 : 2;
              final spacing = 10.0;
              final cardWidth =
                  (constraints.maxWidth - spacing * (columns - 1)) / columns;
              return Wrap(
                spacing: spacing,
                runSpacing: spacing,
                children: subjects
                    .map(
                      (subject) => SizedBox(
                        width: cardWidth,
                        child: _SubjectReportCard(
                          subject: subject,
                          selected: subject.name == selectedSubject.name,
                          onTap: () => onSelectSubject(subject),
                        ),
                      ),
                    )
                    .toList(),
              );
            },
          ),
        if (subjects.isNotEmpty) ...[
          const SizedBox(height: 22),
          Text(
            'Passes and reviews by subject',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 10),
          ...subjects.map((subject) {
            final subjectPasses = subject.marks
                .where((mark) => mark.passed)
                .length;
            final subjectReviews = subject.marks.length - subjectPasses;
            return Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Card(
                child: Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 12,
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          subject.name,
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                      ),
                      _CountBadge(
                        label: '$subjectPasses pass',
                        color: colors.primary,
                      ),
                      const SizedBox(width: 8),
                      _CountBadge(
                        label: '$subjectReviews review',
                        color: colors.error,
                      ),
                    ],
                  ),
                ),
              ),
            );
          }),
          const SizedBox(height: 16),
          Text(
            selectedSubject.name,
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Assessments ordered newest first',
            style: TextStyle(color: colors.onSurfaceVariant),
          ),
          const SizedBox(height: 10),
          _AssessmentTable(marks: selectedSubject.marks),
        ],
      ],
    );
  }
}

class _ReportMetric extends StatelessWidget {
  const _ReportMetric({
    required this.width,
    required this.label,
    required this.value,
    required this.icon,
    this.accent,
  });

  final double width;
  final String label;
  final String value;
  final IconData icon;
  final Color? accent;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final color = accent ?? colors.primary;
    return SizedBox(
      width: width,
      child: Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Icon(icon, color: color),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      value,
                      style: Theme.of(context).textTheme.titleLarge
                          ?.copyWith(fontWeight: FontWeight.bold, color: color),
                    ),
                    Text(
                      label,
                      style: TextStyle(color: colors.onSurfaceVariant),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SubjectReportCard extends StatelessWidget {
  const _SubjectReportCard({
    required this.subject,
    required this.selected,
    required this.onTap,
  });

  final SubjectProgress subject;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final resultColor = subject.passed ? colors.primary : colors.error;
    final comparisonColor = switch (subject.peerComparison) {
      PeerComparison.above => colors.primary,
      PeerComparison.within => colors.tertiary,
      PeerComparison.below => colors.error,
      PeerComparison.unavailable => colors.onSurfaceVariant,
    };
    final comparisonLabel = switch (subject.peerComparison) {
      PeerComparison.above =>
        'Above class average by ${_formatNumber(subject.average - subject.classAverage!)} pp',
      PeerComparison.within => 'Within 5 pp of class average',
      PeerComparison.below =>
        'Below class average by ${_formatNumber(subject.classAverage! - subject.average)} pp',
      PeerComparison.unavailable => 'Not shown (fewer than 3 learners)',
    };
    return Card(
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(
          color: selected ? colors.primary : colors.outlineVariant,
          width: selected ? 2 : 1,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                subject.name,
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
              const SizedBox(height: 6),
              Text(
                'Learner average',
                style: TextStyle(color: colors.onSurfaceVariant, fontSize: 12),
              ),
              const SizedBox(height: 2),
              Text(
                _formatPercent(subject.average),
                style: Theme.of(context).textTheme.headlineSmall
                    ?.copyWith(color: resultColor, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 4),
              if (subject.classAverage != null) ...[
                Text(
                  'Class average: ${_formatPercent(subject.classAverage!)}',
                  style: TextStyle(
                    color: colors.onSurfaceVariant,
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 6),
                _CountBadge(label: comparisonLabel, color: comparisonColor),
              ] else
                Text(
                  comparisonLabel,
                  style: TextStyle(
                    color: comparisonColor,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      '${subject.marks.length} ${subject.marks.length == 1 ? 'assessment' : 'assessments'}',
                      style: TextStyle(
                        color: colors.onSurfaceVariant,
                        fontSize: 12,
                      ),
                    ),
                  ),
                  _CountBadge(
                    label: subject.passed ? 'PASS' : 'REVIEW',
                    color: resultColor,
                  ),
                ],
              ),
              const SizedBox(height: 12),
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: LinearProgressIndicator(
                  value: (subject.average / 100).clamp(0.0, 1.0),
                  minHeight: 7,
                  color: resultColor,
                  backgroundColor: colors.surfaceContainerHighest,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CountBadge extends StatelessWidget {
  const _CountBadge({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        child: Text(
          label,
          style: TextStyle(
            color: color,
            fontSize: 11,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),
    );
  }
}

class _AssessmentTable extends StatelessWidget {
  const _AssessmentTable({required this.marks});

  final List<ParentMark> marks;

  @override
  Widget build(BuildContext context) {
    if (marks.isEmpty) return const _EmptyMarks();
    final colors = Theme.of(context).colorScheme;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          headingRowColor: WidgetStatePropertyAll(
            colors.surfaceContainerHighest.withValues(alpha: 0.65),
          ),
          columns: const [
            DataColumn(label: Text('Assessment')),
            DataColumn(label: Text('Type')),
            DataColumn(label: Text('Score')),
            DataColumn(label: Text('%')),
            DataColumn(label: Text('Result')),
            DataColumn(label: Text('Date captured')),
          ],
          rows: marks.map((mark) {
            final resultColor = mark.passed ? colors.primary : colors.error;
            return DataRow(
              cells: [
                DataCell(Text(mark.assessmentTitle)),
                DataCell(Text(mark.assessmentType)),
                DataCell(
                  Text(
                    '${_formatNumber(mark.score)} / ${_formatNumber(mark.total)}',
                  ),
                ),
                DataCell(Text(_formatPercent(mark.percent))),
                DataCell(
                  _CountBadge(
                    label: mark.passed ? 'PASS' : 'REVIEW',
                    color: resultColor,
                  ),
                ),
                DataCell(
                  Text(
                    DateFormat('dd MMM yyyy')
                        .format(mark.dateRecorded.toLocal()),
                  ),
                ),
              ],
            );
          }).toList(),
        ),
      ),
    );
  }
}

class _LearnerSummary extends StatelessWidget {
  const _LearnerSummary({required this.learner});

  final LearnerProgress learner;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final average = learner.overallAverage;
    final passed = average != null && average >= ParentMark.passPercent;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            CircleAvatar(
              radius: 27,
              backgroundColor: colors.primaryContainer,
              child: Text(
                learner.firstName.isEmpty
                    ? '?'
                    : learner.firstName.substring(0, 1).toUpperCase(),
                style: TextStyle(
                  color: colors.primary,
                  fontWeight: FontWeight.bold,
                  fontSize: 20,
                ),
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    learner.fullName,
                    style: Theme.of(context).textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.bold),
                  ),
                  Text(
                    'Grade ${learner.grade}',
                    style: TextStyle(color: colors.onSurfaceVariant),
                  ),
                ],
              ),
            ),
            if (average == null)
              Text(
                'No marks yet',
                style: TextStyle(color: colors.onSurfaceVariant),
              )
            else
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    _formatPercent(average),
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: passed ? colors.primary : colors.error,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    passed ? 'OVERALL PASS' : 'OVERALL',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: passed ? colors.primary : colors.error,
                      letterSpacing: 0.6,
                    ),
                  ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}

class _EmptyMarks extends StatelessWidget {
  const _EmptyMarks();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(22),
        child: Column(
          children: [
            Icon(Icons.assignment_outlined, size: 36),
            SizedBox(height: 10),
            Text(
              'No marks have been recorded yet.',
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.error, required this.onRetry});

  final Object error;
  final Future<void> Function() onRetry;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.cloud_off_outlined, size: 52, color: colors.error),
            const SizedBox(height: 14),
            Text(
              'We couldn’t load progress',
              style: Theme.of(context).textTheme.titleLarge
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            Text(
              error.toString(),
              textAlign: TextAlign.center,
              style: TextStyle(color: colors.onSurfaceVariant),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Try again'),
            ),
          ],
        ),
      ),
    );
  }
}

String _formatPercent(double value) {
  final formatted = value.toStringAsFixed(1).replaceFirst(RegExp(r'\.0$'), '');
  return '$formatted%';
}

String _formatNumber(double value) {
  return value.toStringAsFixed(1).replaceFirst(RegExp(r'\.0$'), '');
}
