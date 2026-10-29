# الفواتير والتأمين والإشعارات والمراسلة والحساب (الخطوة 6)

كل المسارات تتطلب توكن وصول (`Authorization: Bearer …`). خارج النطاق ⇒ `404` وليس `403` كي لا نكشف الوجود.

## الفواتير

تُنشأ فاتورة مع **كل حجز** (`INV-000123`) برقم متسلسل، وتُربط بالموعد (`Appointment.InvoiceId`).

| الطلب | الوصف |
|---|---|
| `GET /api/invoices?status&patientId&appointmentId&page&pageSize` | القائمة + `outstandingTotal` + `unpaidCount` |
| `GET /api/invoices/{id}` | التفاصيل: البنود، الدفعات، حصة التأمين، المتبقي |
| `POST /api/invoices/{id}/pay` | `{method, amount?, tendered?, reference?}` |

**النطاق:** المريض فواتيره · الاستقبال/مدير الفرع فرعهم · المالية/المستشفى/الإدارة الكل · غيرهم `403`.

**الحالات:** `Unpaid` · `PartiallyPaid` · `Paid` · `Covered` (غطّاها التأمين كليًا) · `Cancelled`.
`Remaining = Subtotal − Discount − InsuranceCovered − Paid`.

**السداد**
- المريض: `Card` أو `ApplePay` فقط، ويُسدَّد كامل المتبقي (يُتجاهل `amount`). الدفع محاكى.
- الموظف: `Card/Cash/ApplePay/BankTransfer`، ومبلغ جزئي اختياري. `tendered` للنقد فقط ويُحسب منه `changeGiven`.
- مبلغ يتجاوز المتبقي ⇒ `400`. فاتورة مدفوعة/ملغاة ⇒ `409 invalid_state`.
- يُقفل صف الفاتورة (`SELECT … FOR UPDATE`) داخل معاملة، فلا تنجح دفعتان متزامنتان معًا.
- عند اكتمال السداد يصبح `Appointment.Paid = true`، ويُشعَر المريض إن حصّل الموظف الدفعة.

**الحجز:** دفع إلكتروني ⇒ فاتورة مدفوعة ودفعة مسجلة · `PayAtClinic` ⇒ `Unpaid` · تغطية كاملة ⇒ `Covered`.
حصة التأمين تُستهلك من `PatientInsurance.UsedAmount` وتُعاد عند الإلغاء.

**الإلغاء**
- مجاني (أكثر من 24 ساعة، أو بواسطة الطبيب/الموظف): الفاتورة `Cancelled`، ويُسجَّل استرداد (دفعة سالبة `REFUND`) لما دُفع.
- متأخر (المريض، أقل من 24 ساعة): تتحول الفاتورة إلى بند «رسوم إلغاء متأخر» (50% من حصة المريض)، ويُسترد الفرق إن كان دفع أكثر، وإن لم يدفع تبقى `Unpaid` لتُحصَّل.

## التأمين

`GET /api/me/insurance` ⇒ الشركة، الفئة، رقم الوثيقة، `copayRate`، `annualLimit`، `used`، `remaining`، `active`، `expired`. لا وثيقة ⇒ `404 insurance_not_found`.

## الإشعارات (`/api/notifications`)

| الطلب | الوصف |
|---|---|
| `GET ?unread=true&type=&page&pageSize` | القائمة + `unread` |
| `GET /unread-count` | العدّاد |
| `POST /{id}/read` · `POST /read-all` | مقروء |
| `DELETE /{id}` · `POST /{id}/restore` | حذف ناعم + تراجع |

الأنواع: `Appointment, Result, Medication, Invoice, Message, Payment, Alert, System`. التجميع «اليوم/أمس» عند العميل من `createdAt`.

## المراسلة (`/api/conversations`)

- `POST /` `{userId, text?}`: يفتح/يعيد المحادثة المباشرة. المريض يراسل أطباءه فقط (له معهم موعد غير ملغى أو كشف)؛ الطبيب يبدأ مع مرضاه؛ الطاقم يراسل الطاقم. غير ذلك `403/404`.
- `POST /support` `{text?}`: محادثة دعم واحدة لكل مريض. `GET /support`: قائمة الدعم للاستقبال/الفرع/المستشفى/الإدارة، وينضم الموظف عند أول رد.
- `GET /` قائمتي (آخر رسالة + `unread`) · `GET /unread-count`.
- `GET /{id}/messages?take&before`: الأحدث أولًا في الصفحة، مرتبة تصاعديًا، و`hasMore`. فتحها يصفّر غير المقروء.
- `POST /{id}/messages` `{text}` (حتى 2000 حرف) ⇒ `201`. يصل المستلم إشعار **واحد** لكل دفعة رسائل غير مقروءة.
- `POST /{id}/read` ⇒ `204`.

## الحساب (`/api/me`)

- `GET/PUT /profile`: الاسم، البريد (فريد، يُحوَّل لأحرف صغيرة `email_taken` عند التكرار)، سنة الميلاد، الجنس، و`about` للطبيب. تغيير الجوال خارج هذه الخطوة.
- `GET/POST /family` · `PUT/DELETE /family/{id}`: للمريض فقط، حتى 10 أفراد. حذف فرد له مواعيد ⇒ `409 family_member_in_use`.
- `GET/PUT /settings`: مفاتيح منطقية بقيم افتراضية: `appointments, medications, results, offers(=false), newAppointments, messages, labResults, sound, biometric(=false)`. التحديث جزئي `{"values":{"offers":true}}`، ومفتاح غير معروف `400`.

## ملاحظات

- ربط الدفع ببوابة حقيقية غير موجود؛ الدفع الإلكتروني محاكى ويُسجَّل كدفعة بمرجع `SIM-…`.
- ترقيم الفاتورة يتم بعد الحفظ الأول (رقم مؤقت ثم `INV-{id}`)؛ انقطاع بين الخطوتين يترك رقمًا مؤقتًا فقط.
- المراسلة بالاستعلام (polling)؛ لا WebSocket بعد.
