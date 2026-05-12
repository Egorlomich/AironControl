using System;
using System.Collections.Generic;
using System.Text;

namespace AironControl.Model
{
    public class CommandRequest
    {
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }

        public CommandRequest(string command) => Command = command;
    }
}
