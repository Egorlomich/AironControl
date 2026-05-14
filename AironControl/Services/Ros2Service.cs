using AironControl.Core;
using System.Diagnostics;

namespace AironControl.Services
{
    public class Ros2Service : IRos2Service
    {
        private readonly RobotConnectionManager _manager;

        public Ros2Service(RobotConnectionManager manager)
        {
            _manager = manager;
        }

        public async Task<List<string>> GetPackagesAsync(CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                "source /opt/ros/humble/setup.bash && ros2 pkg list 2>/dev/null", ct);

            if (output.StartsWith("Error:"))
                return new List<string> { output };

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        public async Task<List<string>> GetLaunchFilesAsync(string package, CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                $"source /opt/ros/humble/setup.bash && " +
                $"find $(ros2 pkg prefix {package} 2>/dev/null)/share/{package}/launch " +
                $"-maxdepth 1 -name '*.launch.py' -type f 2>/dev/null | xargs -I{{}} basename {{}} 2>/dev/null",
                ct);

            if (output.StartsWith("Error:") || string.IsNullOrWhiteSpace(output))
                return new List<string> { "No launch files found" };

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList();
        }

        public async Task<string> RunLaunchFileAsync(string package, string launchFile, CancellationToken ct = default)
        {
            var cmd = $"source /opt/ros/humble/setup.bash && " +
                      $"source ~/ros2_ws/install/setup.bash 2>/dev/null; " +
                      $"screen -dmS ros2_launch bash -c \"ros2 launch {package} {launchFile}\"";

            await _manager.ExecuteCommandAsync(cmd, ct);
            return $"Success: {package} {launchFile} started";
        }

        public async Task<List<string>> GetNodesAsync(CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                "source /opt/ros/humble/setup.bash && ros2 node list 2>/dev/null", ct);

            if (output.StartsWith("Error:") || string.IsNullOrWhiteSpace(output))
                return new List<string>();

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.StartsWith("/"))
                .ToList();
        }

        public async Task<List<string>> GetTopicsAsync(CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                "source /opt/ros/humble/setup.bash && ros2 topic list 2>/dev/null", ct);

            if (output.StartsWith("Error:") || string.IsNullOrWhiteSpace(output))
                return new List<string>();

            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.StartsWith("/"))
                .ToList();
        }

        public async Task<string> GetSystemInfoAsync(CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                "echo '=== OS ===' && uname -a && " +
                "echo '=== ROS2 ===' && source /opt/ros/humble/setup.bash && ros2 --version 2>/dev/null && " +
                "echo '=== CPU ===' && top -bn1 | grep 'Cpu(s)' && " +
                "echo '=== RAM ===' && free -h | head -2",
                ct);

            if (output.StartsWith("Error:"))
                return $"Robot: {_manager.CurrentRobot?.Info.Name}\nStatus: {_manager.CurrentRobot?.Info.Status}\n\n{output}";

            return output;
        }

        public async Task<string> RebootRobotAsync(CancellationToken ct = default)
        {
            await _manager.ExecuteCommandAsync("sudo reboot", ct);
            return "Success: Reboot command sent";
        }

        public async Task<string> DeleteAllNodesAsync(CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                "pkill -f 'ros2 launch' 2>/dev/null; " +
                "pkill -f 'ros2 run' 2>/dev/null; " +
                "screen -wipe 2>/dev/null; " +
                "echo 'Done'",
                ct);

            return string.IsNullOrWhiteSpace(output) ? "Все ноды остановлены" : output;
        }

        public async Task<string> GetTopicInfoAsync(string topicName, CancellationToken ct = default)
        {
            return await _manager.ExecuteCommandAsync(
                $"source /opt/ros/humble/setup.bash && ros2 topic info {topicName} 2>/dev/null", ct);
        }

        public async Task<string> EchoTopicAsync(string topicName, int count, CancellationToken ct = default)
        {
            return await _manager.ExecuteCommandAsync(
                $"source /opt/ros/humble/setup.bash && " +
                $"timeout 5 ros2 topic echo {topicName} --once 2>/dev/null",
                ct);
        }

        public async Task<string> StopNodeAsync(string nodeName, CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                $"source /opt/ros/humble/setup.bash && " +
                $"ros2 lifecycle set {nodeName} shutdown 2>/dev/null",
                ct);

            return output.StartsWith("Error:") ? output : $"Success: {nodeName} shutdown";
        }

        public async Task<string> GetNodeInfoAsync(string nodeName, CancellationToken ct = default)
        {
            return await _manager.ExecuteCommandAsync(
                $"source /opt/ros/humble/setup.bash && ros2 node info {nodeName} 2>/dev/null", ct);
        }

        public async Task<string> GetNodeLogsAsync(string nodeName, int count, CancellationToken ct = default)
        {
            return await _manager.ExecuteCommandAsync(
                $"source /opt/ros/humble/setup.bash && " +
                $"timeout 5 ros2 topic echo /rosout --once 2>/dev/null | head -{count * 10}",
                ct);
        }

        public async Task<string> CheckHealthAsync(CancellationToken ct = default)
        {
            return await _manager.ExecuteCommandAsync(
                "source /opt/ros/humble/setup.bash && ros2 doctor 2>/dev/null", ct);
        }

        public async Task<string> ReloadProgramAsync(CancellationToken ct = default)
        {
            var output = await _manager.ExecuteCommandAsync(
                "pkill -f 'ros2 launch' 2>/dev/null; " +
                "pkill -f 'ros2 run' 2>/dev/null; " +
                "source /opt/ros/humble/setup.bash && ros2 daemon restart 2>/dev/null; " +
                "echo 'Reload done'",
                ct);

            return output.StartsWith("Error:") ? output : $"Success: {output}";
        }
    }
}
