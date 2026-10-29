import { useState } from 'react'
import { Badge, Button, Modal } from '../ui'
import { asset } from '../../utils/assets'
import './patterns.css'

const STATUS_TONES = { 'التقرير جاهز': 'green', 'بانتظار التقرير': 'orange' }

// شبكة فحوصات التصوير: studies = [{ id, title, date, doctor, image, status, report }]
export default function StudyGrid({ studies }) {
  const [open, setOpen] = useState(null)
  return (
    <>
      <div className="studies">
        {studies.map((s) => (
          <button key={s.id} className="study" onClick={() => setOpen(s)}>
            <span className="study__img">
              <img src={asset(`medical/${s.image}`)} alt="" loading="lazy" />
            </span>
            <span className="study__body">
              <b>{s.title}</b>
              <small>{s.date}</small>
              <Badge tone={STATUS_TONES[s.status] ?? 'gray'} dot>{s.status}</Badge>
            </span>
          </button>
        ))}
      </div>

      <Modal
        open={!!open}
        onClose={() => setOpen(null)}
        title={open?.title}
        width={720}
        footer={
          <>
            <Button variant="ghost" icon="print">طباعة</Button>
            <Button variant="blue" icon="download">تحميل الصورة</Button>
          </>
        }
      >
        {open && (
          <div className="study__view">
            <div className="study__big">
              <img src={asset(`medical/${open.image}`)} alt={open.title} />
            </div>
            <dl className="detail__list">
              <div><dt>التاريخ</dt><dd>{open.date}</dd></div>
              <div><dt>الطبيب</dt><dd>{open.doctor}</dd></div>
              <div><dt>الحالة</dt><dd>{open.status}</dd></div>
            </dl>
            <div>
              <b>التقرير</b>
              <p className="study__report">{open.report}</p>
            </div>
          </div>
        )}
      </Modal>
    </>
  )
}
