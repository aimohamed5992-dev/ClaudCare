import './patterns.css'

// هيكل تحميل عام: صف إحصاءات + جدول
export default function PageSkeleton() {
  return (
    <div className="skel" role="status" aria-label="جارٍ التحميل">
      <div className="skel__stats">
        {[0, 1, 2, 3].map((i) => <span key={i} className="skel__box skel__box--stat" />)}
      </div>
      <div className="skel__cols">
        <span className="skel__box skel__box--table" />
        <span className="skel__box skel__box--table" />
      </div>
      <span className="visually-hidden">جارٍ التحميل…</span>
    </div>
  )
}
