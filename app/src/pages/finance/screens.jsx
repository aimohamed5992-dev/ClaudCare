import RoleRouter from '../RoleRouter'
import { FinanceClose, FinanceCollect, FinanceDashboard, FinanceExpenses, FinanceInvoices, FinancePayroll } from './FinancePages'

// شاشات دور «finance»: المفتاح = مسار الصفحة ('' = الرئيسية). تُحمَّل هذه الحزمة عند الحاجة فقط.
const screens = {
  '': <FinanceDashboard />,
  invoices: <FinanceInvoices />,
  expenses: <FinanceExpenses />,
  payroll: <FinancePayroll />,
  close: <FinanceClose />,
  collect: <FinanceCollect />,
}

export default function FinanceScreens() {
  return <RoleRouter screens={screens} />
}
