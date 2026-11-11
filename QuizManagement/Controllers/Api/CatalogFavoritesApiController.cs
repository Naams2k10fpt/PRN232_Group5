using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Mvc;
using QuizManagement.ApiModels;
using QuizManagement.Infrastructure;
using Services;

namespace QuizManagement.Controllers.Api;

[ApiController]
[Route("api/favorites/{kind}")]
[ApiAuthorize(Policy = "StudyContent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CatalogFavoritesApiController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<int[]>> List(string kind, CancellationToken cancellationToken)
    {
        if (ParseKind(kind) is not FavoriteKind itemKind)
            return NotFound(UnknownKind());

        return Ok(await catalog.GetFavoritesAsync(CurrentUserId, itemKind, cancellationToken));
    }

    [HttpPut("{id:int:min(1)}")]
    public async Task<IActionResult> Set(
        string kind, int id, FavoriteRequest request, CancellationToken cancellationToken)
    {
        if (ParseKind(kind) is not FavoriteKind itemKind)
            return NotFound(UnknownKind());

        // Prevent an in-flight write for a previous session from changing the new account.
        if (request.AccountId != CurrentUserId)
            return AccountChanged();

        var saved = await catalog.SetFavoriteAsync(
            CurrentUserId, itemKind, id, request.Favorite!.Value, cancellationToken);
        return saved
            ? NoContent()
            : NotFound(new ApiError("favorite_item_not_found", "Không tìm thấy môn học hoặc bộ đề."));
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import(
        string kind, ImportFavoritesRequest request, CancellationToken cancellationToken)
    {
        if (ParseKind(kind) is not FavoriteKind itemKind)
            return NotFound(UnknownKind());

        if (request.AccountId != CurrentUserId)
            return AccountChanged();

        await catalog.ImportFavoritesAsync(CurrentUserId, itemKind, request.Ids, cancellationToken);
        return NoContent();
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Không tìm thấy UserId trong token.");

    private ObjectResult AccountChanged() => StatusCode(
        StatusCodes.Status403Forbidden,
        new ApiError("favorite_account_changed", "Tài khoản đã thay đổi. Hãy tải lại danh sách."));

    private static ApiError UnknownKind() => new("favorite_kind_not_found", "Loại yêu thích không hợp lệ.");

    private static FavoriteKind? ParseKind(string kind) => kind switch
    {
        "subjects" => FavoriteKind.Subject,
        "decks" => FavoriteKind.Deck,
        _ => null
    };
}

public sealed record FavoriteRequest([Required] string AccountId, [Required] bool? Favorite);
public sealed record ImportFavoritesRequest([Required] string AccountId, [Required, MaxLength(500)] int[] Ids);
