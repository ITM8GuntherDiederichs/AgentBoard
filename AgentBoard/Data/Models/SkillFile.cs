namespace AgentBoard.Data.Models;

/// <summary>Represents a reference file attached to a <see cref="Skill"/>.</summary>
public class SkillFile
{
    /// <summary>Unique identifier for this file record.</summary>
    public Guid Id { get; set; }

    /// <summary>The skill this file belongs to (plain Guid, no FK constraint).</summary>
    public Guid SkillId { get; set; }

    /// <summary>Original file name as supplied by the uploader.</summary>
    public string FileName { get; set; } = "";

    /// <summary>MIME content-type of the file (e.g. <c>application/pdf</c>).</summary>
    public string ContentType { get; set; } = "";

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Relative path under <c>wwwroot/</c> (e.g. <c>uploads/skills/{skillId}/{guid}.pdf</c>).
    /// Always uses forward slashes.
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>UTC timestamp when the file was uploaded.</summary>
    public DateTime UploadedAt { get; set; }
}
