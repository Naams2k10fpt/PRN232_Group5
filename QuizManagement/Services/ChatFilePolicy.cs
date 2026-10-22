namespace QuizManagement.Services;

public static class ChatFilePolicy
{
    public const int MaxFiles = 3;
    public const int MaxFileBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif", [".webp"] = "image/webp", [".pdf"] = "application/pdf",
        [".txt"] = "text/plain", [".csv"] = "text/csv", [".zip"] = "application/zip",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public static async Task<ChatUpload> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        name = new string(name.Where(character => !char.IsControl(character)).ToArray());
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (name.Length is < 1 or > 180 || !Types.TryGetValue(extension, out var type))
            throw new ChatFailure("invalid_file_type", "Hỗ trợ ảnh PNG/JPG/GIF/WebP, PDF, TXT/CSV, ZIP và tài liệu Office.");
        if (file.Length is < 1 or > MaxFileBytes)
            throw new ChatFailure("file_too_large", "Mỗi file cần từ 1 byte đến 5 MB.", 413);
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaxFileBytes)
                throw new ChatFailure("file_too_large", "File vượt quá 5 MB.", 413);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var bytes = output.ToArray();
        var validImage = extension switch
        {
            ".png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
            ".gif" => bytes.AsSpan().StartsWith("GIF87a"u8) || bytes.AsSpan().StartsWith("GIF89a"u8),
            ".webp" => bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => true
        };
        if (!validImage) throw new ChatFailure("invalid_image", "Nội dung file không đúng định dạng ảnh.");
        return new(name, type, bytes);
    }
}
