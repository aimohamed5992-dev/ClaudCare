import 'package:claudcare_ui/claudcare_ui.dart';
import 'package:flutter/material.dart';
import '../state/app_scope.dart';
import 'patient_record_screen.dart';

class ChatScreen extends StatefulWidget {
  const ChatScreen({super.key, required this.conversationId});
  final int conversationId;

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> {
  final _text = TextEditingController();
  final _scroll = ScrollController();
  int _lastCount = 0;

  @override
  void initState() {
    super.initState();
    _text.addListener(() => setState(() {}));
    // تعليم المحادثة كمقروءة بعد أول بناء
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final store = AppScope.storeOf(context);
      store.remote?.loadMessages(widget.conversationId);
      store.conversations.markRead(widget.conversationId);
    });
  }

  @override
  void dispose() {
    _text.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _toBottom() => WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_scroll.hasClients) _scroll.animateTo(_scroll.position.maxScrollExtent, duration: ccMotion(context, const Duration(milliseconds: 250)), curve: Curves.easeOut);
      });

  void _send() {
    final store = AppScope.storeOf(context);
    store.conversations.send(widget.conversationId, _text.text);
    _text.clear();
    _toBottom();
  }

  @override
  Widget build(BuildContext context) {
    final store = AppScope.storeOf(context);
    AppScope.of(context);
    final conv = store.conversations.byId(widget.conversationId);
    final typing = store.conversations.typing.contains(conv.id);
    if (conv.messages.length != _lastCount) {
      _lastCount = conv.messages.length;
      _toBottom();
    }

    return Scaffold(
      appBar: AppBar(
        centerTitle: false,
        leading: IconButton(tooltip: 'رجوع', icon: const Icon(Icons.arrow_forward_rounded), onPressed: () => Navigator.of(context).maybePop()),
        title: Row(children: [
          CcAvatar(name: conv.name, asset: conv.avatar, size: 38),
          const SizedBox(width: CcSpace.s3),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(conv.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800)),
              Text(typing ? 'يكتب الآن…' : (conv.online ? 'متصل' : conv.role), style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: typing || conv.online ? CcColors.greenText : CcColors.muted)),
            ]),
          ),
        ]),
        actions: [
          if (conv.patientId != null)
            IconButton(
              tooltip: 'الملف الطبي',
              icon: const Icon(Icons.folder_open_rounded),
              onPressed: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => PatientRecordScreen(patientId: conv.patientId!))),
            ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: ListView.builder(
              controller: _scroll,
              padding: const EdgeInsets.fromLTRB(CcSpace.s4, CcSpace.s4, CcSpace.s4, CcSpace.s2),
              itemCount: conv.messages.length + (typing ? 1 : 0),
              itemBuilder: (_, i) {
                if (i == conv.messages.length) return const _Bubble(text: '…', time: '', mine: false);
                final m = conv.messages[i];
                return _Bubble(text: m.text, time: m.time, mine: m.fromMe);
              },
            ),
          ),
          SafeArea(
            top: false,
            child: Container(
              padding: const EdgeInsets.fromLTRB(CcSpace.s3, CcSpace.s2, CcSpace.s3, CcSpace.s2),
              decoration: const BoxDecoration(border: Border(top: BorderSide(color: CcColors.line))),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  IconButton(
                    tooltip: 'إرفاق',
                    icon: const Icon(Icons.attach_file_rounded, color: CcColors.muted),
                    onPressed: () => ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('الإرفاق غير متاح في النسخة التجريبية'))),
                  ),
                  Expanded(
                    child: TextField(
                      controller: _text,
                      minLines: 1,
                      maxLines: 4,
                      textInputAction: TextInputAction.newline,
                      decoration: const InputDecoration(hintText: 'اكتب رسالة…', contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 10)),
                    ),
                  ),
                  const SizedBox(width: CcSpace.s2),
                  IconButton.filled(
                    tooltip: 'إرسال',
                    onPressed: _text.text.trim().isEmpty ? null : _send,
                    style: IconButton.styleFrom(backgroundColor: CcColors.blue, disabledBackgroundColor: CcColors.line, minimumSize: const Size(48, 48)),
                    icon: const Icon(Icons.send_rounded, color: Colors.white),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble({required this.text, required this.time, required this.mine});
  final String text, time;
  final bool mine;

  @override
  Widget build(BuildContext context) {
    final w = MediaQuery.sizeOf(context).width * 0.78;
    return Align(
      alignment: mine ? AlignmentDirectional.centerStart : AlignmentDirectional.centerEnd,
      child: Semantics(
        label: '${mine ? 'أنت' : 'هو'}: $text${time.isEmpty ? '' : '، $time'}',
        excludeSemantics: true,
        child: Container(
          constraints: BoxConstraints(maxWidth: w),
          margin: const EdgeInsets.only(bottom: CcSpace.s2),
          padding: const EdgeInsets.fromLTRB(14, 10, 14, 8),
          decoration: BoxDecoration(
            color: mine ? CcColors.blue : CcColors.blue50,
            borderRadius: BorderRadiusDirectional.only(
              topStart: const Radius.circular(18),
              topEnd: const Radius.circular(18),
              bottomStart: Radius.circular(mine ? 4 : 18),
              bottomEnd: Radius.circular(mine ? 18 : 4),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(text, style: TextStyle(color: mine ? Colors.white : CcColors.slate, height: 1.5, fontSize: 15)),
              if (time.isNotEmpty) ...[const SizedBox(height: 2), Text(time, style: TextStyle(fontSize: 11, color: mine ? const Color(0xFFCFE3FF) : CcColors.muted))],
            ],
          ),
        ),
      ),
    );
  }
}
