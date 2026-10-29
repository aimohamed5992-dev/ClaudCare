import { Icon } from '../../components/ui'
import { asset, logo } from '../../utils/assets'
import './auth.css'

const features = [
  { icon: 'shield', text: 'حماية وخصوصية كاملة لبيانات المرضى' },
  { icon: 'clock', text: 'نتائج وتقارير في وقتها الفعلي' },
  { icon: 'users', text: 'إدارة المستشفى والفروع من مكان واحد' },
]

// تخطيط شاشات الدخول: نموذج على جانب وصورة على الجانب الآخر
export default function AuthLayout({ title, subtitle, children, footer }) {
  return (
    <div className="auth">
      <section className="auth__form">
        <div className="auth__inner">
          <img className="auth__logo" src={logo} alt="ClaudCare" />
          <header>
            <h1>{title}</h1>
            {subtitle && <p>{subtitle}</p>}
          </header>
          {children}
          {footer && <footer className="auth__footer">{footer}</footer>}
        </div>
      </section>

      <aside className="auth__art" aria-hidden="true">
        <img src={asset('auth/auth-hero.jpg')} alt="" />
        <div className="auth__card">
          <h2>رعاية صحية ذكية في مكان واحد</h2>
          <ul>
            {features.map((f) => (
              <li key={f.icon}>
                <span>
                  <Icon name={f.icon} size={18} />
                </span>
                {f.text}
              </li>
            ))}
          </ul>
        </div>
      </aside>
    </div>
  )
}
