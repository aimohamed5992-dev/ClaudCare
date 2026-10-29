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
  Conversation({required this.id, required this.name, required this.role, required this.avatar, required List<Message> messages, this.unread = 0, this.doctorId}) : messages = List.of(messages);
  final int id;
  final String name, role, avatar;
  final List<Message> messages;
  int unread;
  final int? doctorId;
  Message get last => messages.isEmpty ? Message(fromMe: false, text: lastText ?? '', time: lastTime ?? '') : messages.last;
  String? lastText, lastTime;
}

const _a = 'assets/images/avatars/';
const _replies = ['وصلتني رسالتك، سأرد عليك قريبًا.', 'شكرًا لتواصلك، سنراجع الأمر ونوافيك.', 'تمام، يمكنك حجز موعد للمتابعة من التطبيق.'];

class ConversationsController extends ChangeNotifier {
  ConversationsController({List<Conversation>? initial, this.autoReply = false}) : _items = initial ?? _seed();

  List<Conversation> _items;
  final bool autoReply;
  PatientRemote? remote;
  int _clock = demoNowMinutes + 1, _ix = 0;

  void replaceAll(List<Conversation> items) {
    // نحتفظ برسائل المحادثات المحمَّلة سابقًا حتى لا تُفرَّغ عند تحديث القائمة
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
  final _timers = <Timer>[];
  final typing = <int>{};

  static List<Conversation> _seed() => [
        Conversation(id: 1, name: 'د. منى القاسم', role: 'القلب', avatar: '${_a}doc-f1.webp', doctorId: 1, unread: 1, messages: const [
          Message(fromMe: true, text: 'السلام عليكم دكتورة، هل أستمر على نفس جرعة أملوديبين؟', time: 'أمس 06:45 م'),
          Message(fromMe: false, text: 'وعليكم السلام، نعم استمري وسجّلي قراءة الضغط يوميًا، ونراجعها في موعدك.', time: '08:15 ص'),
        ]),
        Conversation(id: 2, name: 'الدعم الفني', role: 'ClaudCare', avatar: '${_a}reception-f1.webp', messages: const [
          Message(fromMe: false, text: 'مرحبًا بك! كيف يمكننا مساعدتك اليوم؟', time: 'قبل أسبوع'),
        ]),
        Conversation(id: 3, name: 'صيدلية المستشفى', role: 'الصيدلية', avatar: '${_a}pharmacist-f1.webp', messages: const [
          Message(fromMe: false, text: 'وصفتك RX-3298 جاهزة للاستلام من الصيدلية.', time: '3 أكتوبر'),
        ]),
      ];

  List<Conversation> get all => List.unmodifiable(_items);
  Conversation byId(int id) => _items.firstWhere((c) => c.id == id);
  int get unreadTotal => _items.fold(0, (s, c) => s + c.unread);

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
        c.messages.add(Message(fromMe: false, text: _replies[_ix++ % _replies.length], time: _now()));
        notifyListeners();
      }));
    }
  }

  /// يعيد محادثة طبيب موجودة أو ينشئها.
  Conversation forDoctor(int doctorId, String name, String role, String avatar) {
    final i = _items.indexWhere((c) => c.doctorId == doctorId);
    if (i >= 0) return _items[i];
    final c = Conversation(id: 100 + doctorId, name: name, role: role, avatar: avatar, doctorId: doctorId, messages: [Message(fromMe: false, text: 'مرحبًا، كيف أستطيع مساعدتك؟', time: _now())]);
    _items.insert(0, c);
    notifyListeners();
    return c;
  }

  @override
  void dispose() {
    for (final t in _timers) {
      t.cancel();
    }
    super.dispose();
  }
}
