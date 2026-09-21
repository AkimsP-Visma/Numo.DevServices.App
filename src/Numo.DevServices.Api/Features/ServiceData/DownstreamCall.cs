using Numo.Common.Lib.Exceptions.Authorization;

namespace Numo.DevServices.Api.Features.ServiceData;

/// <summary>Carries a slice error out of a resource, which returns wire shapes and cannot return a
/// <see cref="NumoResult"/>. The two data handlers turn it back into a failed result.</summary>
public sealed class DownstreamCallException(NumoError error, Exception innerException)
    : Exception(error.Message, innerException)
{
    public NumoError Error { get; } = error;
}

/// <summary>
/// The one place a client library failure becomes a slice error. The Person client signals failure
/// by throwing; the Employee-family clients return a FluentResults result instead, so a resource
/// using those checks the result itself and throws <see cref="DownstreamCallException"/> with
/// <see cref="ServiceDataErrors.DownstreamCallFailed"/>. Neither style reaches the wire contract.
/// </summary>
internal static class DownstreamCall
{
    /// <summary>
    /// Wraps one call, or several composed into one lambda: a resource needing a batched lookup or a
    /// fan-out can nest these, and the innermost failure is the one that is reported.
    /// </summary>
    public static async Task<T> InvokeAsync<T>(Func<Task<T>> call, string callDescription)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (ShouldDescribe(exception))
        {
            throw new DownstreamCallException(Describe(exception, callDescription), exception);
        }
    }

    /// <summary>
    /// A by-id call whose absent record is an answer rather than a failure. Each client library
    /// throws its own not-found type - <c>PersonNotFoundException</c>, <c>EmployeeNotFoundException</c>
    /// and so on, none of them sharing a base - so the caller names the one its client throws.
    /// </summary>
    public static async Task<T?> FindAsync<T, TNotFoundException>(Func<Task<T>> call, string callDescription)
        where T : class
        where TNotFoundException : Exception
    {
        try
        {
            return await call();
        }
        catch (TNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (ShouldDescribe(exception))
        {
            throw new DownstreamCallException(Describe(exception, callDescription), exception);
        }
    }

    private static bool ShouldDescribe(Exception exception)
        => exception switch
        {
            // Already names its own failure: re-describing an inner call's error would erase the
            // specific id and status code a composed resource needs.
            DownstreamCallException => false,

            // A cancellation is the caller giving up, not a downstream failure. An HttpClient
            // timeout arrives the same way but carries a TimeoutException, and that is one.
            OperationCanceledException => exception.InnerException is TimeoutException,

            _ => true,
        };

    private static NumoError Describe(Exception exception, string callDescription)
        => exception switch
        {
            UnauthorizedException => ServiceDataErrors.DownstreamCallUnauthorized(callDescription),
            HttpRequestException httpRequestException =>
                ServiceDataErrors.DownstreamCallFailed(callDescription, (int?)httpRequestException.StatusCode),
            _ => ServiceDataErrors.DownstreamCallFailed(callDescription, null),
        };
}
