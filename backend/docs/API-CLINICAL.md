# السريري: الكشف والوصفات والتحاليل والأشعة والأدوية

## الطبيب
| الطلب | الوصف |
|---|---|
| `GET /api/patients?q=&filter=chronic\|kids\|seniors\|allergy&page=` | مرضاك (من لهم موعد غير ملغى أو كشف معك) |
| `GET /api/patients/{id}/record` | نظرة عامة: حساسية، أمراض، آخر علامات حيوية، الأدوية الحالية، عدّادات |
| `GET /api/patients/{id}/visits \| prescriptions \| lab-orders \| imaging-orders` (+`/{orderId}`) | القوائم (مصفّحة) |
| `PUT /api/patients/{id}/clinical-profile` | فصيلة الدم والقياسات؛ قوائم الحساسية والأمراض **تُستبدل** إن أُرسلت (طبيب/تمريض/طوارئ) |
| `POST /api/visits` | **حفظ الكشف كاملًا** (انظر أدناه) |
| `POST /api/prescriptions/check {patientId, drugIds[]}` | فحص مسبق لتعارض الحساسية والتكرار |
| `GET /api/refill-requests` · `POST …/{medicationId}/approve {days?}` · `/reject {reason}` | طلبات إعادة الصرف من مرضاك |
| `GET /api/lab-orders` · `/api/imaging-orders` (+`/{id}`) | ما طلبتَه من فحوصات |
| `GET /api/catalog/lab-tests` · `/imaging-studies` · `GET /api/drugs?q=` | الأدلة (التحاليل بمعدلاتها الطبيعية) |

### `POST /api/visits`
```json
{ "appointmentId": 12,
  "complaint": "ألم في الصدر",
  "vitals": { "systolicBp": 135, "diastolicBp": 85, "heartRate": 78, "temperatureC": 36.8, "spo2": 98 },
  "diagnosis": "ارتفاع ضغط الدم", "notes": "…", "followUp": "بعد أسبوعين",
  "labTests": ["hba1c", "kidney"], "imagingStudies": ["chest-xray"],
  "prescription": [{ "drugId": 6, "dose": "قرص", "frequency": "مرتين يوميًا", "timesPerDay": 2, "durationDays": 30, "instructions": "بعد الأكل", "allergyOverride": false }] }
```
- مع `appointmentId` يجب أن يكون الموعد «قيد الكشف» (ابدأه بـ `/start`)، ويُكمَل تلقائيًا. بدونه: `patientId` لمريض في رعايتك.
- **معاملة واحدة**: الزيارة + طلبات الفحوص + الوصفة + أدوية المريض + إكمال الموعد؛ أي فشل لا يحفظ شيئًا.
- **تعارض الحساسية** ⇒ `409 allergy_conflict` مع `conflicts: [{index, drug, allergy}]` ما لم يُرسَل `allergyOverride: true` لذلك الدواء (يُسجَّل ويُنبَّه في `warnings`).
- التنبيهات غير المانعة (`warnings`): دواء خارج الدليل (لم يُفحص)، دواء مكرر مع أدوية المريض الحالية.
- العلامات: الضغطان معًا والانقباضي > الانبساطي؛ النطاقات: انقباضي 60–260، انبساطي 30–160، نبض 30–220، حرارة 34–42، تشبع 70–100.
- يُنشئ أرقامًا `RX-…` و`LAB-…` و`IMG-…` ويُبلَّغ المريض.

## المريض (`/api/me/*`)
| الطلب | الوصف |
|---|---|
| `health-summary` · `visits` · `prescriptions` | الملف الصحي والزيارات والوصفات |
| `lab-results` (+`/{id}`) | الطلب غير الجاهز يظهر بحالته **دون قيم** (`ready:false`)؛ الجاهز بقيمه وأعلامه (`Normal/High/Low/Critical`) |
| `imaging` (+`/{id}`) | التقرير يظهر عند اعتماده (Final) فقط |
| `medications?active=true\|false` | الأدوية: مواعيد الجرعات، الأيام المتبقية، `endingSoon` (≤5 أيام)، التذكير، حالة إعادة الصرف |
| `medications/today` | جرعات اليوم (`Taken/Missed/Upcoming`) ونسبة الالتزام |
| `POST medications/{id}/doses {slotIndex, taken, date?}` | تأكيد/إلغاء جرعة (اليوم أو أمس) — متكرر الأثر |
| `PATCH medications/{id} {reminderEnabled}` | التذكير |
| `POST medications/{id}/refill` | طلب إعادة صرف (`refill_pending` إن كان معلّقًا، `medication_ended` إن انتهى) |

## الصلاحيات
- المريض: نفسه فقط. الطبيب: مرضاه. تمريض/طوارئ/تنويم/عمليات: سريريًا لكل المرضى. استقبال/إدارة: بيانات تعريف فقط (بلا أمراض أو حساسية). مختبر/أشعة/صيدلية: طلباتهم فقط.
- مريض خارج نطاقك ⇒ `404` (لا نكشف وجوده).

## قواعد
- جدول الجرعات: 1→08:00 · 2→08:00،20:00 · 3→08:00،14:00،20:00 · 4→06،12،18،22 · 0 (عند الحاجة) بلا جدول.
- تصنيف نتيجة: **حرج** إن قلّت عن 60% من الحد الأدنى أو زادت عن 160% من الأعلى، وإلا **منخفض/مرتفع** خارج المعدل.
- اعتماد إعادة الصرف يمدّد المدة بقدر `days` أو بمدة الوصفة الأصلية.
