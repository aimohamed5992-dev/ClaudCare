namespace ClaudCare.Api.Domain;

public abstract class Entity
{
    public int Id { get; set; }
}

/// <summary>كيان يُختم تلقائيًا بتاريخ الإنشاء/التعديل (UTC) عند الحفظ.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

public abstract class AuditedEntity : Entity, IAuditable
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
