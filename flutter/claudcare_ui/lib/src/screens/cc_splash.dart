import 'dart:async';
import 'package:flutter/material.dart';
import '../motion.dart';
import '../tokens.dart';
import '../widgets/cc_badge.dart';

/// شاشة البداية: الشعار يظهر بتكبير ناعم مع موجات هادئة، ثم الشعار النصي والشارة.
/// تستدعي [onFinished] بعد [duration] (لا تستدعيها إن أُزيلت الشاشة قبل ذلك).
class CcSplash extends StatefulWidget {
  const CcSplash({
    super.key,
    required this.logoAsset,
    required this.onFinished,
    this.tagline = 'رعاية أذكى لكل مريض',
    this.badge,
    this.duration = const Duration(milliseconds: 2600),
  });

  final String logoAsset;
  final VoidCallback onFinished;
  final String tagline;
  final String? badge;
  final Duration duration;

  @override
  State<CcSplash> createState() => _CcSplashState();
}

class _CcSplashState extends State<CcSplash> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(milliseconds: 1800));
  Timer? _timer;

  late final Animation<double> _logoScale = Tween(begin: 0.78, end: 1.0).animate(CurvedAnimation(parent: _c, curve: const Interval(0, 0.45, curve: Curves.easeOutBack)));
  late final Animation<double> _logoFade = CurvedAnimation(parent: _c, curve: const Interval(0, 0.35, curve: Curves.easeOut));
  late final Animation<double> _textFade = CurvedAnimation(parent: _c, curve: const Interval(0.45, 0.8, curve: Curves.easeOut));
  late final Animation<Offset> _textSlide = Tween(begin: const Offset(0, 0.4), end: Offset.zero).animate(CurvedAnimation(parent: _c, curve: const Interval(0.45, 0.8, curve: Curves.easeOut)));

  @override
  void initState() {
    super.initState();
    _timer = Timer(widget.duration, widget.onFinished);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_c.isAnimating && !_c.isCompleted) {
      final reduce = MediaQuery.maybeDisableAnimationsOf(context) == true;
      if (reduce) {
        _c.value = 1;
      } else {
        _c.forward();
      }
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final reduce = ccMotion(context, const Duration(seconds: 1)) == Duration.zero;
    return Scaffold(
      backgroundColor: Colors.white,
      body: Stack(
        children: [
          if (!reduce) const Positioned.fill(child: _Ripples()),
          Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                FadeTransition(
                  opacity: _logoFade,
                  child: ScaleTransition(
                    scale: _logoScale,
                    child: Image.asset(widget.logoAsset, width: 200, semanticLabel: 'ClaudCare'),
                  ),
                ),
                const SizedBox(height: CcSpace.s5),
                FadeTransition(
                  opacity: _textFade,
                  child: SlideTransition(
                    position: _textSlide,
                    child: Column(
                      children: [
                        Text(widget.tagline, style: const TextStyle(color: CcColors.muted, fontSize: 16, fontWeight: FontWeight.w600)),
                        if (widget.badge != null) ...[const SizedBox(height: CcSpace.s3), CcBadge(widget.badge!)],
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
          Positioned(
            bottom: 48,
            left: 0,
            right: 0,
            child: FadeTransition(
              opacity: _textFade,
              child: const Center(
                child: SizedBox(width: 26, height: 26, child: CircularProgressIndicator(strokeWidth: 2.6, color: CcColors.sky)),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// دوائر خفيفة تتسع وتتلاشى خلف الشعار.
class _Ripples extends StatefulWidget {
  const _Ripples();
  @override
  State<_Ripples> createState() => _RipplesState();
}

class _RipplesState extends State<_Ripples> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(vsync: this, duration: const Duration(seconds: 3))..repeat();
  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: ExcludeSemantics(
        child: AnimatedBuilder(animation: _c, builder: (_, __) => CustomPaint(painter: _RipplePainter(_c.value))),
      ),
    );
  }
}

class _RipplePainter extends CustomPainter {
  _RipplePainter(this.t);
  final double t;

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2 - 20);
    final maxR = size.shortestSide * 0.75;
    for (var i = 0; i < 3; i++) {
      final p = (t + i / 3) % 1;
      final paint = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5
        ..color = CcColors.sky.withValues(alpha: (1 - p) * 0.18);
      canvas.drawCircle(center, 90 + (maxR - 90) * p, paint);
    }
  }

  @override
  bool shouldRepaint(_RipplePainter old) => old.t != t;
}
