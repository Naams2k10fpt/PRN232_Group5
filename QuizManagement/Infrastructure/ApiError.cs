namespace QuizManagement.Infrastructure;

// Error body for every /api/* failure. `Code` is stable and machine-readable (the mobile app switches on it);
// `Message` is Vietnamese text that can be shown as is. `Errors` holds per-field messages for validation failures.
public sealed record ApiError(string Code, string Message, IDictionary<string, string[]>? Errors = null);
