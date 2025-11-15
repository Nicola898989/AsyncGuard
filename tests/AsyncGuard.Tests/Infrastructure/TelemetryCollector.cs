using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;

namespace AsyncGuard.Tests.Infrastructure;

internal sealed class TelemetryCollector : IDisposable
{
    private readonly List<Activity> _activities = new();
    private readonly List<LongMeasurement> _longMeasurements = new();
    private readonly List<DoubleMeasurement> _doubleMeasurements = new();
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public TelemetryCollector()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AsyncGuardTelemetry.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => _activities.Add(activity)
        };

        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener();
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AsyncGuardTelemetry.Meter.Name)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            lock (_longMeasurements)
            {
                _longMeasurements.Add(new LongMeasurement(instrument.Name, measurement, CloneTags(tags)));
            }
        });

        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            lock (_doubleMeasurements)
            {
                _doubleMeasurements.Add(new DoubleMeasurement(instrument.Name, measurement, CloneTags(tags)));
            }
        });

        _meterListener.Start();
    }

    public IReadOnlyList<Activity> Activities
    {
        get { lock (_activities) return _activities.ToList(); }
    }

    public IReadOnlyList<LongMeasurement> LongMeasurements
    {
        get { lock (_longMeasurements) return _longMeasurements.ToList(); }
    }

    public IReadOnlyList<DoubleMeasurement> DoubleMeasurements
    {
        get { lock (_doubleMeasurements) return _doubleMeasurements.ToList(); }
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private static KeyValuePair<string, object?>[] CloneTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (tags.Length == 0)
            return Array.Empty<KeyValuePair<string, object?>>();

        var copy = new KeyValuePair<string, object?>[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            copy[i] = tags[i];
        }

        return copy;
    }
}

internal readonly record struct LongMeasurement(string Name, long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags);

internal readonly record struct DoubleMeasurement(string Name, double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags);
