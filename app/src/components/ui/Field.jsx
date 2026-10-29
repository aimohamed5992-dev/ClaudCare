import { useId, useState } from 'react'
import { cx } from '../../utils/cx'
import Icon from './Icon'

// غلاف الحقل: تسمية + رسالة مساعدة/خطأ
export function Field({ label, hint, error, htmlFor, children, className }) {
  return (
    <div className={cx('field', error && 'field--error', className)}>
      {label && (
        <label className="field__label" htmlFor={htmlFor}>
          {label}
        </label>
      )}
      {children}
      {(error || hint) && <p className="field__msg">{error || hint}</p>}
    </div>
  )
}

export function Input({ label, hint, error, icon, type = 'text', className, ...rest }) {
  const id = useId()
  const [show, setShow] = useState(false)
  const isPassword = type === 'password'
  return (
    <Field label={label} hint={hint} error={error} htmlFor={id} className={className}>
      <div className={cx('control', icon && 'control--icon')}>
        {icon && <Icon name={icon} size={18} className="control__icon" />}
        <input id={id} className="control__input" type={isPassword && show ? 'text' : type} {...rest} />
        {isPassword && (
          <button type="button" className="control__toggle" onClick={() => setShow((s) => !s)} aria-label="إظهار كلمة المرور">
            <Icon name="eye" size={18} />
          </button>
        )}
      </div>
    </Field>
  )
}

export function Select({ label, hint, error, options = [], className, ...rest }) {
  const id = useId()
  return (
    <Field label={label} hint={hint} error={error} htmlFor={id} className={className}>
      <div className="control control--select">
        <select id={id} className="control__input" {...rest}>
          {options.map((o) => {
            const opt = typeof o === 'string' ? { value: o, label: o } : o
            return (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            )
          })}
        </select>
        <Icon name="chevronDown" size={18} className="control__chevron" />
      </div>
    </Field>
  )
}

export function Textarea({ label, hint, error, rows = 4, className, ...rest }) {
  const id = useId()
  return (
    <Field label={label} hint={hint} error={error} htmlFor={id} className={className}>
      <div className="control">
        <textarea id={id} className="control__input control__input--area" rows={rows} {...rest} />
      </div>
    </Field>
  )
}

export function Checkbox({ label, className, ...rest }) {
  return (
    <label className={cx('check', className)}>
      <input type="checkbox" {...rest} />
      <span className="check__box">
        <Icon name="check" size={14} strokeWidth={3} />
      </span>
      {label}
    </label>
  )
}

export function Switch({ label, className, ...rest }) {
  return (
    <label className={cx('switch', className)}>
      <input type="checkbox" role="switch" {...rest} />
      <span className="switch__track" />
      {label}
    </label>
  )
}
