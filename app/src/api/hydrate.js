// يملأ مصفوفات البيانات التي تقرؤها صفحات كل دور من الخادم (استبدال في المكان)، فتعمل الصفحات دون تغيير.
import { api } from './client'
import * as m from './mappers'
import { labRequests } from '../data/lab'
import { radRequests } from '../data/radiology'
import { inventory, purchaseOrders, rxQueue, sales } from '../data/pharmacy'
import { doctorAppointments, doctorPatients } from '../data/doctor'
import { bookings, queue, receptionInvoices } from '../data/reception'
import { expenses, invoices as financeInvoices } from '../data/finance'
import { employees, leaveRequests } from '../data/hr'
import { approvals, claims } from '../data/insurance'
import { ambulances, erPatients } from '../data/emergency'
import { pendingAdmissions } from '../data/admissions'
import { cases } from '../data/surgery'
import { myAppointments, patientInvoices } from '../data/patient'

const P = { pageSize: 50 }
const win = () => ({ from: m.todayIso(), to: m.plusDays(30), pageSize: 50 })

// لكل دور: مصفوفة الهدف + دالة تجلب الصفوف جاهزة الشكل
const SOURCES = {
  lab: [[labRequests, async () => m.mapLabRequests(await api.get('/api/lab-orders', P))]],
  radiology: [[radRequests, async () => m.mapRadRequests(await api.get('/api/imaging-orders', P))]],
  pharmacy: [
    [rxQueue, async () => {
      const [open, done, rejected] = await Promise.all(['', 'Dispensed', 'Rejected'].map((s) => api.get('/api/pharmacy/prescriptions', { ...P, status: s || undefined })))
      return [...m.mapRx(open), ...m.mapRx(done), ...m.mapRx(rejected)]
    }],
    [inventory, async () => m.mapInventory(await api.get('/api/pharmacy/inventory', P))],
    [purchaseOrders, async () => m.mapPurchaseOrders(await api.get('/api/pharmacy/purchase-orders', P))],
    [sales, async () => m.mapSales(await api.get('/api/pharmacy/sales', P))],
  ],
  doctor: [
    [doctorAppointments, async () => m.mapDoctorAppointments(await api.get('/api/appointments', win()))],
    [doctorPatients, async () => m.mapDoctorPatients(await api.get('/api/patients', P))],
  ],
  reception: [
    [queue, async () => m.mapQueue(await api.get('/api/reception/queue'))],
    [bookings, async () => m.mapBookings(await api.get('/api/appointments', win()))],
    [receptionInvoices, async () => m.mapReceptionInvoices(await api.get('/api/invoices', P))],
  ],
  finance: [
    [financeInvoices, async () => m.mapFinanceInvoices(await api.get('/api/invoices', P))],
    [expenses, async () => m.mapExpenses(await api.get('/api/finance/expenses', P))],
  ],
  hr: [
    [employees, async () => m.mapEmployees(await api.get('/api/hr/employees', P))],
    [leaveRequests, async () => {
      const [l, e] = await Promise.all([api.get('/api/hr/leaves', { status: 'Pending' }), api.get('/api/hr/employees', P)])
      return m.mapLeaves(l, m.mapEmployees(e))
    }],
  ],
  insurance: [
    [approvals, async () => m.mapApprovals(await api.get('/api/insurance/pre-auths', P))],
    [claims, async () => m.mapClaims(await api.get('/api/insurance/claims', P))],
  ],
  emergency: [
    [erPatients, async () => m.mapErCases(await api.get('/api/emergency/cases'))],
    [ambulances, async () => m.mapAmbulances(await api.get('/api/emergency/ambulances'))],
  ],
  admissions: [[pendingAdmissions, async () => m.mapPendingAdmissions(await api.get('/api/admissions', { ...P, status: 'Pending' }))]],
  surgery: [[cases, async () => m.mapSurgeries(await api.get('/api/surgery/cases'))]],
  patient: [
    [myAppointments, async () => m.mapMyAppointments(await api.get('/api/appointments', P))],
    [patientInvoices, async () => m.mapMyInvoices(await api.get('/api/invoices', P))],
  ],
}

/** يجلب بيانات الدور ويستبدل المصفوفات. يعيد true إن تغيّر شيء، والأخطاء لا تمنع بقية المصادر. */
export async function hydrate(roleKey) {
  let changed = false
  const errors = []
  await Promise.all(
    (SOURCES[roleKey] ?? []).map(async ([target, load]) => {
      try {
        const rows = await load()
        if (JSON.stringify(rows) !== JSON.stringify(target)) {
          target.splice(0, target.length, ...rows)
          changed = true
        }
      } catch (e) {
        errors.push(e)
      }
    }),
  )
  if (errors.length && errors.every((e) => e.status === 401)) throw errors[0]
  return { changed, errors }
}

/** يفرّغ بيانات الدور (عند الخروج) كي لا يراها مستخدم آخر على الجهاز نفسه. */
export function clearHydrated(roleKey) {
  for (const [target] of SOURCES[roleKey] ?? []) target.splice(0, target.length)
}
