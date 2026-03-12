using AgentBoard.Contracts;
using AgentBoard.Data.Models;
using AgentBoard.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;

namespace AgentBoard.Components.Pages;

public partial class Skills
{
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private List<Skill> _skills = [];
    private bool _loading = true;
    private string? _initError;

    // ── Files state ───────────────────────────────────────────────────────────
    private Guid? _expandedFilesSkillId;
    private readonly Dictionary<Guid, List<SkillFile>> _skillFiles = new();
    private readonly HashSet<Guid> _filesLoading = [];
    private Guid? _uploadingSkillId;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            await LoadSkillsAsync();
        }
        catch (Exception ex)
        {
            _initError = ex.Message;
            _loading = false;
        }
    }

    private async Task LoadSkillsAsync()
    {
        _loading = true;
        try
        {
            var skills = await Http.GetFromJsonAsync<List<Skill>>("api/skills");
            _skills = skills ?? [];
        }
        catch (Exception ex)
        {
            _initError = ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    // ── Files panel ───────────────────────────────────────────────────────────

    private async Task ToggleFilesAsync(Guid skillId)
    {
        if (_expandedFilesSkillId == skillId)
        {
            _expandedFilesSkillId = null;
            return;
        }

        _expandedFilesSkillId = skillId;

        if (!_skillFiles.ContainsKey(skillId))
        {
            await LoadFilesAsync(skillId);
        }
    }

    private async Task LoadFilesAsync(Guid skillId)
    {
        _filesLoading.Add(skillId);
        try
        {
            var files = await Http.GetFromJsonAsync<List<SkillFile>>($"api/skills/{skillId}/files");
            _skillFiles[skillId] = files ?? [];
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load files: {ex.Message}", Severity.Error);
            _skillFiles[skillId] = [];
        }
        finally
        {
            _filesLoading.Remove(skillId);
        }
    }

    private async Task TriggerFileUpload(Guid skillId)
    {
        await JS.InvokeVoidAsync("eval", $"document.getElementById('file-upload-{skillId}').click()");
    }

    private async Task OnFileSelected(InputFileChangeEventArgs e, Guid skillId)
    {
        var file = e.File;
        if (file is null) return;

        _uploadingSkillId = skillId;
        StateHasChanged();

        try
        {
            const long maxFileSize = 50L * 1024 * 1024; // 50 MB
            using var content = new MultipartFormDataContent();
            var stream = file.OpenReadStream(maxAllowedSize: maxFileSize);
            var streamContent = new StreamContent(stream);
            var mimeType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType;
            streamContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
            content.Add(streamContent, "file", file.Name);

            var response = await Http.PostAsync($"api/skills/{skillId}/files", content);
            response.EnsureSuccessStatusCode();

            Snackbar.Add($"Uploaded \"{file.Name}\" successfully", Severity.Success);

            // Refresh
            _skillFiles.Remove(skillId);
            await LoadFilesAsync(skillId);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Upload failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            _uploadingSkillId = null;
        }
    }

    private async Task DownloadFile(Guid skillId, Guid fileId, string fileName)
    {
        try
        {
            var url = $"api/skills/{skillId}/files/{fileId}/download";
            // Trigger browser download via an anchor click
            await JS.InvokeVoidAsync("eval",
                $"(function(){{" +
                $"var a=document.createElement('a');" +
                $"a.href='{url}';" +
                $"a.download='{fileName}';" +
                $"document.body.appendChild(a);" +
                $"a.click();" +
                $"document.body.removeChild(a);" +
                $"}})()");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Download failed: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeleteFile(Guid skillId, SkillFile file)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.ContentText, $"Delete \"{file.FileName}\"? This cannot be undone." },
            { x => x.ButtonText,  "Delete" }
        };
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Confirm Delete", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                var response = await Http.DeleteAsync($"api/skills/{skillId}/files/{file.Id}");
                response.EnsureSuccessStatusCode();
                Snackbar.Add($"Deleted \"{file.FileName}\"", Severity.Success);

                // Refresh
                _skillFiles.Remove(skillId);
                await LoadFilesAsync(skillId);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Delete failed: {ex.Message}", Severity.Error);
            }
        }
    }

    // ── Skills CRUD ───────────────────────────────────────────────────────────

    private async Task OpenCreateDialog()
    {
        var model = new Skill();
        var parameters = new DialogParameters<SkillDialog> { { x => x.Model, model } };
        var dialog = await DialogService.ShowAsync<SkillDialog>("New Skill", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Skill created })
        {
            try
            {
                var response = await Http.PostAsJsonAsync("api/skills", created);
                response.EnsureSuccessStatusCode();
                Snackbar.Add("Skill created", Severity.Success);
                await LoadSkillsAsync();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error creating skill: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task OpenEditDialog(Skill skill)
    {
        var model = new Skill
        {
            Id        = skill.Id,
            Name      = skill.Name,
            Content   = skill.Content,
            CreatedAt = skill.CreatedAt,
            UpdatedAt = skill.UpdatedAt
        };

        var parameters = new DialogParameters<SkillDialog> { { x => x.Model, model } };
        var dialog = await DialogService.ShowAsync<SkillDialog>("Edit Skill", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Skill updated })
        {
            try
            {
                var patch = new SkillPatch(updated.Name, updated.Content);
                var response = await Http.PatchAsJsonAsync($"api/skills/{updated.Id}", patch);
                response.EnsureSuccessStatusCode();
                Snackbar.Add("Skill updated", Severity.Success);
                await LoadSkillsAsync();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error updating skill: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task DeleteSkill(Skill skill)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.ContentText, $"Delete \"{skill.Name}\"? This cannot be undone." },
            { x => x.ButtonText,  "Delete" }
        };
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Confirm Delete", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            try
            {
                var response = await Http.DeleteAsync($"api/skills/{skill.Id}");
                response.EnsureSuccessStatusCode();
                Snackbar.Add("Skill deleted", Severity.Success);
                await LoadSkillsAsync();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error deleting skill: {ex.Message}", Severity.Error);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    internal static string FormatFileSize(long bytes)
    {
        const long kb = 1024;
        const long mb = 1024 * kb;
        const long gb = 1024 * mb;

        if (bytes < kb)  return $"{bytes} B";
        if (bytes < mb)  return $"{bytes / (double)kb:F1} KB";
        if (bytes < gb)  return $"{bytes / (double)mb:F1} MB";
        return $"{bytes / (double)gb:F1} GB";
    }

    internal static string GetContentTypeIcon(string contentType)
    {
        if (contentType.StartsWith("image/",  StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.Image;
        if (contentType.StartsWith("video/",  StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.VideoFile;
        if (contentType.StartsWith("audio/",  StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.AudioFile;
        if (contentType.Contains("pdf",        StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.PictureAsPdf;
        if (contentType.Contains("zip",        StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("compressed", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("tar",        StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("gzip",       StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.FolderZip;
        if (contentType.Contains("word",       StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("document",   StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.Description;
        if (contentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("excel",       StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.TableChart;
        if (contentType.StartsWith("text/",    StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.TextSnippet;
        if (contentType.Contains("json",       StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("xml",        StringComparison.OrdinalIgnoreCase)) return Icons.Material.Filled.Code;
        return Icons.Material.Filled.InsertDriveFile;
    }
}
