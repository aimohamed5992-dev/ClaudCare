import { paths } from './iconPaths'

export default function Icon({ name, size = 20, strokeWidth = 1.8, className, ...rest }) {
  const body = paths[name]
  if (!body) return null
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      dangerouslySetInnerHTML={{ __html: body }}
      {...rest}
    />
  )
}
