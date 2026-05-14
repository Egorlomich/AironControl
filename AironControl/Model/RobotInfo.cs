using System;
using System.Collections.Generic;
using System.Text;

namespace AironControl.Model
{
    public class RobotInfo
    {
        public string ID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProtocolType { get; set; } = string.Empty;
        public string Status { get; set; } = "Disconnected";
        public object? Info { get; set; }

        public DateTime LastConnectionTime { get; set; } = DateTime.MinValue;
        public string? LastError { get; set; }
        public bool IsConnected => Status == "Connected" || Status == "Connecting";

        public RobotInfo()
        {
        }

        public RobotInfo(string id, string name, string protocolType)
        {
            ID = id;
            Name = name;
            ProtocolType = protocolType;
        }

        // Методы для удобства обновления состояния
        public void UpdateStatus(string newStatus, string? error = null)
        {
            Status = newStatus;
            if (error != null)
                LastError = error;

            if (newStatus == "Connected")
                LastConnectionTime = DateTime.Now;
        }

        public void Reset()
        {
            Status = "Disconnected";
            LastError = null;
            LastConnectionTime = DateTime.MinValue;
        }

        public override string ToString() => $"{Name} ({ProtocolType}) - {Status}";
    }
}