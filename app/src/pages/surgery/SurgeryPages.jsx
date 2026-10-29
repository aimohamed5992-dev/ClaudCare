import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { DashboardTemplate, ListPage } from '../../components/patterns'
import { Badge, Button, Card, Checkbox, Icon, Select } from '../../components/ui'
import { CASE_TONES, CHECK_ITEMS, ROOMS, ROOM_TONES, TEAM_TONES, casesWeekly, cases, checklistCases, roomUtil, rooms as initialRooms, team } from '../../data/surgery'
import { cx } from '../../utils/cx'
import './surgery.css'
import '../reception/reception.css'

export function SurgeryDashboard() {
  const navigate = useNavigate()
  return (
    <DashboardTemplate
      banner={{ title: 'غرفة العمليات', text: 'عملية جارية في OR-1، و3 عمليات مجدولة اليوم.', cta: 'جدول العمليات', to: '/surgery/schedule', image: 'role-surgery.png' }}
      stats={[
        { icon: 'calendar', label: 'عمليات اليوم', value: '4' },
        { icon: 'pulse', label: 'جارية الآن', value: '1', tone: 'blue' },
        { icon: 'building', label: 'غرف متاحة', value: '1 من 4', tone: 'green' },
        { icon: 'alert', label: 'قوائم غير مكتملة', value: '2', tone: 'orange' },
      ]}
      table={{
        title: 'عمليات اليوم',
        columns: [
          { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'procedure' },
          { key: 'room', header: 'الغرفة' },
          { key: 'time', header: 'الوقت' },
          { key: 'status', header: 'الحالة', type: 'badge', tones: CASE_TONES },
        ],
        rows: cases.filter((c) => c.status !== 'تمت'),
      }}
      side={{
        title: 'يحتاج انتباهك',
        items: [
          { mark: '!', title: 'تحذير: مميّع دم', subtitle: 'سارة أحمد – SG-1043', tone: 'red', onClick: () => navigate('/surgery/checklist') },
          { mark: '2', title: 'قوائم ما قبل العملية ناقصة', subtitle: 'عمليتان خلال 3 ساعات', tone: 'orange', onClick: () => navigate('/surgery/checklist') },
          { mark: '1', title: 'OR-4 تحت الصيانة', subtitle: 'فحص جهاز التخدير', tone: 'orange', onClick: () => navigate('/surgery/rooms') },
        ],
      }}
      charts={[
        { title: 'العمليات خلال الأسبوع', data: casesWeekly },
        { title: 'استخدام الغرف (%)', data: roomUtil },
      ]}
    />
  )
}

