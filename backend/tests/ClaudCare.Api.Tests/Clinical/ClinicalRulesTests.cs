using ClaudCare.Api.Domain;
using ClaudCare.Api.Services.Clinical;

namespace ClaudCare.Api.Tests.Clinical;

public class ResultFlagCalculatorTests
{
    [Theory]
    [InlineData(14, 30, 100, ResultFlag.Critical)]   // أقل من 60% من الحد الأدنى (18)
    [InlineData(25, 30, 100, ResultFlag.Low)]
    [InlineData(50, 30, 100, ResultFlag.Normal)]
    [InlineData(30, 30, 100, ResultFlag.Normal)]     // الحد نفسه طبيعي
    [InlineData(100, 30, 100, ResultFlag.Normal)]
    [InlineData(112, 70, 99, ResultFlag.High)]
    [InlineData(160, 70, 99, ResultFlag.Critical)]   // > 160% من الأعلى (158.4)
    public void Classifies_like_the_patient_app(double v, double min, double max, ResultFlag expected) =>
        Assert.Equal(expected, ResultFlagCalculator.Flag((decimal)v, (decimal)min, (decimal)max));

    [Fact]
    public void Missing_bounds_are_ignored()
    {
        Assert.Equal(ResultFlag.Normal, ResultFlagCalculator.Flag(5, null, null));
        Assert.Equal(ResultFlag.High, ResultFlagCalculator.Flag(250, null, 200));
        Assert.Equal(ResultFlag.Low, ResultFlagCalculator.Flag(10, 12, null));
    }
}

public class ClinicalCatalogTests
{
    [Fact]
    public void Keys_are_unique_and_ranges_make_sense()
    {
        Assert.Equal(ClinicalCatalog.LabTests.Count, ClinicalCatalog.LabTests.Select(t => t.Key).Distinct().Count());
        Assert.Equal(ClinicalCatalog.ImagingStudies.Count, ClinicalCatalog.ImagingStudies.Select(t => t.Key).Distinct().Count());
        foreach (var i in ClinicalCatalog.LabTests.SelectMany(t => t.Items).Where(i => i.Min is not null && i.Max is not null))
            Assert.True(i.Min < i.Max, $"{i.Name}: الحد الأدنى يجب أن يكون أقل من الأعلى");
        Assert.All(ClinicalCatalog.LabTests, t => Assert.NotEmpty(t.Items));
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        Assert.NotNull(ClinicalCatalog.Lab("CBC"));
        Assert.NotNull(ClinicalCatalog.Imaging("Chest-XRay"));
        Assert.Null(ClinicalCatalog.Lab("none"));
    }
}

public class MedicationScheduleTests
{
    [Theory]
    [InlineData(0, new int[0])]
    [InlineData(1, new[] { 480 })]
    [InlineData(2, new[] { 480, 1200 })]
    [InlineData(3, new[] { 480, 840, 1200 })]
    [InlineData(4, new[] { 360, 720, 1080, 1320 })]
    [InlineData(9, new int[0])]
    public void Times_per_day(int perDay, int[] expected) => Assert.Equal(expected, MedicationSchedule.Times(perDay));

    [Fact]
    public void Csv_round_trips_including_empty()
    {
        Assert.Equal([480, 1200], MedicationSchedule.FromCsv(MedicationSchedule.ToCsv([480, 1200])));
        Assert.Empty(MedicationSchedule.FromCsv(MedicationSchedule.ToCsv([])));
    }
}
