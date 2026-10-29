import 'package:flutter/material.dart';
import '../tokens.dart';

/// صورة دائرية؛ عند غياب الصورة أو فشل تحميلها تظهر الحروف الأولى.
class CcAvatar extends StatelessWidget {
  const CcAvatar({super.key, required this.name, this.asset, this.size = 48, this.package});

  final String name;
  final String? asset;
  final double size;
  final String? package;

  String get _initials {
    final parts = name.replaceAll(RegExp(r'^(د\.|أ\.|ص\.)\s*'), '').trim().split(RegExp(r'\s+'));
    // نتجاهل «ال» التعريف: القاسم ← ق
    return parts.take(2).map((p) => p.length > 3 && p.startsWith('ال') ? p.substring(2) : p).map((p) => p.isEmpty ? '' : p.characters.first).join();
  }

  @override
  Widget build(BuildContext context) {
    final fallback = Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: const BoxDecoration(color: CcColors.blue50, shape: BoxShape.circle),
      child: Text(_initials, style: TextStyle(color: CcColors.blue, fontWeight: FontWeight.w800, fontSize: size * 0.34)),
    );
    if (asset == null) return Semantics(label: name, child: ExcludeSemantics(child: fallback));
    return Semantics(
      label: name,
      image: true,
      child: ExcludeSemantics(
        child: ClipOval(
          child: Image.asset(asset!, package: package, width: size, height: size, fit: BoxFit.cover, errorBuilder: (_, __, ___) => fallback),
        ),
      ),
    );
  }
}
