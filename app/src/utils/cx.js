// دمج أسماء الأصناف مع تجاهل القيم الفارغة
export const cx = (...parts) => parts.filter(Boolean).join(' ')
