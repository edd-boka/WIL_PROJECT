import 'dart:async';

import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:supabase_flutter/supabase_flutter.dart';

import 'screens/auth_screen.dart';
import 'screens/parent_dashboard.dart';

const _supabaseUrl = 'https://ebueyaqzcnzavbjkbnsb.supabase.co';
const _supabaseKey = 'sb_publishable_mtyNT8cldnHXJafDeTSnWg_ieZ642Wb';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  if (_supabaseUrl.isEmpty || _supabaseKey.isEmpty) {
    runApp(const ConfigurationRequiredApp());
    return;
  }

  await Supabase.initialize(url: _supabaseUrl, publishableKey: _supabaseKey);
  runApp(ParentApp(client: Supabase.instance.client));
}

class ParentApp extends StatefulWidget {
  const ParentApp({super.key, required this.client});

  final SupabaseClient client;

  @override
  State<ParentApp> createState() => _ParentAppState();
}

class _ParentAppState extends State<ParentApp> {
  static const _themePreferenceKey = 'parent_mobile_dark_theme';
  ThemeMode _themeMode = ThemeMode.light;

  @override
  void initState() {
    super.initState();
    _loadThemePreference();
  }

  Future<void> _loadThemePreference() async {
    final preferences = await SharedPreferences.getInstance();
    final isDark = preferences.getBool(_themePreferenceKey) ?? false;
    if (mounted) {
      setState(() => _themeMode = isDark ? ThemeMode.dark : ThemeMode.light);
    }
  }

  Future<void> _toggleTheme() async {
    final isDark = _themeMode != ThemeMode.dark;
    setState(() => _themeMode = isDark ? ThemeMode.dark : ThemeMode.light);
    final preferences = await SharedPreferences.getInstance();
    await preferences.setBool(_themePreferenceKey, isDark);
  }

  @override
  Widget build(BuildContext context) {
    const seed = Color(0xFF176B63);
    return MaterialApp(
      title: 'EduTracker Mobile App',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(seedColor: seed),
        scaffoldBackgroundColor: const Color(0xFFF5F7F6),
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFFF5F7F6),
          surfaceTintColor: Colors.transparent,
        ),
        cardTheme: CardThemeData(
          color: Colors.white,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(20),
            side: const BorderSide(color: Color(0xFFE6ECE9)),
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: const Color(0xFFF8FAF9),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: const BorderSide(color: Color(0xFFE1E8E4)),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: const BorderSide(color: Color(0xFFE1E8E4)),
          ),
        ),
      ),
      darkTheme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF67B8FF),
          brightness: Brightness.dark,
          surface: const Color(0xFF101D30),
        ),
        scaffoldBackgroundColor: const Color(0xFF0B1728),
        appBarTheme: const AppBarTheme(
          backgroundColor: Color(0xFF0B1728),
          surfaceTintColor: Colors.transparent,
        ),
        cardTheme: CardThemeData(
          color: const Color(0xFF12233A),
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(20),
            side: const BorderSide(color: Color(0xFF2A4260)),
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: const Color(0xFF12233A),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: const BorderSide(color: Color(0xFF2A4260)),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: const BorderSide(color: Color(0xFF2A4260)),
          ),
        ),
      ),
      themeMode: _themeMode,
      home: _AuthGate(
        client: widget.client,
        isDarkTheme: _themeMode == ThemeMode.dark,
        onToggleTheme: _toggleTheme,
      ),
    );
  }
}

class _AuthGate extends StatefulWidget {
  const _AuthGate({
    required this.client,
    required this.isDarkTheme,
    required this.onToggleTheme,
  });

  final SupabaseClient client;
  final bool isDarkTheme;
  final Future<void> Function() onToggleTheme;

  @override
  State<_AuthGate> createState() => _AuthGateState();
}

class _AuthGateState extends State<_AuthGate> {
  late Session? _session = widget.client.auth.currentSession;
  late final StreamSubscription<AuthState> _subscription;

  @override
  void initState() {
    super.initState();
    _subscription = widget.client.auth.onAuthStateChange.listen((state) {
      if (mounted) setState(() => _session = state.session);
    });
  }

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_session == null) return AuthScreen(client: widget.client);

    return ParentDashboard(
      client: widget.client,
      email: widget.client.auth.currentUser?.email ?? '',
      isDarkTheme: widget.isDarkTheme,
      onToggleTheme: widget.onToggleTheme,
    );
  }
}

class ConfigurationRequiredApp extends StatelessWidget {
  const ConfigurationRequiredApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'EduTracker Mobile App',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF176B63)),
      ),
      home: Scaffold(
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(28),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: const Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.settings_suggest_outlined, size: 56),
                  SizedBox(height: 18),
                  Text(
                    'Connect your Supabase project',
                    style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
                    textAlign: TextAlign.center,
                  ),
                  SizedBox(height: 12),
                  Text(
                    'Run this app with SUPABASE_URL and SUPABASE_ANON_KEY '
                    'configured. See the parent_mobile README for setup.',
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
