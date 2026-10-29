import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/messages.dart';
import '../state/app_scope.dart';
import 'chat_screen.dart';

class MessagesScreen extends StatelessWidget {
  const MessagesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final c = AppScope.storeOf(context).conversations;
    final items = c.all;
    return Scaffold(
      appBar: AppBar(title: const Text('الرسائل'), leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop())),
      body: items.isEmpty
          ? const CcEmptyState(icon: Icons.chat_bubble_outline_rounded, title: 'لا محادثات')
          : ListView.separated(
              padding: const EdgeInsets.all(CcSpace.s5),
              itemCount: items.length,
              separatorBuilder: (_, __) => const SizedBox(height: CcSpace.s2),
              itemBuilder: (_, i) => _Tile(conv: items[i]),
            ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.conv});
  final Conversation conv;

  @override
  Widget build(BuildContext context) {
    final unread = conv.unread > 0;
    return Semantics(
      button: true,
      label: '${conv.name}، ${conv.role}، ${unread ? '${conv.unread} رسالة غير مقروءة، ' : ''}آخر رسالة: ${conv.last.text}',
      excludeSemantics: true,
      child: CcCard(
        onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => ChatScreen(conversationId: conv.id))),
        padding: const EdgeInsets.all(CcSpace.s3),
        color: unread ? CcColors.blue50 : Colors.white,
        child: Row(children: [
          CcAvatar(name: conv.name, asset: conv.avatar, size: 52),
          const SizedBox(width: CcSpace.s3),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [Expanded(child: Text(conv.name, style: TextStyle(fontWeight: unread ? FontWeight.w800 : FontWeight.w700, fontSize: 15))), Text(conv.last.time.replaceAll('أمس ', ''), style: const TextStyle(fontSize: 11, color: CcColors.muted))]),
            Text(conv.role, style: const TextStyle(fontSize: 12, color: CcColors.blue, fontWeight: FontWeight.w600)),
            const SizedBox(height: 2),
            Row(children: [
              Expanded(child: Text(conv.last.fromMe ? 'أنت: ${conv.last.text}' : conv.last.text, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 13, color: unread ? CcColors.slate : CcColors.muted, fontWeight: unread ? FontWeight.w700 : FontWeight.w400))),
              if (unread) Container(margin: const EdgeInsetsDirectional.only(start: 8), padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2), decoration: const BoxDecoration(color: CcColors.blue, borderRadius: BorderRadius.all(CcRadius.pill)), child: Text('${conv.unread}', style: const TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.w800))),
            ]),
          ])),
        ]),
      ),
    );
  }
}
