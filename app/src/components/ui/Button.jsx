import { cx } from '../../utils/cx'
import Icon from './Icon'

// variant: primary (برتقالي) | blue | ghost | soft | danger
// size: sm | md | lg
export default function Button({
  variant = 'primary',
  size = 'md',
  icon,
  full = false,
  loading = false,
  className,
  children,
  disabled,
  type = 'button',
  ...rest
}) {
  return (
    <button
      type={type}
      className={cx('btn', `btn--${variant}`, `btn--${size}`, full && 'btn--full', className)}
      disabled={disabled || loading}
      {...rest}
    >
      {loading ? <span className="btn__spinner" aria-hidden="true" /> : icon && <Icon name={icon} size={18} />}
      {children}
    </button>
  )
}
