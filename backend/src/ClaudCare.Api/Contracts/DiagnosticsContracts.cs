using System.ComponentModel.DataAnnotations;

namespace ClaudCare.Api.Contracts;

/// <summary>نتيجة بند واحد. الوحدة والمدى المرجعي يُؤخذان من الكتالوج إن لم يُرسلا؛ العلم (طبيعي/مرتفع/حرج) يحسبه الخادم.</summary>
public sealed record LabResultInput([Required, StringLength(120)] string Name, [Required] decimal? Value, [StringLength(30)] string? Unit, decimal? RefMin, decimal? RefMax, [StringLength(200)] string? Remark);
public sealed record EnterLabResultsRequest([Required, MinLength(1), MaxLength(60)] List<LabResultInput> Items);
public sealed record CancelOrderRequest([Required, StringLength(300, MinimumLength = 2)] string Reason);

public sealed record ScheduleImagingRequest([Required] DateTime? ScheduledAt, int? DeviceId);
public sealed record ImagingReportRequest([Required, StringLength(5000, MinimumLength = 2)] string Findings, [Required, StringLength(500, MinimumLength = 2)] string Impression, [StringLength(300)] string? ImageKey, bool Final = false);
public sealed record ImagingDeviceDto(int Id, string Name, string Modality, bool IsAvailable);
