using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QuizManagement.Hubs;
using QuizManagement.Infrastructure;
using QuizManagement.Services;
using BusinessObjects;

namespace QuizManagement.Controllers;

[ApiController, Route("api/groups/{groupId:int}/channels"), ChatAuthorize]
public sealed class ChatChannelsController(ChatService chat, StudyGroupService groups, IHubContext<ChatHub> hub,
    VoiceRoomService voice) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ChatFailure error) { return StatusCode(error.Status, new ApiError(error.Code, error.Message)); }
    }
    private async Task Broadcast(int groupId, string name, object data) =>
        await hub.Clients.Users(await groups.RecipientIdsAsync(groupId)).SendAsync(name, data);

    [HttpGet]
    public Task<IActionResult> List(int groupId) => Run(async () => Ok(await chat.ChannelsAsync(groupId, UserId)));

    [HttpPost]
    public Task<IActionResult> Create(int groupId, ChannelRequest request) => Run(async () =>
    {
        var channel = await chat.SaveChannelAsync(groupId, null, UserId, request.Name, request.Topic, request.Kind);
        await Broadcast(groupId, "ChannelsChanged", groupId);
        return Created($"/api/groups/{groupId}/channels/{channel.Id}", channel);
    });

    [HttpPut("{channelId:int}")]
    public Task<IActionResult> Update(int groupId, int channelId, ChannelRequest request) => Run(async () =>
    {
        var channel = await chat.SaveChannelAsync(groupId, channelId, UserId, request.Name, request.Topic, request.Kind);
        await Broadcast(groupId, "ChannelsChanged", groupId);
        return Ok(channel);
    });

    [HttpDelete("{channelId:int}")]
    public Task<IActionResult> Archive(int groupId, int channelId) => Run(async () =>
    {
        await chat.ArchiveChannelAsync(groupId, channelId, UserId);
        await voice.CloseChannelAsync(groupId, channelId);
        await Broadcast(groupId, "ChannelsChanged", groupId);
        return NoContent();
    });

    [HttpGet("{channelId:int}/messages")]
    public Task<IActionResult> Messages(int groupId, int channelId, int? beforeId, int? afterId, int limit = 50) =>
        Run(async () => Ok(await chat.MessagesAsync(groupId, channelId, UserId, beforeId, afterId, limit)));

    [HttpPost("{channelId:int}/messages")]
    [RequestSizeLimit(16 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public Task<IActionResult> Send(int groupId, int channelId, [FromForm] ChatMessageForm request) => Run(async () =>
    {
        // Recheck membership before reading the bounded form-file streams.
        await chat.ChannelsAsync(groupId, UserId);
        if (request.Files.Count > ChatFilePolicy.MaxFiles) throw new ChatFailure("file_limit", "Tối đa 3 file mỗi tin nhắn.");
        var files = new List<ChatUpload>();
        foreach (var file in request.Files) files.Add(await ChatFilePolicy.ReadAsync(file, HttpContext.RequestAborted));
        var message = await chat.SendAsync(groupId, channelId, UserId, request.Content, request.ReplyToId, request.ClientMessageId, files);
        await Broadcast(groupId, "ReceiveMessage", message);
        return Ok(message);
    });

    [HttpPut("{channelId:int}/messages/{messageId:int}")]
    public Task<IActionResult> Edit(int groupId, int channelId, int messageId, EditChatMessageRequest request) => Run(async () =>
    {
        var message = await chat.ChangeMessageAsync(groupId, channelId, messageId, UserId, content: request.Content);
        await Broadcast(groupId, "MessageUpdated", message);
        return Ok(message);
    });

    [HttpDelete("{channelId:int}/messages/{messageId:int}")]
    public Task<IActionResult> Delete(int groupId, int channelId, int messageId) => Run(async () =>
    {
        var message = await chat.ChangeMessageAsync(groupId, channelId, messageId, UserId, delete: true);
        await Broadcast(groupId, "MessageUpdated", message);
        return Ok(message);
    });

    [HttpPut("{channelId:int}/messages/{messageId:int}/reaction")]
    public Task<IActionResult> React(int groupId, int channelId, int messageId, ChatReactionRequest request) => Run(async () =>
    {
        var message = await chat.ChangeMessageAsync(groupId, channelId, messageId, UserId, emoji: request.Emoji, react: request.Active);
        await Broadcast(groupId, "MessageUpdated", message);
        return Ok(message);
    });

    [HttpPost("{channelId:int}/read")]
    public Task<IActionResult> Read(int groupId, int channelId, MarkGroupReadRequest request) => Run(async () =>
    {
        await chat.MarkReadAsync(groupId, channelId, UserId, request.LastMessageId);
        return NoContent();
    });
}

[ApiController, ChatAuthorize]
public sealed class ChatAttachmentsController(ChatService chat) : ControllerBase
{
    [HttpGet("api/chat/attachments/{id:guid}")]
    public async Task<IActionResult> Download(Guid id, bool download = false)
    {
        try
        {
            var file = await chat.DownloadAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Content-Security-Policy"] = "sandbox";
            return file.ContentType.StartsWith("image/", StringComparison.Ordinal) && !download
                ? File(file.Data, file.ContentType) : File(file.Data, file.ContentType, file.FileName);
        }
        catch (ChatFailure error) { return StatusCode(error.Status, new ApiError(error.Code, error.Message)); }
    }
}

public sealed record ChannelRequest([Required, MaxLength(100)] string Name, [MaxLength(200)] string? Topic,
    ChatChannelKind? Kind = null);
public sealed record EditChatMessageRequest([Required, MaxLength(2000)] string Content);
public sealed record ChatReactionRequest([Required, MaxLength(16)] string Emoji, bool Active);
public sealed class ChatMessageForm
{
    [MaxLength(2000)] public string? Content { get; set; }
    public int? ReplyToId { get; set; }
    public Guid? ClientMessageId { get; set; }
    public List<IFormFile> Files { get; set; } = [];
}
