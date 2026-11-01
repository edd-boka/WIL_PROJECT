import 'package:flutter_test/flutter_test.dart';
import 'package:parent_mobile/main.dart';

void main() {
  testWidgets('shows setup instructions when Supabase is not configured', (
    tester,
  ) async {
    await tester.pumpWidget(const ConfigurationRequiredApp());

    expect(find.text('Connect your Supabase project'), findsOneWidget);
    expect(find.textContaining('SUPABASE_URL'), findsOneWidget);
  });
}
