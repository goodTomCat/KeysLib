using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JabyLib.Other;

namespace KeySenderLib
{
    public class KeySender : IDisposable
    {
        protected Task WorckingTaskF = Task.CompletedTask;
        protected QueueOfAsyncActionsAdvanced ActionsForOtherThreadF;
        public event EventHandler<SendingEventArgs> SendingStarted;
        public event EventHandler<SendingEventArgs> SendingStoped;
        protected Process ProcessF;
        protected CancellationTokenSource TokenSourceF = new CancellationTokenSource();
        protected const int WmKeyDownConst = 0x0100;
        protected static IntPtr HookF;
        protected static string NameOfPipeLineF = "KeySenderPipe";
        protected NamedPipeServerStream PipeServerF;
        protected static int TurnKeyF = -1;
        protected Tuple<int, int> DelayKeyUpDownF = new Tuple<int, int>(150, 180);
        protected Tuple<int, int> DelayKeyPressF = new Tuple<int, int>(190, 220);
        protected IDictionary<string, byte> DirectxKeysF;
        protected string ProcessNameF;


        /// <exception cref="ArgumentNullException">directxKeys == null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Словарь не может быть пустым.</exception>
        public KeySender(IDictionary<string, byte> directxKeys)
        {
            if (directxKeys == null)
                throw new ArgumentNullException(nameof(directxKeys)) {Source = GetType().AssemblyQualifiedName};
            if (directxKeys.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(directxKeys.Count), "Словарь не может быть пустым.")
                    {Source = GetType().AssemblyQualifiedName};

            DirectxKeysF = directxKeys.ToDictionary(pair => pair.Key, pair => pair.Value);
        }


        /// <exception cref="ArgumentOutOfRangeException">TurnKey выходит за границы допустимых пределов, 
        /// меньше 1 или больше 254.</exception>
        public int TurnKey
        {
            get { return TurnKeyF; }
            set
            {
                if (value < 1 || value > 254)
                    throw new ArgumentOutOfRangeException(nameof(value)) {Source = GetType().AssemblyQualifiedName};

                TurnKeyF = value;
            }
        }

