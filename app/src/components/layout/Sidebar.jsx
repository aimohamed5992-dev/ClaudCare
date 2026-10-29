import { NavLink, useNavigate } from 'react-router-dom'
import { Avatar, Icon } from '../ui'
import { logo } from '../../utils/assets'
import { cx } from '../../utils/cx'
import useAuth from '../../hooks/useAuth'

export default function Sidebar({ open, onNavigate }) {
  const { role, user, logout } = useAuth()
  const navigate = useNavigate()

  const handleLogout = () => {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <aside className={cx('sidebar', open && 'is-open')} aria-label="الشريط الجانبي">
      <NavLink to={`/${role.key}`} className="sidebar__logo" onClick={onNavigate}>
        <img src={logo} alt="ClaudCare" />
      </NavLink>

      <nav className="sidebar__nav" aria-label="القائمة الرئيسية">
        {role.nav.map((item) => (
          <NavLink
            key={item.path}
            to={item.path ? `/${role.key}/${item.path}` : `/${role.key}`}
            end={item.path === ''}
            className={({ isActive }) => cx('sidebar__link', isActive && 'is-active')}
            onClick={onNavigate}
          >
            <Icon name={item.icon} />
            {item.label}
          </NavLink>
        ))}
      </nav>

      <div className="sidebar__foot">
        <NavLink to={`/${role.key}/profile`} className="sidebar__user" onClick={onNavigate}>
          <Avatar src={user.avatar} name={user.name} size={40} />
          <span>
            <b>{user.name}</b>
            <small>{role.label}</small>
          </span>
        </NavLink>
        <NavLink to={`/${role.key}/settings`} className={({ isActive }) => cx('sidebar__link', isActive && 'is-active')} onClick={onNavigate}>
          <Icon name="settings" />
          الإعدادات
        </NavLink>
        <button className="sidebar__link" onClick={() => navigate('/roles')}>
          <Icon name="users" />
          تبديل نوع الحساب
        </button>
        <button className="sidebar__link" onClick={handleLogout}>
          <Icon name="logout" />
          تسجيل الخروج
        </button>
      </div>
    </aside>
  )
}
