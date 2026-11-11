using System.Security.Claims;
using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace QuizManagement.Controllers;

[Authorize(Policy = "StudyContent")]
public sealed class CatalogFavoritesController(ICatalogService catalog) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(
        string kind, int id, bool? favorite, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || favorite is null || id <= 0 || kind is not ("subject" or "deck"))
            return BadRequest();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản đăng nhập.");
        var itemKind = kind == "subject" ? FavoriteKind.Subject : FavoriteKind.Deck;
        if (!await catalog.SetFavoriteAsync(userId, itemKind, id, favorite.Value, cancellationToken))
            return NotFound();

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToAction("Index", "Subjects");
    }
}
