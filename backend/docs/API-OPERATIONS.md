# واجهات أدوار التشغيل (الخطوة 7)

كل المسارات تتطلب توكن وصول. **قاعدة النطاق:** كل دور تشغيلي يعمل داخل **فرعه** فقط (سجل في فرع آخر ⇒ `404`)،
ومدير النظام ومدير المستشفى يدخلان كل الفروع للإشراف. دور غير مسموح ⇒ `403 action_not_allowed`.
العمليات الحرجة (الصرف، السرير، القرارات، الدفعات) محمية من التزامن: تحديث شرطي ذري أو قفل صف داخل معاملة.

## المختبر `/api/lab-orders` (القراءة في `GET /api/lab-orders`)
`Ordered ─collect→ SampleCollected ─start→ InProgress ─results→ ResultReady` · `cancel` قبل النتيجة.
- `POST /{id}/collect` (المختبر/التمريض) · `POST /{id}/start` · `PUT /{id}/results` `{items:[{name,value,unit?,refMin?,refMax?,remark?}]}`
- الوحدة والمدى المرجعي من الكتالوج إن لم تُرسل، والعلم (`Normal/High/Low/Critical`) **يحسبه الخادم**. بند مكرر أو مدى معكوس ⇒ `400`.
- النتيجة تُشعر المريض والطبيب؛ **الحرجة** تنبّه الطبيب بإشعار `Alert`.
- `POST /{id}/cancel` `{reason}` (المختبر أو الطبيب الطالب) قبل جاهزية النتيجة.

## الأشعة `/api` 
`Ordered → Scheduled → InProgress → Reported`.
- `GET /imaging-devices` · `POST /imaging-orders/{id}/schedule` `{scheduledAt, deviceId?}` — الجهاز من الفرع، مطابق لنوع الفحص، متاح، بلا تداخل 30 دقيقة (`device_busy`).
- `POST /imaging-orders/{id}/start` · `PUT /imaging-orders/{id}/report` `{findings, impression, imageKey?, final}`: مسودة قابلة للتعديل (لا يراها المريض ولا الطبيب)، و`final=true` ⇒ `Reported` ونهائي (`report_final`) مع إشعار المريض والطبيب.

## الصيدلية `/api/pharmacy`
- `GET /prescriptions?status` (الافتراضي بانتظار/قيد التجهيز، الأعلى أولوية فالأقدم) مع `inStock` لكل صنف.
- `POST /prescriptions/{id}/prepare | reject {reason} | dispense {method}`: الصرف يخصم المخزون ويسجل عملية بيع `SAL-…` داخل معاملة بقفل صفوف المخزون؛ نقص ⇒ `409 insufficient_stock` بلا أي تغيير؛ لا صرف مرتين.
- المخزون: `GET/POST /inventory` · `PUT /inventory/{id}` · `POST /inventory/{id}/adjust {delta,reason}` (لا تنزل تحت الصفر) · `lowOnly`.
- البيع المباشر: `POST /sales` (يدمج البنود المكررة ويسعّر من المخزون) · `GET /sales` · `GET /sales/{id}`.
- أوامر الشراء: `POST /purchase-orders` ⇒ `send` ⇒ `receive {lines:[{lineId,quantity}]}` استلام جزئي/كلي يزيد المخزون وينشئ الصنف إن غاب.

## التمريض `/api`
- `POST /vitals` · `GET /patients/{id}/vitals`: يُرجع `alerts` للقيم الحرجة (تشبع، نبض، ضغط، حرارة، تنفس). الانبساطي < الانقباضي.
- `POST|GET /patients/{id}/nursing-notes` (للطاقم فقط).
- سجل الأدوية: `POST|GET /admissions/{id}/medications` · `POST /medication-administrations/{id}/give | skip | delay` (السبب إلزامي للتخطي/التأجيل، ولا تعديل بعد الإعطاء/التخطي).

## التنويم `/api`
- الأسرّة: `GET /beds?ward&status` (مع العدادات) · `POST /beds/{id}/status` (نظافة/متاح/محجوز؛ «مشغول» بالتنويم فقط).
- `POST /admissions` ⇒ `PUT /admissions/{id}/checks {guardianConsent?, insuranceApprovalVerified?}` ⇒ `POST /admissions/{id}/admit {bedId}`.
  - القاصر (<18) يتطلب موافقة ولي الأمر (`guardian_consent_required`) والمخطط يتطلب التحقق من التأمين (`insurance_approval_required`).
  - السرير يُحجز ذريًا: مشغول ⇒ `409 bed_unavailable`، فلا يُخصَّص لمريضين.
- الخروج `POST /admissions/{id}/discharge`: الخطوات الأربع كلها وإلا `discharge_incomplete` (مع `missing`)، وفواتير التنويم مسددة فعليًا وإلا `bill_unsettled`. ينقل السرير إلى «نظافة».
- النقل: `POST /admissions/{id}/transfers` ⇒ `POST /bed-transfers/{id}/approve` ⇒ `complete {bedId}` (القسم المطلوب، السرير القديم يصبح «نظافة»).

## الطوارئ `/api/emergency`
- `POST /cases` (الفرز 1 حرج–5): إن دلّت العلامات (تشبع<90، ضغط<90، نبض>150/<40، تنفس…، أو علامات خطر) يرفع الخادم المستوى إلى 2 على الأكثر (`escalated`) ولا يخفضه أبدًا.
- `GET /cases` لوحة مرتبة بالأخطر فالأقدم مع `waitingMinutes` وعدّادات المستويات · `PUT /cases/{id}/triage` · `start-exam` · `discharge` · `admit {ward}` (يشترط مريضًا مسجلًا؛ ينشئ طلب تنويم طارئ).
- الإسعاف: `GET|POST /ambulances` · `POST /ambulances/{id}/arrive` ينشئ حالة بانتظار الفحص بمستوى الإسعاف.

