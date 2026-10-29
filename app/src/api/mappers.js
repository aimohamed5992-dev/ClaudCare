// تحويل ردود الـ API إلى الشكل نفسه الذي تعرضه الصفحات (كانت بيانات تجريبية ثابتة).
const PATIENT_AVATAR = 'avatars/patient-2.jpg'
const staffAvatar = (key) => (key ? `${key}.jpg` : 'avatars/doc-m1.jpg')
const items = (j) => (Array.isArray(j) ? j : (j?.items ?? j?.page?.items ?? []))
export const money = (n) => `${Number(n).toLocaleString('en-US', { maximumFractionDigits: 2 })} ر.س`

const pad = (n) => String(n).padStart(2, '0')
export const hhmm = (m) => `${pad(Math.floor(m / 60))}:${pad(m % 60)}`
export const ampm = (m) => {
  const h = Math.floor(m / 60)
  return `${pad(h % 12 === 0 ? 12 : h % 12)}:${pad(m % 60)} ${h < 12 ? 'ص' : 'م'}`
}
const isoDay = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
export const todayIso = () => isoDay(new Date())
export const plusDays = (n) => isoDay(new Date(Date.now() + n * 864e5))
const dayWord = (iso) => (iso === todayIso() ? 'اليوم' : iso === plusDays(1) ? 'غدًا' : iso === plusDays(-1) ? 'أمس' : iso)
const MONTHS = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر']
const longDate = (iso) => {
  const [y, m, d] = iso.split('-').map(Number)
  return `${d} ${MONTHS[m - 1]} ${y}`
}
const atLocal = (utc) => new Date(utc.endsWith('Z') ? utc : `${utc}Z`)
const stamp = (utc) => {
  const d = atLocal(utc)
  const iso = isoDay(d)
  return `${dayWord(iso) === iso ? longDate(iso) : dayWord(iso)} ${hhmm(d.getHours() * 60 + d.getMinutes())}`
}
const ago = (utc) => {
  const min = Math.max(0, Math.round((Date.now() - atLocal(utc)) / 60000))
  if (min < 1) return 'الآن'
  if (min < 60) return `قبل ${min} د`
  if (min < 1440) return `قبل ${Math.floor(min / 60)} س`
  return stamp(utc)
}

// ---------- الإشعارات ----------
const NOTIF_TYPES = { Appointment: 'appointment', Result: 'result', Alert: 'alert', Payment: 'payment', Invoice: 'payment', Message: 'message', Medication: 'system', System: 'system' }
export const mapNotifications = (j) =>
  items(j).map((n) => {
    const at = atLocal(n.createdAt)
    const days = Math.floor((new Date(todayIso()) - new Date(isoDay(at))) / 864e5)
    return { id: n.id, type: NOTIF_TYPES[n.type] ?? 'system', title: n.title, text: n.text, time: ago(n.createdAt), group: days <= 0 ? 'اليوم' : days === 1 ? 'أمس' : 'هذا الأسبوع', unread: !n.read }
  })

// ---------- المختبر والأشعة ----------
const PRIORITY = { Urgent: 'عاجل', Normal: 'عادي' }
const LAB_STATUS = { Ordered: 'بانتظار العينة', SampleCollected: 'قيد التحليل', InProgress: 'قيد التحليل', ResultReady: 'جاهز', Cancelled: 'ملغى' }
export const mapLabRequests = (j) =>
  items(j).map((o) => ({ id: o.id, order: o.no, patient: o.patient?.fullName ?? '—', test: o.testName, doctor: o.doctorName ?? '—', priority: PRIORITY[o.priority], status: LAB_STATUS[o.status], time: stamp(o.orderedAt), avatar: PATIENT_AVATAR }))

