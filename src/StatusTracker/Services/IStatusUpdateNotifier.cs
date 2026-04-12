namespace StatusTracker.Services;

public interface IStatusUpdateNotifier
{
    event Action<int>? OnEndpointUpdated;
    void NotifyUpdate(int endpointId);
}

public class StatusUpdateNotifier : IStatusUpdateNotifier
{
    public event Action<int>? OnEndpointUpdated;

    public void NotifyUpdate(int endpointId)
    {
        var handler = OnEndpointUpdated;
        handler?.Invoke(endpointId);
    }
}
