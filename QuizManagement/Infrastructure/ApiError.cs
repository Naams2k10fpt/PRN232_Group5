using System.Text.Json.Serialization;

namespace QuizManagement.Infrastructure;

// Error body for every /api/* failure. `Code` is stable and machine-readable (the mobile app switches on it);
// `Message` is Vietnamese text that can be shown as is. `Errors` holds per-field messages for validation
// failures, keyed by camelCase field name; `RetryAfterSeconds` is set when the client must wait.
public sealed record ApiError(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IDictionary<string, string[]>? Errors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RetryAfterSeconds = null);
