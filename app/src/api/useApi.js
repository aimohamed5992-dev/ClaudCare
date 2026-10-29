import { useCallback, useEffect, useState } from 'react'
import { api } from './client'

/**
 * يجلب GET ويحوّله بـ map إلى شكل الصفوف الذي تتوقعه الصفحة.
 * يعيد { data, loading, error, reload }. تغيّر path أو deps يعيد الجلب.
 */
export default function useApi(path, map = (x) => x, deps = []) {
  const [state, setState] = useState({ data: null, loading: true, error: null })
  const [tick, setTick] = useState(0)
  const reload = useCallback(() => setTick((t) => t + 1), [])

  useEffect(() => {
    let live = true
    setState((s) => ({ ...s, loading: true, error: null }))
    api
      .get(path)
      .then((j) => live && setState({ data: map(j), loading: false, error: null }))
      .catch((e) => live && setState({ data: null, loading: false, error: e }))
    return () => {
      live = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [path, tick, ...deps])

  return { ...state, reload }
}
