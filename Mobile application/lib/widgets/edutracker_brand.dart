import 'package:flutter/material.dart';

class EduTrackerBrand extends StatelessWidget {
  const EduTrackerBrand({super.key});

  static const tagline = 'The 1st Step To Making Progress Is Tracking It';

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        const CustomPaint(
          size: Size(112, 90),
          painter: _EduTrackerMarkPainter(),
        ),
        const SizedBox(height: 2),
        Text(
          'EduTracker',
          textAlign: TextAlign.center,
          style: TextStyle(
            color: Theme.of(context).colorScheme.primary,
            fontSize: 26,
            fontWeight: FontWeight.w800,
            letterSpacing: -0.8,
          ),
        ),
        const SizedBox(height: 5),
        Text(
          tagline,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodySmall?.copyWith(
            color: Theme.of(context).colorScheme.onSurfaceVariant,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}

class _EduTrackerMarkPainter extends CustomPainter {
  const _EduTrackerMarkPainter();

  static const _indigo = Color(0xFF2821B8);
  static const _orange = Color(0xFFFF8A45);
  static const _page = Color(0xFFCAC8C5);

  @override
  void paint(Canvas canvas, Size size) {
    final sx = size.width / 112;
    final sy = size.height / 90;
    canvas.save();
    canvas.scale(sx, sy);

    final rayPaint = Paint()
      ..color = _orange
      ..strokeWidth = 1.2
      ..strokeCap = StrokeCap.round;
    for (var index = 0; index < 11; index++) {
      final x = 56.0 + (index - 5) * 5.2;
      final height = 15.0 - (index - 5).abs() * 1.2;
      canvas.drawLine(Offset(x, 29), Offset(x, 29 - height), rayPaint);
    }

    final bookPaint = Paint()
      ..color = _page
      ..style = PaintingStyle.stroke
      ..strokeWidth = 4
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;
    final leftPage = Path()
      ..moveTo(55, 59)
      ..cubicTo(47, 53, 38, 51, 22, 48)
      ..lineTo(22, 57)
      ..cubicTo(38, 60, 48, 64, 55, 71);
    final rightPage = Path()
      ..moveTo(57, 59)
      ..cubicTo(65, 53, 74, 51, 90, 48)
      ..lineTo(90, 57)
      ..cubicTo(74, 60, 64, 64, 57, 71);
    canvas.drawPath(leftPage, bookPaint);
    canvas.drawPath(rightPage, bookPaint);
    canvas.drawLine(
      const Offset(56, 59),
      const Offset(56, 82),
      Paint()
        ..color = _page
        ..strokeWidth = 3,
    );

    final capPaint = Paint()
      ..color = _indigo
      ..style = PaintingStyle.fill;
    final cap = Path()
      ..moveTo(20, 34)
      ..lineTo(56, 23)
      ..lineTo(92, 34)
      ..lineTo(56, 46)
      ..close();
    canvas.drawPath(cap, capPaint);

    final mortarboard = Path()
      ..moveTo(33, 39)
      ..lineTo(56, 47)
      ..lineTo(79, 39)
      ..lineTo(79, 53)
      ..lineTo(56, 64)
      ..lineTo(33, 53)
      ..close();
    canvas.drawPath(mortarboard, capPaint);
    canvas.drawCircle(const Offset(56, 34), 2.5, Paint()..color = Colors.white);

    final tasselPaint = Paint()
      ..color = _indigo
      ..strokeWidth = 2
      ..strokeCap = StrokeCap.round;
    canvas.drawLine(const Offset(90, 35), const Offset(94, 48), tasselPaint);
    canvas.drawCircle(const Offset(94, 50), 2.2, Paint()..color = _indigo);

    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _EduTrackerMarkPainter oldDelegate) => false;
}
