namespace ClaudCare.Api.Domain;

// تُخزَّن كلها كنصوص في قاعدة البيانات (قراءة أسهل وأمان عند إعادة ترتيب القيم).

public enum UserRole { Admin, Hospital, Branch, Lab, Radiology, Pharmacy, Doctor, Nurse, Reception, Insurance, Finance, Hr, Emergency, Admissions, Surgery, Patient }
public enum Gender { Male, Female }

public enum AppointmentStatus { Pending, Confirmed, Arrived, InProgress, Done, Cancelled, NoShow }
public enum VisitType { Clinic, Video, FollowUp }

public enum PrescriptionStatus { Pending, Preparing, Dispensed, Rejected }
public enum RefillStatus { None, Requested, Approved, Rejected }
public enum OrderPriority { Normal, Urgent }
public enum LabOrderStatus { Ordered, SampleCollected, InProgress, ResultReady, Cancelled }
public enum ImagingOrderStatus { Ordered, Scheduled, InProgress, Reported, Cancelled }
public enum ReportStatus { Draft, Final }
public enum ResultFlag { Normal, High, Low, Critical }

public enum BedStatus { Available, Occupied, Cleaning, Reserved }
public enum AdmissionStatus { Pending, Admitted, Discharged }
public enum AdmissionType { Emergency, Planned, Transfer }
public enum TransferStatus { WaitingBed, Approved, Done }
public enum EmergencyStatus { Waiting, InExam, Admitted, Discharged }
public enum AmbulanceStatus { EnRoute, Arrived }
public enum DoseStatus { Due, Given, Skipped, Delayed }
public enum QueueStatus { Waiting, Called, Done, Cancelled }

public enum OperatingRoomState { Available, InSurgery, Cleaning, Maintenance }
public enum SurgeryStatus { Scheduled, InProgress, Done, Cancelled }

public enum InvoiceStatus { Unpaid, PartiallyPaid, Paid, Covered, Cancelled }
public enum PaymentMethod { Card, Cash, ApplePay, BankTransfer, Insurance, PayAtClinic }
public enum ExpenseStatus { Pending, Approved, Paid, Rejected }
public enum PayrollStatus { Draft, PendingApproval, Paid }

public enum ApprovalStatus { Pending, MoreInfoNeeded, Approved, Rejected }
public enum ClaimStatus { Draft, Submitted, Paid, PartiallyPaid, Rejected }
public enum ContractStatus { Active, Renewing, Expired }

public enum EmployeeStatus { Active, OnLeave, Probation, Terminated }
public enum LeaveType { Annual, Sick, Emergency }
public enum LeaveStatus { Pending, Approved, Rejected }
public enum ShiftKind { Morning, Evening, Night }
public enum ShiftStatus { Confirmed, SwapRequested }

public enum PurchaseOrderStatus { Draft, Sent, PartiallyReceived, Received }
public enum SaleStatus { Paid, Refunded }

public enum ConversationKind { Direct, Support }
public enum NotificationType { Appointment, Result, Medication, Invoice, Message, Payment, Alert, System }
public enum OtpPurpose { Login2Fa, Register, ResetPassword, ChangePhone }