export function SurgerySchedule() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'now', label: 'جارية', match: (r) => r.status === 'جارية' },
        { key: 'plan', label: 'مجدولة', match: (r) => r.status === 'مجدولة' },
        { key: 'done', label: 'تمت', match: (r) => r.status === 'تمت' },
      ]}
      columns={[
        { key: 'no', header: 'الرقم' },
        { key: 'patient', header: 'المريض', type: 'person', avatar: 'avatar', sub: 'procedure' },
        { key: 'room', header: 'الغرفة' },
        { key: 'time', header: 'الوقت' },
        { key: 'surgeon', header: 'الجرّاح' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: CASE_TONES },
      ]}
      rows={cases}
      searchKeys={['no', 'patient', 'procedure', 'surgeon']}
      addLabel="جدولة عملية"
      form={{
        title: 'جدولة عملية',
        fields: [
          { name: 'patient', label: 'اسم المريض', required: true },
          { name: 'procedure', label: 'الإجراء الجراحي', required: true },
          { name: 'room', label: 'الغرفة', options: ROOMS },
          { name: 'time', label: 'الوقت', required: true },
          { name: 'surgeon', label: 'الجرّاح', options: ['د. عمر الشامي', 'د. ريم العتيبي', 'د. زياد النجار'] },
        ],
        defaults: { no: 'SG-1045', anesthetist: 'يُحدد لاحقًا', status: 'مجدولة' },
      }}
      detail={{
        titleKey: 'patient',
        subKey: 'no',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: CASE_TONES,
        fields: [
          { label: 'الإجراء', key: 'procedure' },
          { label: 'الغرفة', key: 'room' },
          { label: 'الوقت', key: 'time' },
          { label: 'الجرّاح', key: 'surgeon' },
          { label: 'التخدير', key: 'anesthetist' },
        ],
        actions: [{ label: 'قائمة ما قبل العملية', icon: 'check', variant: 'blue', to: '/surgery/checklist' }, { label: 'تعديل الموعد', icon: 'edit', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- حالة الغرف ---------- */
export function SurgeryRooms() {
  const [rooms, setRooms] = useState(initialRooms)
  const set = (id, state, note) => setRooms((rs) => rs.map((r) => (r.id === id ? { ...r, state, note } : r)))
  return (
    <div className="orr">
      {rooms.map((r) => (
        <Card key={r.id} className={cx('orr__card', `orr__card--${ROOM_TONES[r.state]}`)}>
          <div className="orr__head"><b dir="ltr">{r.id}</b><Badge tone={ROOM_TONES[r.state]} dot>{r.state}</Badge></div>
          <p className="orr__note">{r.note}</p>
          <div className="orr__actions">
            {r.state === 'متاحة' && <Button size="sm" variant="blue" onClick={() => set(r.id, 'قيد العملية', 'عملية قيد التنفيذ')}>بدء عملية</Button>}
            {r.state === 'قيد العملية' && <Button size="sm" variant="blue" onClick={() => set(r.id, 'تنظيف وتعقيم', 'انتهت العملية – بانتظار التعقيم')}>إنهاء العملية</Button>}
            {r.state === 'تنظيف وتعقيم' && <Button size="sm" variant="blue" icon="check" onClick={() => set(r.id, 'متاحة', 'جاهزة للعملية التالية')}>اكتمل التعقيم</Button>}
            {r.state === 'صيانة' && <Button size="sm" variant="blue" onClick={() => set(r.id, 'تنظيف وتعقيم', 'انتهت الصيانة – بانتظار التعقيم')}>انتهت الصيانة</Button>}
            {(r.state === 'متاحة' || r.state === 'تنظيف وتعقيم') && <Button size="sm" variant="ghost" onClick={() => set(r.id, 'صيانة', 'أُغلقت للصيانة')}>إغلاق للصيانة</Button>}
          </div>
        </Card>
      ))}
    </div>
  )
}

export function SurgeryTeam() {
  return (
    <ListPage
      tabs={[
        { key: 'all', label: 'الكل', match: () => true },
        { key: 'surg', label: 'جرّاحون', match: (r) => r.role.includes('جرّاح') },
        { key: 'an', label: 'تخدير', match: (r) => r.role.includes('تخدير') },
        { key: 'nurse', label: 'تمريض وفنيون', match: (r) => r.role.includes('ممرضة') || r.role.includes('فني') },
      ]}
      columns={[
        { key: 'name', header: 'العضو', type: 'person', avatar: 'avatar', sub: 'role' },
        { key: 'cases', header: 'عمليات اليوم' },
        { key: 'status', header: 'الحالة', type: 'badge', tones: TEAM_TONES },
      ]}
      rows={team}
      searchKeys={['name', 'role']}
      detail={{
        titleKey: 'name',
        subKey: 'role',
        avatar: 'avatar',
        badgeKey: 'status',
        tones: TEAM_TONES,
        fields: [{ label: 'عمليات اليوم', key: 'cases' }],
        actions: [{ label: 'إسناد لعملية', icon: 'plus', variant: 'blue', to: '/surgery/schedule' }, { label: 'جدول المناوبة', icon: 'calendar', variant: 'ghost' }],
      }}
    />
  )
}

/* ---------- قائمة ما قبل العملية ---------- */
export function SurgeryChecklist() {
  const [id, setId] = useState(String(checklistCases[0].id))
  const [state, setState] = useState(() => Object.fromEntries(checklistCases.map((c) => [c.id, { done: Object.fromEntries(c.done.map((k) => [k, true])), ack: false, ready: false }])))
  const c = checklistCases.find((x) => String(x.id) === id)
  const st = state[c.id]
  const patch = (p) => setState((s) => ({ ...s, [c.id]: { ...s[c.id], ...p } }))
  const missing = CHECK_ITEMS.filter((i) => !st.done[i.key])
  const pct = Math.round(((CHECK_ITEMS.length - missing.length) / CHECK_ITEMS.length) * 100)
  const reasons = [
    missing.length > 0 && `${missing.length} بنود غير مكتملة`,
    c.alerts.length > 0 && !st.ack && 'أكّد الاطلاع على تحذيرات المريض',
  ].filter(Boolean)

  return (
    <div className="pre">
      <div className="pre__main">
        <Card>
          <Select label="العملية" options={checklistCases.map((x) => ({ value: String(x.id), label: `${x.no} · ${x.patient} · ${x.procedure}${state[x.id].ready ? ' ✓' : ''}` }))} value={id} onChange={(e) => setId(e.target.value)} />
        </Card>
        {c.alerts.map((a) => <div key={a} className="pre__alert" role="alert"><Icon name="alert" size={20} /><span>{a}</span></div>)}
        {c.allergy !== 'لا يوجد' && <div className="pre__allergy"><Icon name="alert" size={18} /><span><b>حساسية:</b> {c.allergy}</span></div>}
        <Card title="بنود التحقق">
          <div className="pre__items">
            {CHECK_ITEMS.map((i) => (
              <Checkbox key={i.key} label={i.label} checked={!!st.done[i.key]} disabled={st.ready} onChange={(e) => patch({ done: { ...st.done, [i.key]: e.target.checked } })} />
            ))}
          </div>
        </Card>
      </div>
      <aside className="pre__side">
        <Card title="جاهزية العملية">
          <div className="pre__bar" role="progressbar" aria-valuenow={pct} aria-valuemin="0" aria-valuemax="100" aria-label="اكتمال القائمة"><span style={{ width: `${pct}%` }} /></div>
          <p style={{ margin: '8px 0 0', color: 'var(--muted)' }}>{pct}% مكتمل</p>
          <div className="pre__form">
            {c.alerts.length > 0 && <Checkbox label="اطّلعت على تحذيرات المريض وتم التنسيق مع الطبيب" checked={st.ack} disabled={st.ready} onChange={(e) => patch({ ack: e.target.checked })} />}
            {st.ready ? (
              <>
                <Badge tone="green" dot>العملية جاهزة</Badge>
                <Button variant="ghost" onClick={() => patch({ ready: false })}>إعادة فتح القائمة</Button>
              </>
            ) : (
              <>
                {reasons.length > 0 && <ul className="pre__reasons" aria-live="polite">{reasons.map((r) => <li key={r}>{r}</li>)}</ul>}
                <Button icon="check" size="lg" full disabled={reasons.length > 0} onClick={() => patch({ ready: true })}>اعتماد الجاهزية</Button>
              </>
            )}
          </div>
        </Card>
      </aside>
    </div>
  )
}
