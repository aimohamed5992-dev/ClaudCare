using ClaudCare.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Data;

/// <summary>بيانات مرجعية تُضمن في كل البيئات (آمنة التكرار): دليل الأدوية بعائلاتها الدوائية للتحقق من الحساسية.</summary>
public sealed class ReferenceDataSeeder(ClaudCareDbContext db)
{
    // (الاسم، العائلة الدوائية، الشكل، الفئة، السعر)
    private static readonly (string Name, string Group, string Form, string Category, decimal Price)[] Drugs =
    [
        ("أموكسيسيلين 500 مجم", "البنسلين", "كبسولات", "مضادات حيوية", 3.5m),
        ("أزيثروميسين 500 مجم", "ماكروليد", "أقراص", "مضادات حيوية", 9m),
        ("باراسيتامول 500 مجم", "مسكن", "أقراص", "مسكنات", 0.6m),
        ("إيبوبروفين 400 مجم", "مضاد التهاب", "أقراص", "مسكنات", 1.2m),
        ("أسبرين 81 مجم", "الأسبرين", "أقراص", "الضغط", 0.4m),
        ("ميتفورمين 500 مجم", "السكري", "أقراص", "السكري", 0.9m),
        ("أملوديبين 5 مجم", "الضغط", "أقراص", "الضغط", 1.1m),
        ("أوميبرازول 20 مجم", "المعدة", "كبسولات", "الجهاز الهضمي", 2.1m),
        ("بخاخ سالبيوتامول", "الربو", "بخاخ", "الجهاز التنفسي", 14m),
        ("فيتامين د 50,000", "فيتامين", "كبسولات", "فيتامينات", 8m),
        ("ليفوثيروكسين 100 مكجم", "الغدة الدرقية", "أقراص", "غدد", 2.5m),
        ("فيوروسيميد 40 مجم", "مدرات البول", "أقراص", "القلب", 0.8m),
        ("أنسولين لانتوس", "السكري", "حقن", "السكري", 120m),
        ("كريم هيدروكورتيزون 1%", "كورتيزون موضعي", "كريم", "جلدية", 12m),
        ("سيتريزين 10 مجم", "مضاد حساسية", "أقراص", "حساسية", 1.4m),
        ("أوزيميبرازول", "المعدة", "كبسولات", "الجهاز الهضمي", 2.8m),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = (await db.Drugs.Select(d => d.Name).ToListAsync(ct)).ToHashSet();
        foreach (var d in Drugs.Where(d => !existing.Contains(d.Name)))
            db.Drugs.Add(new Drug { Name = d.Name, Group = d.Group, Form = d.Form, Category = d.Category, UnitPrice = d.Price });
        await db.SaveChangesAsync(ct);
    }
}
