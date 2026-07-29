namespace backend.Services.Production;

public sealed class ProductionStatisticsEngine
{
    private readonly Dictionary<MeasurementZone, ZoneAccumulator> _overall = CreateAccumulators();
    private readonly Dictionary<MeasurementZone, ZoneAccumulator> _auto = CreateAccumulators();
    private readonly Dictionary<MeasurementZone, ZoneAccumulator> _manual = CreateAccumulators();
    private readonly Dictionary<MeasurementZone, ZoneLiveState> _live = CreateLiveStates();

    private ProductionRunMetadata? _metadata;
    private DateTime? _lastTimestampUtc;
    private ControlMode _currentMode;
    private double _currentProductionLength;
    private double _autoTimeSeconds;
    private double _manualTimeSeconds;

    public bool IsActive => _metadata is not null;

    public void StartRun(ProductionRunMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        Reset();
        _metadata = metadata;
        _lastTimestampUtc = metadata.StartTimeUtc;
        _currentMode = ControlMode.Auto;
    }

    public void ApplySample(ProductionTelemetrySample sample)
    {
        EnsureRunIsActive();

        if (_lastTimestampUtc is DateTime previous)
        {
            var elapsedSeconds = Math.Max(0d, (sample.TimestampUtc - previous).TotalSeconds);
            if (sample.Mode == ControlMode.Auto)
            {
                _autoTimeSeconds += elapsedSeconds;
            }
            else
            {
                _manualTimeSeconds += elapsedSeconds;
            }
        }

        _currentMode = sample.Mode;
        _currentProductionLength = sample.ProductionLength;
        _lastTimestampUtc = sample.TimestampUtc;

        foreach (var zone in Enum.GetValues<MeasurementZone>())
        {
            if (!sample.Zones.TryGetValue(zone, out var zoneSample))
            {
                continue;
            }

            var modeAccumulator = sample.Mode == ControlMode.Auto ? _auto : _manual;

            if (Math.Abs(zoneSample.Setpoint) < 0.000001d)
            {
                _overall[zone].IncrementSkipped();
                modeAccumulator[zone].IncrementSkipped();
                _live[zone].SetInvalid(zoneSample.Setpoint, zoneSample.Actual);
                continue;
            }

            var percentDeviation = ((zoneSample.Actual - zoneSample.Setpoint) / zoneSample.Setpoint) * 100d;
            _overall[zone].Apply(percentDeviation);
            modeAccumulator[zone].Apply(percentDeviation);
            _live[zone].Set(zoneSample.Setpoint, zoneSample.Actual, percentDeviation);
        }
    }

    public ProductionStatisticsSnapshot GetSnapshot()
    {
        EnsureRunIsActive();

        var totalControlTime = _autoTimeSeconds + _manualTimeSeconds;
        var autoPct = totalControlTime <= 0d ? 0d : (_autoTimeSeconds / totalControlTime) * 100d;
        var manualPct = totalControlTime <= 0d ? 0d : (_manualTimeSeconds / totalControlTime) * 100d;

        return new ProductionStatisticsSnapshot
        {
            Metadata = _metadata!,
            LastUpdateUtc = _lastTimestampUtc ?? _metadata!.StartTimeUtc,
            CurrentMode = _currentMode,
            CurrentProductionLength = _currentProductionLength,
            AutoTimeSeconds = _autoTimeSeconds,
            ManualTimeSeconds = _manualTimeSeconds,
            TotalControlTimeSeconds = totalControlTime,
            AutoPercentage = autoPct,
            ManualPercentage = manualPct,
            LiveZones = _live.ToDictionary(pair => pair.Key, pair => pair.Value.ToSnapshot()),
            OverallZones = _overall.ToDictionary(pair => pair.Key, pair => pair.Value.ToSnapshot()),
            AutoQuality = new ModeQualitySnapshot
            {
                Zones = _auto.ToDictionary(pair => pair.Key, pair => pair.Value.ToSnapshot()),
            },
            ManualQuality = new ModeQualitySnapshot
            {
                Zones = _manual.ToDictionary(pair => pair.Key, pair => pair.Value.ToSnapshot()),
            },
        };
    }