const IMG_STATUS = { Ordered: 'بانتظار التصوير', Scheduled: 'بانتظار التصوير', InProgress: 'قيد التصوير', Reported: 'مكتمل', Cancelled: 'ملغى' }
export const mapRadRequests = (j) =>
  items(j).map((o) => ({ id: o.id, order: o.no, patient: o.patient?.fullName ?? '—', study: o.studyName, modality: o.modality, doctor: o.doctorName ?? '—', priority: PRIORITY[o.priority], status: IMG_STATUS[o.status], time: stamp(o.orderedAt), avatar: PATIENT_AVATAR }))

// ---------- الصيدلية ----------
const RX_STATUS = { Pending: 'بانتظار الصرف', Preparing: 'قيد التجهيز', Dispensed: 'تم الصرف', Rejected: 'مرفوضة' }
export const mapRx = (j) =>
  items(j).map((r) => ({ id: r.id, no: r.no, patient: r.patient.fullName, doctor: r.doctorName, itemsText: r.items.map((i) => i.drugName).join(' + '), time: ago(r.issuedAt), priority: PRIORITY[r.priority], status: RX_STATUS[r.status], avatar: PATIENT_AVATAR }))

export const mapInventory = (j) =>
  items(j).map((i) => {
    const level = Math.max(0, Math.min(100, Math.round((i.quantity / Math.max(1, i.minQuantity * 2)) * 100)))
    return {
      id: i.id, name: i.drugName, category: i.form ?? '—', qty: i.quantity, min: i.minQuantity, level, expiry: i.expiryDate ? i.expiryDate.slice(0, 7) : '—', supplier: i.supplier ?? '—',
      status: i.quantity === 0 ? 'نفد' : i.low ? 'منخفض' : i.expiringSoon ? 'قارب الانتهاء' : 'متوفر',
    }
  })

const PO_STATUS = { Draft: 'مسودة', Sent: 'مُرسل', PartiallyReceived: 'مستلم جزئيًا', Received: 'مستلم' }
export const mapPurchaseOrders = (j) => items(j).map((p) => ({ id: p.id, no: p.no, supplier: p.supplier, items: p.lines.length, total: money(p.total), date: p.orderDate, status: PO_STATUS[p.status] }))

const METHOD = { Cash: 'نقدًا', Card: 'بطاقة', ApplePay: 'بطاقة', BankTransfer: 'تحويل', Insurance: 'تأمين', PayAtClinic: 'في العيادة' }
export const mapSales = (j) =>
  items(j).map((s) => ({ id: s.id, no: s.no, patient: s.customerName ?? 'بيع مباشر', items: s.lines.map((l) => l.itemName).join(' + '), amount: money(s.total), method: METHOD[s.method], time: stamp(s.at), status: s.status === 'Refunded' ? 'مرتجع' : 'مدفوعة' }))

// ---------- المواعيد والمرضى ----------
const DOC_APPT = { Pending: 'مجدول', Confirmed: 'مجدول', Arrived: 'بالانتظار', InProgress: 'حاضر', Done: 'مكتمل', Cancelled: 'ملغي', NoShow: 'ملغي' }
export const mapDoctorAppointments = (j) =>
  items(j).map((a) => ({ id: a.id, patient: a.patient.fullName, age: a.patient.age ?? 0, reason: a.reason, time: hhmm(a.startMinutes), date: dayWord(a.date), status: DOC_APPT[a.status], avatar: PATIENT_AVATAR }))

export const mapDoctorPatients = (j) =>
  items(j).map((p) => ({ id: p.id, name: p.fullName, age: p.age ?? 0, gender: p.gender === 'Female' ? 'أنثى' : 'ذكر', condition: p.mainCondition ?? '—', last: p.lastVisit ? longDate(p.lastVisit) : '—', blood: p.bloodType ?? '—', phone: p.phone ?? '', avatar: PATIENT_AVATAR }))

const BOOKING = { Pending: 'بانتظار التأكيد', Confirmed: 'مؤكد', Arrived: 'تم الحضور', InProgress: 'تم الحضور', Done: 'تم الحضور', Cancelled: 'ملغي', NoShow: 'ملغي' }
export const mapBookings = (j) =>
  items(j).map((a) => ({ id: a.id, patient: a.patient.fullName, phone: '', doctor: a.doctor.fullName, dept: a.specialtyName, date: a.date, time: hhmm(a.startMinutes), status: BOOKING[a.status], avatar: PATIENT_AVATAR }))

