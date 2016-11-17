using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeySenderLib
{
    public class SendingEventArgs : EventArgs
    {
        public SendingEventArgs(string processName, int processId, IReadOnlyDictionary<string, byte> keys)
        {
            if (processName == null) throw new ArgumentNullException(nameof(processName));
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            ProcessId = processId;
            ProcessName = processName;
            Keys = keys;
        }


        public int ProcessId { get; }
        public string ProcessName { get; }
        public IReadOnlyDictionary<string, byte> Keys { get; }
    }
}