## الاستقبال `/api/reception`
- `GET /today` (مواعيد اليوم حسب الحالة + الطابور) · `POST|GET /queue` تذاكر مرقمة لكل فرع/يوم حتى مع الإصدار المتزامن · `call | done | cancel` (الطبيب يرى ويُنادي تذاكره فقط).
- `POST /patients`: تسجيل زائر (جوال مطبّع، تكرار ⇒ `phone_taken`/`national_id_taken`)، حساب بلا كلمة مرور قابلة للتخمين ويفعّله بـ«نسيت كلمة المرور».

## العمليات `/api/surgery`
- `GET /rooms` · `POST /rooms/{id}/state` · `GET /cases?date&status` · `POST /cases` (تعارض غرفة/جراح/تخدير ⇒ `room_busy`/`surgeon_busy`/`anesthetist_busy`، غرفة صيانة، تاريخ ماضٍ).
- قائمة ما قبل العملية (8 بنود): `PUT /cases/{id}/checklist/{key} {done}` — إلغاء بند بعد الاعتماد يُسقط الاعتماد.
- `POST /cases/{id}/preop/approve {acknowledgeAlert}`: القائمة كاملة (`checklist_incomplete`) وإقرار التنبيه (`alert_not_acknowledged`).
- `start` (معتمدة، يوم العملية، غرفة متاحة بحجز ذري ⇒ الغرفة «قيد العملية») · `complete` (الغرفة «نظافة») · `cancel` (المجدولة فقط).
- الجراح/التخدير يرون حالاتهم فقط.

## الموارد البشرية `/api/hr`
- الموظفون: `GET|POST /employees` · `PUT /employees/{id}` · `POST /employees/{id}/status` — **إنهاء الخدمة يعطّل الحساب ويلغي الجلسات فورًا**.
- الإجازات: `POST /leaves` (الموظف لنفسه، والموارد البشرية لأي موظف) — تداخل `leave_overlap`، رصيد `insufficient_balance`. `approve | reject {reason}`: السنوية تخصم الرصيد **مرة واحدة** حتى مع الاعتماد المتزامن.
- المناوبات: `GET|POST /shifts` · `DELETE` · `request-swap` (الموظف) · `confirm` (الموارد البشرية) — تكرار `shift_exists`، وإجازة معتمدة `on_leave`.
- `GET /licenses/expiring?days` مرتبة بالأقرب مع `daysLeft`.

## المالية `/api/finance`
- المصروفات: `POST|GET /expenses` · `approve` (**فوق 10,000 ر.س للمستشفى/النظام**: `approval_limit_exceeded`) · `reject {reason}` · `pay` (المعتمد فقط).
- الرواتب: `POST /payroll/generate {period:"yyyy-MM"}` (بلا تكرار، يستثني منتهي الخدمة) · `GET /payroll?period` · `PUT /payroll/{id}` (المسودة فقط، الصافي ≥ 0) · `submit` · `pay` (المالية فقط).
- الإقفال اليومي: `GET /daily-close/preview?date` (المتوقع من **الدفعات الفعلية** نقدًا/بطاقة + التأمين) · `POST /daily-close` — فرق بلا سبب `400`، مرة واحدة لليوم `already_closed`، لا يوم مستقبلي.
- `GET /summary?from&to`: الإيراد، المستردات، المصروفات، الرواتب، الصافي، المستحق على المرضى، المستحق من التأمين.

## التأمين `/api/insurance`
- `GET /companies` · `GET /policies?patientId` (الاستقبال/التنويم/التأمين/المالية).
- الموافقات: `POST /pre-auths` (يشترط وثيقة سارية) · `GET` (الطبيب طلباته فقط) · `POST /{id}/documents/{docId}/receive` · `POST /{id}/decide {decision, approvedAmount?, rejectReason?}`: `Approved` بعد استلام كل المستندات وبمبلغ ≤ المطلوب، `Rejected` بسبب، `MoreInfoNeeded`. يُشعَر المريض والطبيب.
- المطالبات: `POST /claims/from-invoice/{id}` (فاتورة فيها حصة تأمين، مرة واحدة) ⇒ `submit` ⇒ `settle {paidAmount}` (جزئي/كلي ولا يتجاوز المبلغ) · `reject {reason}`.

## اللوحات `/api/dashboard`
- `GET /overview?branchId` (النظام/المستشفى لأي فرع أو الكل، مدير الفرع والمالية لفرعهما): مواعيد اليوم، إيراد اليوم، المستحق، الأسرّة، الطوارئ، المختبر/الأشعة/الوصفات المعلقة، المخزون المنخفض، المصروفات والموافقات والإجازات المعلقة، عمليات اليوم، وسلسلة 7 أيام.
- `GET /me`: أعداد سريعة لشاشة الدور (مريض، طبيب، مختبر، أشعة، صيدلية، تمريض، استقبال، تأمين، مالية، موارد بشرية، طوارئ، تنويم، عمليات).

## ملاحظات
- بيانات العرض للأسرّة والمخزون والموظفين والطوارئ… تُزرع في الخطوة 8؛ حاليًا القوائم فارغة في قاعدة التطوير ما عدا الحسابات والكتالوج.
- التقارير الموسعة (تصدير، رسوم تاريخية أطول) خارج هذه الخطوة.
