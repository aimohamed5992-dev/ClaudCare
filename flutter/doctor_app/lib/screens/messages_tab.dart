import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/messages.dart';
import '../state/app_scope.dart';
import 'chat_screen.dart';

class MessagesTab extends StatefulWidget {
  const MessagesTab({super.key});
  @override
  State<MessagesTab> createState() => _MessagesTabState();
}

class _MessagesTabState extends State<MessagesTab> {
  final _q = TextEditingController();

  @override
  void dispose() {
    _q.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    AppScope.of(context);
    final c = AppScope.storeOf(context).conversations;
    final items = c.search(_q.text);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s4, CcSpace.s5, CcSpace.s3),
          child: Row(children: [
            Expanded(child: Semantics(header: true, child: const Text('الرسائل', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w800)))),
            if (c.unreadTotal > 0) CcBadge('${c.unreadTotal} غير مقروءة', tone: CcTone.orange),
          ]),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: CcSpace.s5),
          child: TextField(
            controller: _q,
            onChanged: (_) => setState(() {}),
            decoration: InputDecoration(
              hintText: 'ابحث في المحادثات',
              prefixIcon: const Icon(Icons.search_rounded, color: CcColors.muted),
              suffixIcon: _q.text.isEmpty ? null : IconButton(tooltip: 'مسح البحث', icon: const Icon(Icons.close_rounded), onPressed: () => setState(_q.clear)),
            ),
          ),
        ),
        const SizedBox(height: CcSpace.s3),
        Expanded(
          child: items.isEmpty
              ? const CcEmptyState(icon: Icons.chat_bubble_outline_rounded, title: 'لا محادثات', text: 'لا نتائج مطابقة لبحثك.')
              : ListView.separated(
                  padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s8),
                  itemCount: items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: CcSpace.s2),
                  itemBuilder: (_, i) => _ConversationTile(conv: items[i]),
                ),
        ),
      ],
    );
  }
}

class _ConversationTile extends StatelessWidget {
  const _ConversationTile({required this.conv});
  final Conversation conv;

  @override
  Widget build(BuildContext context) {
    final unread = conv.unread > 0;
    return Semantics(
      button: true,
      label: '${conv.name}، ${conv.role}، ${unread ? '${conv.unread} رسائل غير مقروءة، ' : ''}آخر رسالة: ${conv.last.text}',
      excludeSemantics: true,
      child: CcCard(
        onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => ChatScreen(conversationId: conv.id))),
        padding: const EdgeInsets.all(CcSpace.s3),
        color: unread ? CcColors.blue50 : Colors.white,
        child: Row(
          children: [
            Stack(
              children: [
                CcAvatar(name: conv.name, asset: conv.avatar, size: 52),
                if (conv.online) Positioned(bottom: 0, left: 0, child: Container(width: 14, height: 14, decoration: BoxDecoration(color: CcColors.green, shape: BoxShape.circle, border: Border.all(color: Colors.white, width: 2)))),
              ],
            ),
            const SizedBox(width: CcSpace.s3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(children: [
                    Expanded(child: Text(conv.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontWeight: unread ? FontWeight.w800 : FontWeight.w700, fontSize: 15))),
                    Text(conv.last.time.replaceAll('أمس ', ''), style: const TextStyle(fontSize: 11, color: CcColors.muted)),
                  ]),
                  Text(conv.role, style: const TextStyle(fontSize: 12, color: CcColors.blue, fontWeight: FontWeight.w600)),
                  const SizedBox(height: 2),
                  Row(children: [
                    Expanded(child: Text(conv.last.fromMe ? 'أنت: ${conv.last.text}' : conv.last.text, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 13, color: unread ? CcColors.slate : CcColors.muted, fontWeight: unread ? FontWeight.w700 : FontWeight.w400))),
                    if (unread) Container(margin: const EdgeInsetsDirectional.only(start: 8), padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 2), decoration: const BoxDecoration(color: CcColors.blue, borderRadius: BorderRadius.all(CcRadius.pill)), child: Text('${conv.unread}', style: const TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.w800))),
                  ]),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
