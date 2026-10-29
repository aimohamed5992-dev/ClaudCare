import 'dart:async';
import 'package:flutter/foundation.dart';
import 'clock.dart';
import 'remote.dart';
import '../utils/format.dart';

class Message {
  const Message({required this.fromMe, required this.text, required this.time});
  final bool fromMe;
  final String text, time;
}

class Conversation {
  Conversation({required this.id, required this.name, required this.role, required this.avatar, required List<Message> messages, this.unread = 0, this.online = false, this.patientId}) : messages = List.of(messages);
  final int id;
  final String name, role, avatar;
  final List<Message> messages;
  int unread;
  final bool online;
  final int? patientId;

  Message get last => messages.isEmpty ? Message(fromMe: false, text: lastText ?? '', time: lastTime ?? '') : messages.last;
  String? lastText, lastTime;
}

const _a = 'assets/images/avatars/';

List<Conversation> _seed() => [
      Conversation(
        id: 1, name: 'نادين يوسف', role: 'ممرضة – قسم الباطنة', avatar: '${_a}nurse-f1.webp', unread: 2, online: true,
        messages: const [
          Message(fromMe: false, text: 'دكتورة منى، ضغط المريض في غرفة 206 مرتفع (160/100).', time: '08:42 ص'),
          Message(fromMe: true, text: 'سجّلي القراءة مرة أخرى بعد 15 دقيقة وأبلغيني.', time: '08:44 ص'),
          Message(fromMe: false, text: 'تمت إعادة القياس: 150/95. هل أعطيه الدواء الاحتياطي؟', time: '09:02 ص'),
        ],
      ),
      Conversation(
        id: 2, name: 'د. عمر الشامي', role: 'طبيب باطنة', avatar: '${_a}doc-m1.webp', unread: 0,
        messages: const [
          Message(fromMe: true, text: 'دكتور عمر، هل يمكنك مراجعة حالة السيد عبدالله ناصر؟ كرياتينينه مرتفع.', time: 'أمس 04:10 م'),
          Message(fromMe: false, text: 'بالطبع، سأمرّ عليه بعد جولة الصباح غدًا.', time: 'أمس 04:32 م'),
        ],
      ),
      Conversation(
        id: 3, name: 'مختبر المستشفى', role: 'فنيو المختبر', avatar: '${_a}lab-tech.webp', unread: 1, online: true,
        messages: const [Message(fromMe: false, text: 'نتيجة صورة الدم للمريض محمد علي جاهزة وفيها قيم خارج المعدل.', time: '08:10 ص')],
      ),
      Conversation(
        id: 4, name: 'سارة أحمد', role: 'مريضة', avatar: '${_a}patient-2.webp', unread: 0, patientId: 2,
        messages: const [
          Message(fromMe: false, text: 'السلام عليكم دكتورة، هل أستمر على نفس جرعة أملوديبين؟', time: 'أمس 06:45 م'),
          Message(fromMe: true, text: 'وعليكم السلام، نعم استمري وسجّلي قراءة الضغط يوميًا، ونراجعها في موعدك.', time: 'أمس 07:20 م'),
        ],
      ),
      Conversation(
        id: 5, name: 'ص. لمى الشهري', role: 'الصيدلية', avatar: '${_a}pharmacist-f1.webp', unread: 0,
        messages: const [Message(fromMe: false, text: 'تم تغيير بديل أوميبرازول بسبب نفاد الصنف، موافقتك مطلوبة على الوصفة RX-3301.', time: 'السبت 11:05 ص')],
      ),
    ];

const _replies = ['تمام دكتورة، سأتابع ذلك فورًا.', 'وصلتني رسالتك، شكرًا لك.', 'حاضر، وسأوافيك بالتحديث قريبًا.'];

class ConversationsController extends ChangeNotifier {
  ConversationsController({List<Conversation>? initial, this.autoReply = false}) : _items = initial ?? _seed();

  List<Conversation> _items;
  final bool autoReply;
  DoctorRemote? remote;

  void replaceAll(List<Conversation> items) {
    for (final n in items) {
      final old = _items.where((o) => o.id == n.id).firstOrNull;
      if (old != null && n.messages.isEmpty) n.messages.addAll(old.messages);
    }
    _items = List.of(items);
    notifyListeners();
  }

  void setMessages(int id, List<Message> messages) {
    final c = byId(id);
    c.messages
      ..clear()
      ..addAll(messages);
    notifyListeners();
  }
  int _clock = demoNowMinutes + 1;
  int _replyIx = 0;
  final _timers = <Timer>[];
  final typing = <int>{};

  List<Conversation> get all {
    final l = List.of(_items);
    return l;
  }

  Conversation byId(int id) => _items.firstWhere((c) => c.id == id);
  int get unreadTotal => _items.fold(0, (s, c) => s + c.unread);

  List<Conversation> search(String q) {
    final s = q.trim();
    return s.isEmpty ? all : _items.where((c) => c.name.contains(s) || c.role.contains(s) || c.messages.any((m) => m.text.contains(s))).toList();
  }

  void markRead(int id) {
    final c = byId(id);
    if (c.unread == 0) return;
    c.unread = 0;
    notifyListeners();
    remote?.markConversationRead(id);
  }

  String _now() => fmtTime(_clock++);

  void send(int id, String text) {
    final t = text.trim();
    if (t.isEmpty) return;
    final c = byId(id);
    c.messages.add(Message(fromMe: true, text: t, time: remote != null ? fmtTime(nowMinutes) : _now()));
    // المحادثة المُرسَل فيها تصعد للأعلى
    _items
      ..remove(c)
      ..insert(0, c);
    notifyListeners();
    remote?.sendMessage(id, t);
    if (autoReply) {
      typing.add(id);
      notifyListeners();
      _timers.add(Timer(const Duration(milliseconds: 1400), () {
        typing.remove(id);
        c.messages.add(Message(fromMe: false, text: _replies[_replyIx++ % _replies.length], time: _now()));
        notifyListeners();
      }));
    }
  }

  @override
  void dispose() {
    for (final t in _timers) {
      t.cancel();
    }
    super.dispose();
  }
}
