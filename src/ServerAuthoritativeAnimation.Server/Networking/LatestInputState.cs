using ServerAuthoritativeAnimation.Core;

namespace ServerAuthoritativeAnimation.Server.Networking;

internal sealed class LatestInputState
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private MovementInput _latestInput;
    private long _lastUpdateTimestamp;
    private bool _hasReceivedInput;

    public LatestInputState(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public void Update(MovementInput input)
    {
        lock (_gate)
        {
            _latestInput = input.ClampToUnit();
            _lastUpdateTimestamp = _timeProvider.GetTimestamp();
            _hasReceivedInput = true;
        }
    }

    public MovementInput ReadActive(TimeSpan timeout)
    {
        lock (_gate)
        {
            if (!_hasReceivedInput ||
                _timeProvider.GetElapsedTime(_lastUpdateTimestamp, _timeProvider.GetTimestamp()) >= timeout)
            {
                return MovementInput.Zero;
            }

            return _latestInput;
        }
    }
}
