# الأطباء والمواعيد

كل النقاط تتطلب `Authorization: Bearer`. التواريخ `yyyy-MM-dd` والأوقات **دقائق من منتصف الليل بتوقيت العيادة** (09:30 = 570).

## دليل الأطباء (مريض أو طاقم)
| الطلب | الوصف |
|---|---|
| `GET /api/specialties` | التخصصات مع عدد أطبائها |
| `GET /api/doctors?q=&specialty=&sort=rating\|price\|experience&favoritesOnly=&page=&pageSize=` | بحث وتصفية؛ كل طبيب فيه `nextSlot` و`isFavorite` |
| `GET /api/doctors/{id}` | الملف: نبذة، أسعار، أيام الدوام، آخر 10 تقييمات |
| `GET /api/doctors/{id}/availability?from=&days=14&type=Clinic\|Video` | الخانات المتاحة لكل يوم (الأيام الخالية لا تُعاد) |
| `GET/PUT/DELETE /api/me/favorites/doctors[/{id}]` | المفضلة (المريض) — متكررة الأثر |

## الطبيب
| الطلب | الوصف |
|---|---|
| `GET /api/doctors/me/working-hours` · `PUT` | عرض/تعديل أيام وساعات الدوام (≥ ساعة). يعيد `futureAppointmentsOutsideHours` |
| `GET /api/doctors/me/agenda?date=` | مواعيد اليوم + عدّادات الحالات (للرئيسية) |

## المواعيد
| الطلب | من | الوصف |
|---|---|---|
| `POST /api/appointments` | مريض (لنفسه) · استقبال/إدارة (`patientId`) | حجز. يحسب التأمين ويحدد الحالة (Pending إن كان الطبيب يتطلب تأكيدًا) |
| `GET /api/appointments?scope=upcoming\|past\|cancelled\|all&date=&from=&to=&status=&doctorId=&patientId=&page=` | حسب الدور | القائمة. المريض/الطبيب: الخاص بهم · استقبال/تمريض/مدير فرع: فرعهم · إدارة: الكل |
| `GET /api/appointments/{id}` | | خارج النطاق ⇒ 404 |
| `POST …/{id}/cancel {reason}` | مريض/طبيب/استقبال | يُعيد `lateCancellation` و`cancellationFee` (مريض، < 24 ساعة) |
| `POST …/{id}/reschedule {date, startMinutes}` | مريض/طبيب/استقبال | الطبيب يؤكد الطلب المعلّق عند النقل |
| `POST …/{id}/confirm` | طبيب/استقبال | من Pending |
| `POST …/{id}/arrive` | استقبال/تمريض | يوم الموعد فقط، للزيارات الحضورية |
| `POST …/{id}/start` · `/complete` | الطبيب | البدء بعد الوصول (أو من Confirmed للفيديو) |
| `POST …/{id}/no-show` | طبيب/استقبال | بعد وقت الموعد |
| `POST …/{id}/review {rating 1-5, comment}` | المريض | مرة واحدة بعد Done؛ يحدّث متوسط الطبيب |

كل `AppointmentDto` يحمل **`allowedActions`** (ما يستطيعه المستخدم الحالي الآن) لتبني الواجهات أزرارها منه دون تكرار القواعد.

## الحالات
`Pending → Confirmed → Arrived → InProgress → Done` · `Cancelled` من Pending/Confirmed · `NoShow` بعد الوقت.

## رموز الأخطاء الخاصة
`slot_invalid` 400 (خارج الدوام/الماضي/غير على الشبكة/أبعد من 60 يومًا) · `slot_taken` 409 · `patient_busy` 409 · `doctor_on_leave` 400 ·
`video_unavailable` 400 · `doctor_not_found` / `appointment_not_found` / `family_member_not_found` 404 · `invalid_state` 409 · `action_not_allowed` 403 ·
`appointment_started` 409 · `not_today` 409 · `same_slot` 400 · `already_reviewed` 409.

## قواعد مهمة
- **منع الحجز المزدوج** مضمون في قاعدة البيانات (فهرس فريد) فيصمد أمام الطلبات المتزامنة: من 8 طلبات لنفس الخانة ينجح واحد.
- **التسعير:** السعر الكامل `price`، وحصة التأمين `insuranceCovered = (1 − التحمّل) × السعر` مقيّدة بالمتبقي من الحد السنوي، والمستحق `due = price − insuranceCovered`.
  الدفع الإلكتروني يُحاكى حاليًا ويُسجَّل كدفعة فعلية مع الفواتير (الخطوة 6).
- **الإلغاء:** مجاني قبل 24 ساعة؛ بعدها تُحسب رسوم 50% من حصة المريض (للعرض، والتحصيل مع الفواتير). المريض لا يلغي بعد بدء الموعد.
- الإشعارات تُكتب للطرف الآخر عند الحجز والتأكيد والإلغاء والنقل والوصول والإكمال.
- التوقيت: منطقة العيادة `Clinic:TimeZone` (الرياض). التواريخ تُنسَّق دائمًا بالثقافة الثابتة (جهازك بالتقويم الهجري!).
