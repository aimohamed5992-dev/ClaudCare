using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ClaudCare.Api.Domain;

public class Hospital : AuditedEntity
{
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(50)] public string? LicenseNo { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(200)] public string? Email { get; set; }
    [MaxLength(200)] public string? Website { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Branch> Branches { get; set; } = [];
}

public class Branch : AuditedEntity
{
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; } = null!;
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(80)] public string City { get; set; } = string.Empty;
    [MaxLength(250)] public string? Address { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;
}

[Index(nameof(BranchId), nameof(Name), IsUnique = true)]
public class Department : Entity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
}

[Index(nameof(Key), IsUnique = true)]
public class Specialty : Entity
{
    [MaxLength(40)] public string Key { get; set; } = string.Empty;
    [MaxLength(80)] public string NameAr { get; set; } = string.Empty;
    [MaxLength(80)] public string NameEn { get; set; } = string.Empty;
}

[Index(nameof(Key), IsUnique = true)]
public class SystemSetting : Entity
{
    [MaxLength(80)] public string Key { get; set; } = string.Empty;
    [MaxLength(1000)] public string Value { get; set; } = string.Empty;
    [MaxLength(40)] public string Category { get; set; } = "general";
}
