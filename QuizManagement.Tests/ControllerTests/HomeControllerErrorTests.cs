using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QuizManagement.Controllers;
using QuizManagement.ViewModels.Home;
using Xunit;

namespace QuizManagement.Tests.ControllerTests;

public class HomeControllerErrorTests
{
    [Fact]
    public void Error_ReturnsSafe500ViewWithRequestId_AndLogsException()
    {
        var logger = new RecordingLogger<HomeController>();
        var httpContext = new DefaultHttpContext { TraceIdentifier = "request-123" };
        var exception = new InvalidOperationException("sensitive detail");
        httpContext.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Error = exception,
            Path = "/boom"
        });
        var controller = new HomeController(logger, null!, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("request-123", model.RequestId);
        Assert.DoesNotContain("sensitive detail", model.RequestId);
        Assert.Same(exception, Assert.Single(logger.Exceptions));
    }

    [Fact]
    public void Error_IsAnonymousAndNeverCached()
    {
        var action = typeof(HomeController).GetMethod(nameof(HomeController.Error))!;

        var allowAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>();
        var cache = action.GetCustomAttribute<ResponseCacheAttribute>();

        Assert.NotNull(allowAnonymous);
        Assert.NotNull(cache);
        Assert.True(cache!.NoStore);
        Assert.Equal(ResponseCacheLocation.None, cache.Location);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<Exception> Exceptions { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null)
            {
                Exceptions.Add(exception);
            }
        }
    }
}
