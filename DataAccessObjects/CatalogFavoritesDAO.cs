using BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects;

public static class CatalogFavoritesDAO
{
    public static Task<int[]> ListAsync(
        QuizManagementDbContext context, string userId, FavoriteKind kind, CancellationToken cancellationToken)
    {
        var ids = kind == FavoriteKind.Subject
            ? context.SubjectFavorites.Where(f => f.UserId == userId && f.IsFavorite).Select(f => f.SubjectId)
            : context.DeckFavorites.Where(f => f.UserId == userId && f.IsFavorite).Select(f => f.DeckId);

        return ids.OrderBy(id => id).ToArrayAsync(cancellationToken);
    }

    public static Task<int> SaveAsync(
        QuizManagementDbContext context, string userId, FavoriteKind kind, int[] ids, bool favorite,
        bool import, CancellationToken cancellationToken)
    {
        // Legacy imports only fill in unknown preferences. An explicit removal
        // leaves a false row so imports from other devices cannot restore it.
        FormattableString sql = kind == FavoriteKind.Subject
            ? (FormattableString)$"""
                INSERT INTO "SubjectFavorites" ("UserId", "SubjectId", "IsFavorite")
                SELECT {userId}, s."Id", {favorite}
                FROM "Subjects" s
                WHERE s."Id" = ANY ({ids}) AND NOT s."IsDeleted"
                ORDER BY s."Id"
                ON CONFLICT ("UserId", "SubjectId") DO UPDATE
                SET "IsFavorite" = EXCLUDED."IsFavorite" WHERE NOT {import}
                """
            : (FormattableString)$"""
                INSERT INTO "DeckFavorites" ("UserId", "DeckId", "IsFavorite")
                SELECT {userId}, d."Id", {favorite}
                FROM "Decks" d
                JOIN "Subjects" s ON s."Id" = d."SubjectId"
                WHERE d."Id" = ANY ({ids}) AND NOT d."IsDeleted" AND NOT s."IsDeleted"
                ORDER BY d."Id"
                ON CONFLICT ("UserId", "DeckId") DO UPDATE
                SET "IsFavorite" = EXCLUDED."IsFavorite" WHERE NOT {import}
                """;

        return context.Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);
    }
}
