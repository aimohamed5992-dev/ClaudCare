import { Badge, Button, Card, DataTable } from '../ui'
import './patterns.css'

const FLAG_TONES = { طبيعي: 'green', مرتفع: 'red', منخفض: 'orange' }

const columns = [
  { key: 'name', header: 'التحليل' },
  { key: 'value', header: 'النتيجة', render: (r) => <b className={r.flag !== 'طبيعي' ? 'lab__abnormal' : undefined}>{r.value} <small>{r.unit}</small></b> },
  { key: 'range', header: 'المعدل الطبيعي' },
  { key: 'flag', header: 'التقييم', render: (r) => <Badge tone={FLAG_TONES[r.flag] ?? 'gray'} dot>{r.flag}</Badge> },
]

// تقرير نتائج تحليل: report = { title, date, doctor, lab, rows:[{ name, value, unit, range, flag }] }
export default function LabReport({ report, showActions = true }) {
  const abnormal = report.rows.filter((r) => r.flag !== 'طبيعي').length
  return (
    <Card
      title={report.title}
      action={abnormal > 0 ? <Badge tone="orange">{abnormal} خارج المعدل</Badge> : <Badge tone="green">كل النتائج طبيعية</Badge>}
      padded={false}
      className="lab"
    >
      <div className="lab__meta">
        <span><small>التاريخ</small><b>{report.date}</b></span>
        <span><small>الطبيب المحيل</small><b>{report.doctor}</b></span>
        <span><small>المختبر</small><b>{report.lab}</b></span>
      </div>
      <DataTable columns={columns} rows={report.rows} rowKey="name" />
      {showActions && (
        <div className="lab__actions">
          <Button variant="soft" icon="download">تحميل PDF</Button>
          <Button variant="ghost" icon="print">طباعة</Button>
        </div>
      )}
    </Card>
  )
}
