using ClaudCare.Api.Contracts;
using ClaudCare.Api.Domain;

namespace ClaudCare.Api.Services.Clinical;

public sealed record LabTestDef(string Key, string Name, string Category, IReadOnlyList<LabTestItemDef> Items);
public sealed record LabTestItemDef(string Name, string Unit, decimal? Min, decimal? Max);
public sealed record ImagingDef(string Key, string Name, string Modality);

/// <summary>دليل الفحوصات: تعريفات المختبر مع المعدلات الطبيعية (تُستخدم عند إدخال النتائج) والأشعة.</summary>
public static class ClinicalCatalog
{
    public static readonly IReadOnlyList<LabTestDef> LabTests =
    [
        new("cbc", "صورة دم كاملة (CBC)", "دم",
        [
            new("الهيموجلوبين", "جم/دل", 12, 16), new("كريات الدم البيضاء", "×10³/µL", 4, 11),
            new("الصفائح الدموية", "×10³/µL", 150, 400), new("الهيماتوكريت", "%", 36, 48),
        ]),
        new("hba1c", "السكر التراكمي (HbA1c)", "سكري", [new("السكر التراكمي", "%", 4, 5.6m)]),
        new("fasting-glucose", "سكر صائم", "سكري", [new("سكر صائم", "مجم/دل", 70, 99)]),
        new("kidney", "وظائف الكلى", "كلى", [new("كرياتينين", "مجم/دل", 0.6m, 1.2m), new("يوريا", "مجم/دل", 15, 45)]),
        new("liver", "وظائف الكبد", "كبد", [new("ALT", "U/L", 7, 56), new("AST", "U/L", 10, 40), new("بيليروبين كلي", "مجم/دل", 0.1m, 1.2m)]),
        new("lipids", "دهون الدم", "قلب", [new("كوليسترول كلي", "مجم/دل", 0, 200), new("LDL", "مجم/دل", 0, 130), new("HDL", "مجم/دل", 40, 100), new("دهون ثلاثية", "مجم/دل", 0, 150)]),
        new("thyroid", "وظائف الغدة الدرقية", "غدد", [new("TSH", "mIU/L", 0.4m, 4.0m)]),
        new("vitamin-d", "فيتامين د", "فيتامينات", [new("فيتامين د", "نانوجرام/مل", 30, 100)]),
        new("electrolytes", "الأملاح", "دم", [new("صوديوم", "ميلي مول/لتر", 135, 145), new("بوتاسيوم", "ميلي مول/لتر", 3.5m, 5.1m)]),
    ];

    public static readonly IReadOnlyList<ImagingDef> ImagingStudies =
    [
        new("chest-xray", "أشعة سينية للصدر", "X-Ray"),
        new("knee-xray", "أشعة سينية للركبة", "X-Ray"),
        new("ecg", "تخطيط قلب", "ECG"),
        new("mri-brain", "رنين مغناطيسي للمخ", "MRI"),
        new("ct-abdomen", "أشعة مقطعية للبطن", "CT"),
        new("us-abdomen", "سونار البطن", "Ultrasound"),
    ];

    public static LabTestDef? Lab(string key) => LabTests.FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    public static ImagingDef? Imaging(string key) => ImagingStudies.FirstOrDefault(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>تصنيف قيمة التحليل (نفس منطق تطبيق المريض): حرج إن قلّت عن 60% من الحد الأدنى أو زادت عن 160% من الأعلى.</summary>
public static class ResultFlagCalculator
{
    public static ResultFlag Flag(decimal value, decimal? min, decimal? max)
    {
        if (min is { } lo && value < lo * 0.6m) return ResultFlag.Critical;
        if (max is { } hi && value > hi * 1.6m) return ResultFlag.Critical;
        if (min is { } l && value < l) return ResultFlag.Low;
        if (max is { } h && value > h) return ResultFlag.High;
        return ResultFlag.Normal;
    }
}

/// <summary>أوقات الجرعات اليومية (بالدقائق) حسب عدد المرات.</summary>
public static class MedicationSchedule
{
    public static int[] Times(int timesPerDay) => timesPerDay switch
    {
        1 => [8 * 60],
        2 => [8 * 60, 20 * 60],
        3 => [8 * 60, 14 * 60, 20 * 60],
        4 => [6 * 60, 12 * 60, 18 * 60, 22 * 60],
        _ => [], // عند الحاجة: بلا جدول
    };

    public static string ToCsv(IEnumerable<int> times) => string.Join(',', times);
    public static int[] FromCsv(string csv) => csv.Length == 0 ? [] : csv.Split(',').Select(int.Parse).ToArray();
}
