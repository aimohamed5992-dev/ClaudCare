import { Link } from 'react-router-dom'
import { Avatar, Icon } from '../ui'
import GlobalSearch from './GlobalSearch'
import useAuth from '../../hooks/useAuth'
import useNotifications from '../../hooks/useNotifications'

export default function Topbar({ title, onMenu }) {
  const { role, user } = useAuth()
  const { unread } = useNotifications()
  return (
    <header className="topbar">
      <button className="topbar__menu" onClick={onMenu} aria-label="فتح القائمة">
        <Icon name="menu" size={22} />
      </button>
      <h1 className="topbar__title">{title}</h1>
      <GlobalSearch />
      <Link to={`/${role.key}/notifications`} className="topbar__icon" aria-label={unread ? `الإشعارات (${unread} غير مقروءة)` : 'الإشعارات'}>
        <Icon name="bell" size={20} />
        {unread > 0 && <i className="topbar__dot">{unread}</i>}
      </Link>
      <Link to={`/${role.key}/profile`} className="topbar__avatar" aria-label="الملف الشخصي">
        <Avatar src={user.avatar} name={user.name} size={42} />
      </Link>
    </header>
  )
}