        public bool IsStarted
        {
            get
            {
                if (WorckingTaskF.Status == TaskStatus.Running)
                    return true;

                return false;
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">Не верный id процесса.</exception>
        public int ProcessId
        {
            get
            {
                if (ProcessF == null)
                    return -1;

                return ProcessF.Id;
            }
            set
            {
                try
                {
                    ProcessF = Process.GetProcessById(value);
                }
                catch (ArgumentException ex)
                {
                    var str = new StringBuilder();
                    str.AppendLine("Не верный id процесса.");
                    str.Append(nameof(value) + $": {value}");
                    throw new ArgumentOutOfRangeException(str.ToString(), ex)
                        {Source = GetType().AssemblyQualifiedName};
                }
                catch (InvalidOperationException ex)
                {
                    var str = new StringBuilder();
                    str.AppendLine("Не верный id процесса.");
                    str.Append(nameof(value) + $": {value}");
                    throw new ArgumentOutOfRangeException(str.ToString(), ex)
                        {Source = GetType().AssemblyQualifiedName};
                }
            }
        }

        /// <exception cref="ArgumentNullException">value == null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Словарь не может быть пустым.</exception>
        public IDictionary<string, byte> DirectxKeys
        {
            get { return new ReadOnlyDictionary<string, byte>(DirectxKeysF); }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value)) {Source = GetType().AssemblyQualifiedName};
                if (value.Count == 0)
                    throw new ArgumentOutOfRangeException(nameof(value.Count), "Словарь не может быть пустым.")
                        {Source = GetType().AssemblyQualifiedName};

                DirectxKeysF = value.ToDictionary(pair => pair.Key, pair => pair.Value);
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">Не верное имя процесса.</exception>
        public string ProcessName
        {
            get { return ProcessNameF; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Строка равна null или является пустой строкой.")
                    { Source = GetType().AssemblyQualifiedName };
                ProcessNameF = value;

                try
                {
                    ProcessF =
                        Process.GetProcessesByName(value).First(process => process.ProcessName.Equals(value));
                }
                catch
                {
                    // ignored
                }
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">Значение нижней границы должно быть положительным и больше 10. -or- 
        /// Значение верхней границы должно быть больше либо равно нижней.</exception>
        public virtual Tuple<int, int> DelayKeyUpDown
        {
            get { return DelayKeyUpDownF; }
            set
            {
                if (value.Item1 <= 10)
                    throw new ArgumentOutOfRangeException(nameof(value.Item1), value.Item1,
                            "Значение нижней границы должно быть положительным и больше 10.")
                        {Source = GetType().AssemblyQualifiedName};
                if (value.Item2 < value.Item1)
                    throw new ArgumentOutOfRangeException(nameof(value.Item2), value.Item2,
                            "Значение верхней границы должно быть больше либо равно нижней.")
                        {Source = GetType().AssemblyQualifiedName};

                DelayKeyUpDownF = value;
            }
        }

        public bool ChangeFocusToWindow { get; set; } = true;

        /// <exception cref="ArgumentOutOfRangeException">Значение нижней границы должно быть положительным и больше 10. -or- 
        /// Значение верхней границы должно быть больше либо равно нижней.</exception>
        public virtual Tuple<int, int> DelayKeyPress
        {
            get { return DelayKeyPressF; }
            set
            {
                if (value.Item1 <= 10)
                    throw new ArgumentOutOfRangeException(nameof(value.Item1), value.Item1,
                            "Значение нижней границы должно быть положительным и больше 10.")
                        {Source = GetType().AssemblyQualifiedName};
                if (value.Item2 < value.Item1)
                    throw new ArgumentOutOfRangeException(nameof(value.Item2), value.Item2,
                            "Значение верхней границы должно быть больше либо равно нижней.")
                        {Source = GetType().AssemblyQualifiedName};

                DelayKeyPressF = value;
            }
        }

        ///// <exception cref="ArgumentNullException">keysStrings == null.</exception>
        //public IEnumerable<byte> DirectxKeyCodes
        //{
        //    get { return new ReadOnlyCollection<byte>(DirectxKeyCodesF); }
        //    set
        //    {
        //        if (value == null)
        //            throw new ArgumentNullException(nameof(value))
        //            { Source = GetType().AssemblyQualifiedName };
        //        var array = value.ToArray();
        //        if (array.Length == 0)
        //            throw new ArgumentOutOfRangeException(nameof(value), "Перечисление не может быть пустым.");

        //        DirectxKeyCodesF = array;
        //    }
        //}


        public void Stop()
        {
            var tokenSource = TokenSourceF;
            if (tokenSource != null)
            {
                tokenSource.Cancel();
                TokenSourceF = null;
                WorckingTaskF = Task.CompletedTask;
            }
            ActionsForOtherThreadF.Add(() =>
            {
                InvokeEvents(new ReadOnlyDictionary<string, byte>(DirectxKeys), ProcessF, false);
                return 1;
            });
        }

        /// <exception cref="ArgumentNullException">keysStrings == null. -or- options == null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Не верный id процесса.</exception>
        /// <exception cref="Exception">При попытке инициализации отправки нажатия клавиш возникла непредвиденная ошибка.</exception>
        //public void Start(int processId, IEnumerable<string> keysStrings, SendingOptions options)
        //{
        //    if (keysStrings == null)
        //        throw new ArgumentNullException(nameof(keysStrings))
        //            {Source = GetType().AssemblyQualifiedName};
        //    if (options == null)
        //        throw new ArgumentNullException(nameof(options))
        //            {Source = GetType().AssemblyQualifiedName};


        //    try
        //    {
        //        var source = new CancellationTokenSource();
        //        TokenSourceF = source;
        //        ProcessF = Process.GetProcessById(processId);
        //        StartImpl(ProcessF, keysStrings, source.Token, options);
        //    }
        //    catch (ArgumentException ex)
        //    {
        //        var type = Type.GetType(ex.Source, false);
        //        if (type != null)
        //        {
        //            if (type.Equals(GetType()))
        //                throw ex;
        //        }
        //        else
        //        {
        //            var str = new StringBuilder();
        //            str.AppendLine("Не верный id процесса.");
        //            str.Append(nameof(processId) + $": {processId}");
        //            throw new ArgumentOutOfRangeException(str.ToString(), ex)
        //                {Source = GetType().AssemblyQualifiedName};
        //        }
        //    }
        //    catch (InvalidOperationException ex)
        //    {
        //        var type = Type.GetType(ex.Source, false);
        //        if (type != null)
        //        {
        //            if (type.Equals(GetType()))
        //                throw ex;
        //        }
        //        else
        //        {
        //            var str = new StringBuilder();
        //            str.AppendLine("Не верный id процесса.");
        //            str.Append(nameof(processId) + $": {processId}");
        //            throw new ArgumentOutOfRangeException(str.ToString(), ex)
        //                {Source = GetType().AssemblyQualifiedName};
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        var type = Type.GetType(ex.Source, false);
        //        if (type != null)
        //        {
        //            if (type.Equals(GetType()))
        //                throw ex;
        //        }
        //        else
        //        {
        //            var str = new StringBuilder();
        //            str.AppendLine("При попытке инициализации отправки нажатия клавиш возникла непредвиденная ошибка.");
        //            str.AppendLine(nameof(processId) + $": {processId}");
        //            str.Append("key strings: ");
        //            foreach (string s in keysStrings)
        //                str.Append($"{s},");
        //            throw new Exception(str.ToString(), ex) {Source = GetType().FullName};
        //        }
        //    }
        //}
        ///// <exception cref="ArgumentNullException">processName == null. -or- options == null. -or- keysStrings == null.</exception>
        ///// <exception cref="ArgumentOutOfRangeException">Не верное имя процесса.</exception>
        ///// <exception cref="Exception">При попытке инициализации отправки нажатия клавиш возникла непредвиденная ошибка.</exception>
        //public void Start(string processName, IEnumerable<string> keysStrings, SendingOptions options)
        //{
        //    if (processName == null)
        //        throw new ArgumentNullException(nameof(processName))
        //            {Source = GetType().AssemblyQualifiedName};
        //    if (keysStrings == null)
        //        throw new ArgumentNullException(nameof(keysStrings))
        //            {Source = GetType().AssemblyQualifiedName};
        //    if (options == null)
        //        throw new ArgumentNullException(nameof(options))
        //            {Source = GetType().AssemblyQualifiedName};


        //    try
        //    {
        //        ProcessF =
        //            Process.GetProcessesByName(processName).First(process => process.ProcessName.Equals(processName));
        //        var source = new CancellationTokenSource();
        //        TokenSourceF = source;
        //        StartImpl(ProcessF, keysStrings, source.Token, options);
        //    }
        //    catch (InvalidOperationException ex)
        //    {
        //        var type = Type.GetType(ex.Source, false);
        //        if (type != null)
        //        {
        //            if (type.Equals(GetType()))
        //                throw ex;
        //        }
        //        else
        //        {
        //            var str = new StringBuilder();
        //            str.AppendLine("Не верное имя процесса.");
        //            str.Append(nameof(processName) + $": {processName}");
        //            throw new ArgumentOutOfRangeException(str.ToString(), ex)
        //                {Source = GetType().AssemblyQualifiedName};
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        var type = Type.GetType(ex.Source, false);
        //        if (type != null)
        //        {
        //            if (type.Equals(GetType()))
        //                throw ex;
        //        }
        //        else
        //        {
        //            var str = new StringBuilder();
        //            str.AppendLine("При попытке инициализации отправки нажатия клавиш возникла непредвиденная ошибка.");
        //            str.AppendLine(nameof(processName) + $": {processName}");
        //            str.Append("key strings: ");
        //            foreach (string s in keysStrings)
        //                str.Append($"{s},");
        //            throw new Exception(str.ToString(), ex) {Source = GetType().FullName};
        //        }
        //    }
        //}
        ///// <exception cref="ArgumentNullException">processName == null. -or- options == null. -or- keysStrings == null.</exception>
        ///// <exception cref="Exception">При попытке инициализации отправки нажатия клавиш возникла непредвиденная ошибка.</exception>
        //public void Start(Process process, IEnumerable<string> keysStrings, SendingOptions options)
        //{
        //    if (process == null) throw new ArgumentNullException(nameof(process)) {Source = GetType().FullName};
        //    if (keysStrings == null)
        //        throw new ArgumentNullException(nameof(keysStrings))
        //            {Source = GetType().FullName};
        //    if (options == null)
        //        throw new ArgumentNullException(nameof(options))
        //            {Source = GetType().FullName};

        //    try
        //    {
        //        var source = new CancellationTokenSource();
        //        TokenSourceF = source;
        //        ProcessF = process;
        //        OptionsF = options;
        //        KeysStringsF = keysStrings;
        //        StartImpl(process, keysStrings, source.Token, options);
        //    }
        //    catch (Exception ex)
        //    {
        //        var type = Type.GetType(ex.Source, false);
        //        if (type != null)
        //        {
        //            if (type.Equals(GetType()))
        //                throw ex;
        //        }
        //        else
        //        {
        //            var str = new StringBuilder();
        //            str.AppendLine("При попытке инициализации отправки нажатия клавиш возникла непредвиденная ошибка.");
        //            str.AppendLine(nameof(process.ProcessName) + $": {process.ProcessName}");
        //            str.Append("key strings: ");
        //            foreach (string s in keysStrings)
        //                str.Append($"{s},");
        //            throw new Exception(str.ToString(), ex) {Source = GetType().FullName};
        //        }
        //    }
        //}

        /// <exception cref="ArgumentOutOfRangeException">Не верный id процесса.</exception>
        /// <exception cref="InvalidOperationException">Перед инициализации отправки нажатий клавиш, 
        /// не удалось установить фокус на окно.</exception>
        public void Start(int processId)
        {
            try
            {
                ProcessF = Process.GetProcessById(processId);
                Start();
            }
            catch (ArgumentException ex)
            {
                var type = Type.GetType(ex.Source);
                if (type != null)
                {
                    if (type.IsAssignableFrom(GetType()))
                        throw;
                }

                throw new ArgumentOutOfRangeException(nameof(processId), processId, "Не верный id процесса.")
                { Source = GetType().AssemblyQualifiedName};
            }
            catch (InvalidOperationException ex)
            {
                var type = Type.GetType(ex.Source);
                if (type != null)
                {
                    if (type.IsAssignableFrom(GetType()))
                        throw;
                }

                throw new ArgumentOutOfRangeException(nameof(processId), processId, "Не верный id процесса.")
                { Source = GetType().AssemblyQualifiedName };
            }
        }
        /// <exception cref="ArgumentOutOfRangeException">Не верное имя процесса.</exception>
        /// <exception cref="Exception">Во время инициализации отправки клавиш возникла непредвиденная ошибка.</exception>
        public void Start(string processName)
        {
            try
            {
                var process = Process.GetProcessesByName(processName);
                ProcessF = process.First(process1 => process1.ProcessName.Equals(processName));
                Start();
            }
            catch (InvalidOperationException ex)
            {
                var type = Type.GetType(ex.Source);
                if (type != null)
                {
                    if (type.IsAssignableFrom(GetType()))
                        throw;
                }

                throw new ArgumentOutOfRangeException(nameof(processName), processName, "Не верное имя процесса.")
                    {Source = GetType().AssemblyQualifiedName};
            }
            catch (Exception ex)
            {
                var type = Type.GetType(ex.Source);
                if (type != null)
                {
                    if (type.IsAssignableFrom(GetType()))
                        throw;
                }

                throw new Exception("Во время инициализации отправки клавиш возникла непредвиденная ошибка.", ex)
                { Source = GetType().AssemblyQualifiedName };
            }
        }
        /// <exception cref="InvalidOperationException">Перед инициализации отправки нажатий клавиш, не удалось установить фокус на окно.</exception>
        public virtual void Start()
        {
            //var currProc = process;
            if (WorckingTaskF.Status == TaskStatus.Running)
            {
                Stop();
                return;
            }

            //if (ChangeFocusToWindow)
            //{
            //    if (ProcessF == null || ProcessF.HasExited)
            //    {
            //        if (ProcessNameF == null)
            //            throw new InvalidOperationException("Не задано имя процесса.");

            //        Start(ProcessNameF);
            //        return;
            //    }
            //    var isSet = SetForegroundWindow(ProcessF.SafeHandle.DangerousGetHandle());
            //    isSet = SetForegroundWindow(ProcessF.MainModule.BaseAddress);
            //    isSet = SetForegroundWindow(ProcessF.MainModule.EntryPointAddress);
            //    if (!SetForegroundWindow(ProcessF.SafeHandle.DangerousGetHandle()))
            //    {
            //        int error = Marshal.GetLastWin32Error();
            //        var str = new StringBuilder();
            //        str.AppendLine("Перед инициализации отправки нажатий клавиш, не удалось установить фокус на окно.");
            //        str.AppendLine($"{nameof(ProcessF.ProcessName)}: {ProcessF.ProcessName}.");
            //        str.Append($"{nameof(error)}: {error}.");
            //        throw new InvalidOperationException(str.ToString()) { Source = GetType().AssemblyQualifiedName };
            //    }
            //}
                

            //var keyCodes = OptionsF.DirectxKeyCodes.Join(KeysStringsF, pair => pair.Key, s => s,
            //    (pair, s) => (byte) pair.Value).ToArray();
            WorckingTaskF = Task.Run(() =>
            {
                StartSendKey(ProcessF, DirectxKeysF.Values, TokenSourceF.Token);

            }, TokenSourceF.Token);
            ActionsForOtherThreadF.Add(() =>
            {
                InvokeEvents(new ReadOnlyDictionary<string, byte>(DirectxKeys), ProcessF, true);
                return 1;
            });
        }
        /// <exception cref="Exception">Во время прослушивания входящего межпроцессорного подключения возникла непредвиденная ошибка.</exception>
        public async Task StartListen()
        {
            try
            {
                PipeServerF?.Disconnect();
                PipeServerF = new NamedPipeServerStream(NameOfPipeLineF);
                while (true)
                {
                    await PipeServerF.WaitForConnectionAsync().ConfigureAwait(false);
                    while (PipeServerF.IsConnected)
                    {
                        var buf = new byte[16];
                        await PipeServerF.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                        if (buf.All(b => b == 1))
                            Start();
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                var type = Type.GetType(ex.Source);
                if (type != null)
                {
                    if (type.IsAssignableFrom(GetType()))
                        throw;
                }
            }
            catch (Exception ex)
            {
                var type = Type.GetType(ex.Source);
                if (type != null)
                {
                    if (type.IsAssignableFrom(GetType()))
                    {
                        var str = new StringBuilder();
                        str.AppendLine(
                            "Во время прослушивания входящего межпроцессорного подключения возникла непредвиденная ошибка.");
                        str.AppendLine($"{nameof(TurnKey)}: {TurnKey}.");
                        str.AppendLine($"{nameof(HookF)} != null: {HookF != IntPtr.Zero}.");
                        str.AppendLine(
                            $"{nameof(WorckingTaskF.Status)}: {Enum.GetName(typeof(TaskStatus), WorckingTaskF.Status)}.");
                        if (ProcessF != null)
                            str.AppendLine($"{nameof(ProcessF.ProcessName)}: {ProcessF.ProcessName}.");
                        throw new Exception(str.ToString(), ex) { Source = GetType().AssemblyQualifiedName };
                    }
                }
                throw;
            }

        }
        /// <exception cref="InvalidOperationException">Не удалось установить хук.</exception>
        public void SetHook()
        {
            LowLevelKeyboardProc procedure = (code, param, lParam) =>
            {
                if (code >= 0 && param == (IntPtr) WmKeyDownConst)
                {
                    var keyCode = Marshal.ReadInt32(lParam);
                    if (keyCode == TurnKeyF)
                    {
                        using (var client = new NamedPipeClientStream(NameOfPipeLineF))
                        {
                            client.Connect();
                            var buf = new byte[16];
                            for (int i = 0; i < buf.Length; i++)
                                buf[i] = 1;
                            client.Write(buf, 0, buf.Length);
                        }
                    }
                }
                return CallNextHookEx(HookF, code, param, lParam);
            };
            if (HookF != IntPtr.Zero)
                UnhookWindowsHookEx(HookF);

            HookF = SetWindowsHookEx(13, procedure, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName),
                0);

            if (HookF == IntPtr.Zero)
            {
                var str = new StringBuilder();
                str.AppendLine("Не удалось установить хук.");
                str.Append($"Код ошибки win32: {Marshal.GetLastWin32Error()}.");
                throw new InvalidOperationException(str.ToString()) {Source = GetType().AssemblyQualifiedName};
            }
        }
        public void Unhook()
        {
            if (HookF == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(HookF);
        }
        public void Dispose()
        {
            Unhook();
        }


        private void InvokeEvents(IReadOnlyDictionary<string, byte> keys , Process process, bool isStarted)
        {
            var eventt = isStarted ? SendingStarted : SendingStoped;
            if (eventt == null)
                return;

            var delegates = eventt.GetInvocationList().Cast<EventHandler<SendingEventArgs>>().ToArray();
            if (delegates.Length == 0)
                return;

            Func<Task> func = () =>
            {
                try
                {
                    var eventArgs = new SendingEventArgs(process.ProcessName, process.Id, keys);
                    foreach (EventHandler<SendingEventArgs> eventHandler in delegates)
                        eventHandler(this, eventArgs);
                }
                catch (Exception)
                {
                    // ignored
                }
                return Task.CompletedTask;
            };
            ActionsForOtherThreadF.Add(func);
        }
        protected virtual void StartSendKey(Process procc, IEnumerable<byte> keyCodes, CancellationToken token)
        {
            var rand = new Random();
            while (true)
            {
                if (token.IsCancellationRequested)
                    break;

                foreach (byte keyCode in keyCodes)
                {
                    if (keyCode != 0)
                        keybd_event(0, keyCode, 0, (UIntPtr)0);
                }
                Thread.Sleep(rand.Next(DelayKeyUpDown.Item1, DelayKeyUpDown.Item2));
                foreach (byte keyCode in keyCodes)
                {
                    if (keyCode != 0)
                        keybd_event(0, keyCode, 2, (UIntPtr)0);
                }
                Thread.Sleep(rand.Next(DelayKeyPress.Item1, DelayKeyPress.Item2));
            }
        }



        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

        //[DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        //private static extern IntPtr SendMessage(IntPtr hwnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        //[DllImport("user32.dll")]
        //internal static extern uint SendInput(uint nInputs, INPUT[] pInputs,
        //    int cbSize);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags,
    UIntPtr dwExtraInfo);

        //[return: MarshalAs(UnmanagedType.Bool)]
        //[DllImport("user32.dll", SetLastError = true)]
        //static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        //[DllImport("user32.dll")]
        //private static extern int MapVirtualKey(uint uCode, MapVirtualKeyMapTypes uMapType);
    }

    
}

