import { Avatar, Badge, Progress } from '../ui'

// يحوّل تعريف الأعمدة التصريحي إلى أعمدة DataTable.
// أنواع الخلايا:
//   text (افتراضي) · person { avatar, sub } · badge { tones } · progress { tone(value) } · render(row) مخصص
export function toColumns(columns) {
  return columns.map((c) => {
    if (c.render) return c
    if (c.type === 'person') {
      return {
        ...c,
        render: (row) => (
          <span className="cell-person">
            <Avatar src={c.avatar ? row[c.avatar] : undefined} name={String(row[c.key])} size={36} />
            <span>
              <b>{row[c.key]}</b>
              {c.sub && <small>{row[c.sub]}</small>}
            </span>
          </span>
        ),
      }
    }
    if (c.type === 'badge') {
      return { ...c, render: (row) => <Badge tone={c.tones?.[row[c.key]] ?? 'gray'} dot>{row[c.key]}</Badge> }
    }
    if (c.type === 'progress') {
      return { ...c, render: (row) => <Progress value={row[c.key]} tone={c.tone?.(row[c.key])} /> }
    }
    return c
  })
}