const QUEUE = { Waiting: 'بالانتظار', Called: 'حاضر', Done: 'مكتمل', Cancelled: 'ملغي' }
export const mapQueue = (j) =>
  (Array.isArray(j) ? j : items(j)).map((t) => ({ id: t.id, ticket: `A-${String(t.ticketNo).padStart(3, '0')}`, patient: t.patientName, clinic: '—', doctor: '—', arrived: t.waitingMinutes ? `منذ ${t.waitingMinutes} د` : 'الآن', status: QUEUE[t.status], avatar: PATIENT_AVATAR }))

export const mapReceptionInvoices = (j) =>
  (j.page?.items ?? items(j)).map((i) => ({ id: i.id, no: i.no, patient: i.patient.fullName, item: i.title, amount: money(i.subtotal - i.insuranceCovered), method: i.payments[0] ? METHOD[i.payments[0].method] : '—', status: i.status === 'Unpaid' ? 'غير مدفوعة' : i.status === 'PartiallyPaid' ? 'جزئية' : 'مدفوعة', avatar: PATIENT_AVATAR }))

// ---------- المالية ----------
export const mapFinanceInvoices = (j) =>
  (j.page?.items ?? items(j)).map((i) => ({
    id: i.id, no: i.no, patient: i.patient.fullName, service: i.title, total: i.subtotal - i.discount, paid: i.paid, due: i.dueDate, avatar: PATIENT_AVATAR,
    status: i.status === 'Unpaid' ? (i.dueDate < todayIso() ? 'متأخرة' : 'غير مدفوعة') : i.status === 'PartiallyPaid' ? 'مدفوعة جزئيًا' : 'مدفوعة',
  }))

const EXP = { Pending: 'بانتظار الاعتماد', Approved: 'معتمد', Paid: 'مدفوع', Rejected: 'مرفوض' }
export const mapExpenses = (j) => items(j).map((e) => ({ id: e.id, no: e.no, title: e.title, category: e.category, amount: money(e.amount), date: e.expenseDate, by: e.requestedBy, status: EXP[e.status] }))

// ---------- الموارد البشرية ----------
const EMP = { Active: 'على رأس العمل', OnLeave: 'إجازة', Probation: 'تحت التجربة', Terminated: 'خارج الدوام' }
export const mapEmployees = (j) => items(j).map((e) => ({ id: e.id, name: e.fullName, role: e.jobTitle, dept: '—', hired: e.hireDate, phone: e.phone ?? '', status: EMP[e.status], avatar: 'avatars/hr-f1.jpg', balance: e.leaveBalanceDays }))
const LEAVE = { Annual: 'سنوية', Sick: 'مرضية', Emergency: 'طارئة' }
export const mapLeaves = (leaves, employees = []) => {
  const bal = new Map(employees.map((e) => [e.id, e.balance]))
  return (Array.isArray(leaves) ? leaves : items(leaves))
    .filter((l) => l.status === 'Pending')
    .map((l) => ({ id: l.id, name: l.employeeName, dept: '—', type: LEAVE[l.type], from: l.from, to: l.to, days: l.days, balance: bal.get(l.employeeId) ?? 0, onLeave: 0, maxOff: 2, avatar: 'avatars/hr-f1.jpg' }))
}

// ---------- التأمين ----------
const PA = { Pending: 'قيد المراجعة', MoreInfoNeeded: 'معلومات ناقصة', Approved: 'موافق', Rejected: 'مرفوض' }
export const mapApprovals = (j) =>
  items(j).map((p) => ({ id: p.id, no: p.no, patient: p.patient.fullName, insurer: p.insurer, procedure: p.procedureName, amount: money(p.amount), priority: PRIORITY[p.priority], status: PA[p.status], submitted: ago(p.submittedAt), doctor: p.doctorName ?? '—', avatar: PATIENT_AVATAR }))
