import { Component } from 'react'
import { logo } from '../utils/assets'

// يمنع انهيار التطبيق كله عند خطأ غير متوقع في أي شاشة
export default class ErrorBoundary extends Component {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  componentDidCatch(error, info) {
    console.error('ClaudCare UI error:', error, info?.componentStack)
  }

  render() {
    if (!this.state.failed) return this.props.children
    return (
      <div className="fullpage" role="alert" style={{ textAlign: 'center' }}>
        <div>
          <img src={logo} alt="ClaudCare" style={{ width: 180, margin: '0 auto 16px' }} />
          <h2>حدث خطأ غير متوقع</h2>
          <p style={{ color: 'var(--muted)', margin: '8px 0 20px' }}>نعتذر عن ذلك. جرّب إعادة تحميل الصفحة.</p>
          <button className="btn btn--blue btn--md" onClick={() => window.location.reload()}>
            إعادة التحميل
          </button>
        </div>
      </div>
    )
  }
}
