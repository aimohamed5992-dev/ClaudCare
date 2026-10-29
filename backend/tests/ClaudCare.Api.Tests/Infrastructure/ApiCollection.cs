namespace ClaudCare.Api.Tests.Infrastructure;

/// <summary>كل اختبارات التكامل تشترك في نسخة واحدة من التطبيق وقاعدة claudcare_test (تُحذف مرة واحدة في النهاية).</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
