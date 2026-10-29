import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../data/notifications.dart';
import '../state/app_scope.dart';
import 'chat_screen.dart';
import 'patient_record_screen.dart';

class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});
  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  bool _unreadOnly = false;

  IconData _icon(NotifType t) => switch (t) {
        NotifType.appointment => Icons.event_available_outlined,
        NotifType.result => Icons.science_outlined,
        NotifType.message => Icons.chat_bubble_outline_rounded,
        NotifType.system => Icons.info_outline_rounded,
      };

  void _open(NotifItem n) {
    final store = AppScope.storeOf(context);
    store.notifications.markRead(n.id);
    if (n.patientId != null) {
      Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => PatientRecordScreen(patientId: n.patientId!)));
    } else if (n.conversationId != null) {
      Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => ChatScreen(conversationId: n.conversationId!)));
    } else if (n.type == NotifType.appointment) {
      store.tab.value = 1;
      Navigator.of(context).popUntil((r) => r.isFirst);
    }
  }

  @override
  Widget build(BuildContext context) {
    AppScope.of(context);
    final c = AppScope.storeOf(context).notifications;
    final items = c.all.where((n) => !_unreadOnly || n.unread).toList();
    final groups = <String>[...{for (final n in items) n.group}];

    return Scaffold(
      appBar: AppBar(
        title: const Text('الإشعارات'),
        leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop()),
        actions: [TextButton(onPressed: c.unreadCount == 0 ? null : c.markAllRead, child: const Text('قراءة الكل', style: TextStyle(fontWeight: FontWeight.w700)))],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s2),
            child: Wrap(spacing: 8, runSpacing: 8, children: [
              ChoiceChip(label: const Text('الكل'), selected: !_unreadOnly, onSelected: (_) => setState(() => _unreadOnly = false)),
              ChoiceChip(label: Text('غير المقروءة (${c.unreadCount})'), selected: _unreadOnly, onSelected: (_) => setState(() => _unreadOnly = true)),
            ]),
          ),
          Expanded(
            child: items.isEmpty
                ? CcEmptyState(image: 'assets/images/empty/empty-notifications.webp', title: 'لا إشعارات', text: _unreadOnly ? 'قرأت كل الإشعارات.' : 'ستظهر هنا تنبيهات مواعيدك ونتائج مرضاك.')
                : ListView(
                    padding: const EdgeInsets.fromLTRB(CcSpace.s5, CcSpace.s2, CcSpace.s5, CcSpace.s8),
                    children: [
                      for (final g in groups) ...[
                        Padding(padding: const EdgeInsets.only(top: CcSpace.s3, bottom: CcSpace.s2), child: CcSectionHeader(g)),
                        for (final n in items.where((n) => n.group == g))
                          Padding(
                            padding: const EdgeInsets.only(bottom: CcSpace.s2),
                            child: Dismissible(
                              key: ValueKey('n${n.id}'),
                              background: Container(alignment: AlignmentDirectional.centerStart, padding: const EdgeInsets.symmetric(horizontal: 20), decoration: BoxDecoration(color: CcColors.red50, borderRadius: BorderRadius.circular(CcRadius.lg)), child: const Icon(Icons.delete_outline_rounded, color: CcColors.redText)),
                              onDismissed: (_) {
                                c.remove(n.id);
                                final m = ScaffoldMessenger.of(context);
                                m.hideCurrentSnackBar();
                                m.showSnackBar(SnackBar(content: const Text('حُذف الإشعار'), action: SnackBarAction(label: 'تراجع', textColor: Colors.white, onPressed: c.undoRemove)));
                              },
                              child: Semantics(
                                button: true,
                                label: '${n.unread ? 'غير مقروء، ' : ''}${n.title}، ${n.text}، ${n.time}',
                                excludeSemantics: true,
                                child: CcCard(
                                  onTap: () => _open(n),
                                  padding: const EdgeInsets.all(CcSpace.s3),
                                  color: n.unread ? CcColors.blue50 : Colors.white,
                                  child: Row(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Container(width: 40, height: 40, decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(CcRadius.md), border: Border.all(color: CcColors.line)), child: Icon(_icon(n.type), color: CcColors.blue, size: 22)),
                                      const SizedBox(width: CcSpace.s3),
                                      Expanded(
                                        child: Column(
                                          crossAxisAlignment: CrossAxisAlignment.start,
                                          children: [
                                            Text(n.title, style: TextStyle(fontWeight: n.unread ? FontWeight.w800 : FontWeight.w700)),
                                            const SizedBox(height: 2),
                                            Text(n.text, style: const TextStyle(fontSize: 13, color: CcColors.muted, height: 1.5)),
                                            const SizedBox(height: 4),
                                            Text(n.time, style: const TextStyle(fontSize: 11, color: CcColors.muted)),
                                          ],
                                        ),
                                      ),
                                      if (n.unread) Container(margin: const EdgeInsets.only(top: 6), width: 10, height: 10, decoration: const BoxDecoration(color: CcColors.orange, shape: BoxShape.circle)),
                                    ],
                                  ),
                                ),
                              ),
                            ),
                          ),
                      ],
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}