const CLAIM = { Draft: 'مسودة', Submitted: 'مُرسلة', Paid: 'مدفوعة', PartiallyPaid: 'مدفوعة جزئيًا', Rejected: 'مرفوضة' }
export const mapClaims = (j) => items(j).map((c) => ({ id: c.id, no: c.no, patient: c.patient.fullName, insurer: c.insurer, amount: money(c.amount), date: c.submittedAt ? c.submittedAt.slice(0, 10) : '—', status: CLAIM[c.status], avatar: PATIENT_AVATAR }))

// ---------- الطوارئ والتنويم والعمليات ----------
const ER = { Waiting: 'بانتظار الفحص', InExam: 'قيد المعاينة', Admitted: 'تحويل للتنويم', Discharged: 'خرج' }
export const mapErCases = (j) =>
  (j.cases ?? items(j)).map((c) => ({ id: c.id, name: c.patientName, age: c.age ?? 0, complaint: c.complaint, level: c.triageLevel, arrived: `قبل ${c.waitingMinutes} د`, zone: c.zone, status: ER[c.status], avatar: PATIENT_AVATAR }))
export const mapAmbulances = (j) =>
  (Array.isArray(j) ? j : items(j)).map((a) => ({ id: a.id, unit: a.unitNo, patient: a.patientDescription, condition: a.condition, eta: a.etaMinutes != null ? `${a.etaMinutes} دقائق` : '—', level: a.triageLevel, status: a.status === 'Arrived' ? 'وصلت' : 'في الطريق' }))
export const mapPendingAdmissions = (j) =>
  items(j).map((a) => ({ id: a.id, name: a.patient.fullName, age: 0, from: a.requestedFrom ?? '—', reason: a.reason, ward: a.requestedWard, insured: a.insuranceApprovalVerified, approval: a.insuranceApprovalVerified ? 'موافقة التأمين موثّقة' : 'بانتظار التحقق', avatar: PATIENT_AVATAR }))

const CASE = { Scheduled: 'مجدولة', InProgress: 'جارية', Done: 'تمت', Cancelled: 'ملغاة' }
export const mapSurgeries = (j) =>
  (Array.isArray(j) ? j : items(j)).map((s) => ({ id: s.id, no: s.no, patient: s.patient.fullName, procedure: s.procedureName, room: s.roomCode, time: `${hhmm(s.startMinutes)} – ${hhmm(s.startMinutes + s.durationMinutes)}`, surgeon: s.surgeonName, anesthetist: s.anesthetistName ?? '—', status: CASE[s.status], avatar: PATIENT_AVATAR }))

// ---------- المريض ----------
const MY_APPT = { Pending: 'قادم', Confirmed: 'قادم', Arrived: 'قادم', InProgress: 'قادم', Done: 'مكتمل', Cancelled: 'ملغي', NoShow: 'ملغي' }
export const mapMyAppointments = (j) =>
  items(j).map((a) => ({ id: a.id, doctor: a.doctor.fullName, dept: a.specialtyName, date: `${dayWord(a.date) === a.date ? '' : `${dayWord(a.date)} `}${longDate(a.date)}`.trim(), time: ampm(a.startMinutes), place: 'الفرع الرئيسي', status: MY_APPT[a.status], avatar: staffAvatar(a.doctor.avatarKey), reason: a.reason }))
export const mapMyInvoices = (j) =>
  (j.page?.items ?? items(j)).map((i) => ({ id: i.id, no: i.no, item: i.title, date: longDate(i.issueDate), amount: money(i.subtotal - i.insuranceCovered), status: i.status === 'Paid' || i.status === 'Covered' ? 'مدفوعة' : 'مستحقة', insurance: i.insuranceCovered > 0 ? `تغطية ${money(i.insuranceCovered)}` : '—' }))
