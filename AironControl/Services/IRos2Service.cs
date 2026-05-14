namespace AironControl.Services
{
    public interface IRos2Service
    {
        Task<List<string>> GetPackagesAsync(CancellationToken ct = default);
        Task<List<string>> GetLaunchFilesAsync(string package, CancellationToken ct = default);
        Task<string> RunLaunchFileAsync(string package, string launchFile, CancellationToken ct = default);
        Task<List<string>> GetNodesAsync(CancellationToken ct = default);
        Task<List<string>> GetTopicsAsync(CancellationToken ct = default);
        Task<string> GetSystemInfoAsync(CancellationToken ct = default);
        Task<string> RebootRobotAsync(CancellationToken ct = default);
        Task<string> DeleteAllNodesAsync(CancellationToken ct = default);
        Task<string> GetTopicInfoAsync(string topicName, CancellationToken ct = default);
        Task<string> EchoTopicAsync(string topicName, int count, CancellationToken ct = default);
        Task<string> StopNodeAsync(string nodeName, CancellationToken ct = default);
        Task<string> GetNodeInfoAsync(string nodeName, CancellationToken ct = default);
        Task<string> GetNodeLogsAsync(string nodeName, int count, CancellationToken ct = default);
        Task<string> CheckHealthAsync(CancellationToken ct = default);
        Task<string> ReloadProgramAsync(CancellationToken ct = default);
    }
}
