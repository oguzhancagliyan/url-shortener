using System.Diagnostics;
using UrlShortener.Infrastructure.Observability;

namespace UrlShortener.Infrastructure.Persistence.Common;

public abstract class BaseInstrumentedRepository
{
    protected static readonly ActivitySource ActivitySource = new(TelemetryConstants.ActivitySourceName);

    protected static Activity? StartActivity(string operation, string provider)
    {
        var activity = ActivitySource.StartActivity($"db.{operation}", ActivityKind.Client);
        activity?.SetTag("db.system", provider);
        return activity;
    }
}
