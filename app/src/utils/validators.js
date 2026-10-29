const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const PHONE = /^\+?\d[\d\s-]{7,14}$/

// يقبل بريدًا إلكترونيًا أو رقم جوال
const EMPLOYEE = /^[A-Za-z]{2,4}-\d{3,6}$/
const NATIONAL_ID = /^\d{10}$/

export const validateIdentity = (v) => {
  const value = v.trim()
  if (!value) return 'هذا الحقل مطلوب'
  if (!EMAIL.test(value) && !PHONE.test(value) && !EMPLOYEE.test(value) && !NATIONAL_ID.test(value)) return 'أدخل بريدًا إلكترونيًا أو جوالًا أو رقم موظف أو هوية صحيحة'
  return ''
}

export const validateEmail = (v) => {
  const value = v.trim()
  if (!value) return 'البريد الإلكتروني مطلوب'
  if (!EMAIL.test(value)) return 'صيغة البريد الإلكتروني غير صحيحة'
  return ''
}

export const validatePassword = (v) => {
  if (!v) return 'كلمة المرور مطلوبة'
  if (v.length < 8) return 'يجب ألا تقل كلمة المرور عن 8 أحرف'
  return ''
}

// قوة كلمة المرور من 0 إلى 4
export const passwordStrength = (v) => {
  let score = 0
  if (v.length >= 8) score++
  if (/[A-Zء-ي]/.test(v) && /[a-z\d]/.test(v)) score++
  if (/\d/.test(v)) score++
  if (/[^\w\s]/.test(v)) score++
  return score
}