    public CompletedProductionRecord CompleteRun(DateTime endTimeUtc)
    {
        EnsureRunIsActive();

        var snapshot = GetSnapshot();
        _metadata = null;

        return new CompletedProductionRecord
        {
            Snapshot = snapshot,
            EndTimeUtc = endTimeUtc,
        };
    }

    private void EnsureRunIsActive()
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("A production run must be started before applying samples.");
        }
    }

    private void Reset()
    {
        foreach (var accumulator in _overall.Values)
        {
            accumulator.Reset();
        }

        foreach (var accumulator in _auto.Values)
        {
            accumulator.Reset();
        }

        foreach (var accumulator in _manual.Values)
        {
            accumulator.Reset();
        }

        foreach (var liveState in _live.Values)
        {
            liveState.Reset();
        }

        _lastTimestampUtc = null;
        _currentProductionLength = 0d;
        _autoTimeSeconds = 0d;
        _manualTimeSeconds = 0d;
    }

    private static Dictionary<MeasurementZone, ZoneAccumulator> CreateAccumulators()
    {
        return Enum
            .GetValues<MeasurementZone>()
            .ToDictionary(zone => zone, _ => new ZoneAccumulator());
    }

    private static Dictionary<MeasurementZone, ZoneLiveState> CreateLiveStates()
    {
        return Enum
            .GetValues<MeasurementZone>()
            .ToDictionary(zone => zone, _ => new ZoneLiveState());
    }

    private sealed class ZoneAccumulator
    {
        private long _count;
        private long _skipped;
        private double _absSum;
        private double _maxPositive = double.NegativeInfinity;
        private double _maxNegative = double.PositiveInfinity;
        private double _current;

        public void Apply(double percentDeviation)
        {
            _count++;
            _absSum += Math.Abs(percentDeviation);
            _current = percentDeviation;
            _maxPositive = Math.Max(_maxPositive, percentDeviation);
            _maxNegative = Math.Min(_maxNegative, percentDeviation);
        }

        public void IncrementSkipped()
        {
            _skipped++;
        }

        public ZoneStatisticsSnapshot ToSnapshot()
        {
            return new ZoneStatisticsSnapshot
            {
                MeasurementCount = _count,
                SkippedCount = _skipped,
                RunningAbsoluteDeviationSum = _absSum,
                AverageAbsoluteDeviation = _count == 0 ? 0d : _absSum / _count,
                MaxPositiveDeviation = _count == 0 ? 0d : _maxPositive,
                MaxNegativeDeviation = _count == 0 ? 0d : _maxNegative,
                CurrentDeviation = _count == 0 ? 0d : _current,
            };
        }

        public void Reset()
        {
            _count = 0;
            _skipped = 0;
            _absSum = 0d;
            _maxPositive = double.NegativeInfinity;
            _maxNegative = double.PositiveInfinity;
            _current = 0d;
        }
    }

    private sealed class ZoneLiveState
    {
        private double _setpoint;
        private double _actual;
        private double _percentDeviation;

        public void Set(double setpoint, double actual, double percentDeviation)
        {
            _setpoint = setpoint;
            _actual = actual;
            _percentDeviation = percentDeviation;
        }

        public void SetInvalid(double setpoint, double actual)
        {
            _setpoint = setpoint;
            _actual = actual;
            _percentDeviation = 0d;
        }

        public ZoneLiveSnapshot ToSnapshot()
        {
            return new ZoneLiveSnapshot
            {
                CurrentSetpoint = _setpoint,
                CurrentActual = _actual,
                CurrentPercentDeviation = _percentDeviation,
            };
        }

        public void Reset()
        {
            _setpoint = 0d;
            _actual = 0d;
            _percentDeviation = 0d;
        }
    }
}
