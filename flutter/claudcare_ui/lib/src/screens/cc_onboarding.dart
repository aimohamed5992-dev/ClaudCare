import 'package:flutter/material.dart';
import '../motion.dart';
import '../tokens.dart';
import '../widgets/cc_button.dart';
import '../widgets/cc_page_dots.dart';

class CcOnboardingPage {
  const CcOnboardingPage({required this.image, required this.title, required this.body});
  final String image;
  final String title;
  final String body;
}

/// شاشات التعريف: PageView بسحب طبيعي (RTL) + تخطي + نقاط + زر التالي/ابدأ.
class CcOnboarding extends StatefulWidget {
  const CcOnboarding({
    super.key,
    required this.pages,
    required this.onDone,
    this.skipLabel = 'تخطي',
    this.nextLabel = 'التالي',
    this.doneLabel = 'ابدأ الآن',
  });

  final List<CcOnboardingPage> pages;
  final VoidCallback onDone;
  final String skipLabel;
  final String nextLabel;
  final String doneLabel;

  @override
  State<CcOnboarding> createState() => _CcOnboardingState();
}

class _CcOnboardingState extends State<CcOnboarding> {
  final _controller = PageController();
  int _index = 0;

  bool get _last => _index == widget.pages.length - 1;

  void _next() {
    if (_last) {
      widget.onDone();
    } else {
      _controller.nextPage(duration: ccMotion(context, const Duration(milliseconds: 350)), curve: Curves.easeOutCubic);
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(CcSpace.s4, CcSpace.s2, CcSpace.s4, 0),
              child: Align(
                alignment: AlignmentDirectional.centerEnd,
                child: Visibility(
                  visible: !_last,
                  maintainState: true,
                  maintainAnimation: true,
                  maintainSize: true,
                  child: TextButton(
                    onPressed: widget.onDone,
                    style: TextButton.styleFrom(foregroundColor: CcColors.muted, minimumSize: const Size(64, 48)),
                    child: Text(widget.skipLabel, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15)),
                  ),
                ),
              ),
            ),
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: widget.pages.length,
                onPageChanged: (i) => setState(() => _index = i),
                itemBuilder: (_, i) => _Slide(page: widget.pages[i]),
              ),
            ),
            CcPageDots(count: widget.pages.length, index: _index),
            Padding(
              padding: const EdgeInsets.fromLTRB(CcSpace.s6, CcSpace.s6, CcSpace.s6, CcSpace.s6),
              child: CcButton(label: _last ? widget.doneLabel : widget.nextLabel, icon: _last ? Icons.check_rounded : null, onPressed: _next),
            ),
          ],
        ),
      ),
    );
  }
}

class _Slide extends StatelessWidget {
  const _Slide({required this.page});
  final CcOnboardingPage page;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, box) {
        // على الشاشات القصيرة نصغّر الصورة بدل أن يفيض النص
        final imageSize = (box.maxHeight * 0.6).clamp(160.0, 480.0);
        return SingleChildScrollView(
          physics: const ClampingScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: CcSpace.s6),
          child: ConstrainedBox(
            constraints: BoxConstraints(minHeight: box.maxHeight),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Image.asset(page.image, height: imageSize, fit: BoxFit.contain, excludeFromSemantics: true),
                const SizedBox(height: CcSpace.s6),
                Semantics(
                  header: true,
                  child: Text(page.title, textAlign: TextAlign.center, style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800, height: 1.3)),
                ),
                const SizedBox(height: CcSpace.s3),
                Text(page.body, textAlign: TextAlign.center, style: const TextStyle(fontSize: 16, color: CcColors.muted, height: 1.7)),
              ],
            ),
          ),
        );
      },
    );
  }
}
