import { useNavigate } from 'react-router-dom'
import { BarChart, Button, Card, DataTable, ListItem, StatCard } from '../ui'
import { toColumns } from './cells'
import { asset } from '../../utils/assets'
import './patterns.css'

// قالب لوحة التحكم: بانر ترحيب + إحصاءات + جدول + قائمة جانبية + مخططات.
// كل الأجزاء اختيارية ما عدا stats.
export default function DashboardTemplate({ banner, stats, table, side, charts = [] }) {
  const navigate = useNavigate()
  return (
    <div className="dash">
      {banner && (
        <section className="dash__banner">
          <div>
            <h2>{banner.title}</h2>
            <p>{banner.text}</p>
            {banner.cta && <Button onClick={() => banner.to && navigate(banner.to)}>{banner.cta}</Button>}
          </div>
          {banner.image && <img src={asset(`roles/${banner.image}`)} alt="" />}
        </section>
      )}

      <div className="dash__stats">
        {stats.map((s) => (
          <StatCard key={s.label} {...s} />
        ))}
      </div>

      {(table || side) && (
        <div className="dash__cols">
          {table && (
            <Card title={table.title} action={table.link && <a href={table.link}>عرض الكل</a>} padded={false}>
              <DataTable columns={toColumns(table.columns)} rows={table.rows} />
            </Card>
          )}
          {side && (
            <Card title={side.title}>
              <div className="dash__list">
                {side.items.map((i, idx) => (
                  <ListItem key={`${i.title}-${idx}`} {...i} />
                ))}
              </div>
            </Card>
          )}
        </div>
      )}

      {charts.length > 0 && (
        <div className={charts.length > 1 ? 'dash__charts dash__charts--two' : 'dash__charts'}>
          {charts.map((c) => (
            <Card key={c.title} title={c.title}>
              <BarChart data={c.data} />
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
