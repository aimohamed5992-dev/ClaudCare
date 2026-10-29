// مسار الصور داخل public/assets.
// الصور مضغوطة بصيغة WebP؛ تبقى الأسماء في الكود بامتداداتها الأصلية (png/jpg)
// ويُحوَّل الامتداد هنا في مكان واحد.
export const asset = (path) => `/assets/${path.replace(/\.(png|jpe?g)$/i, '.webp')}`

export const logo = asset('logo.png')
