using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JabyLib.Other;

namespace KeySenderLib.KeySenderAdvanced
{
    public class KeySenderAdvanced : IDisposable
    {
        protected Task WorckingTaskF = Task.CompletedTask;
        private QueueOfAsyncActionsAdvanced _actionsForOtherThreadF = new QueueOfAsyncActionsAdvanced();
        public event EventHandler<SendingKeysEventArgs> SendingStarted;
        public event EventHandler<SendingKeysEventArgs> SendingStoped;
        private CancellationTokenSource _tokenSourceF = new CancellationTokenSource();
        protected const int WmKeyDownConst = 0x0100;
        private static IntPtr _hookF;
        protected string NameOfPipeLineF = "KeySenderPipe";
        protected NamedPipeServerStream PipeServerF;
        protected static NamedPipeClientStream PipeClientF;
        private static int _turnKeyF = -1;
        private KeyToSend[] _keysToSendF;


        /// <exception cref="ArgumentOutOfRangeException">TurnKey выходит за границы допустимых пределов, 
        /// меньше 1 или больше 254.</exception>
        public int TurnKey
        {
            get { return _turnKeyF; }
            set
            {
                if (value < 1 || value > 254)
                    throw new ArgumentOutOfRangeException(nameof(value)) {Source = GetType().AssemblyQualifiedName};

                _turnKeyF = value;
            }
        }

        public bool IsStarted { get; protected set; }

        public IReadOnlyCollection<KeyToSend> KeysToSend
        {
            get
            {
                if (_keysToSendF == null)
                    return null;

                return new ReadOnlyCollection<KeyToSend>(_keysToSendF);
            }
        }

        /// <exception cref="ArgumentException">Перечисление не может быть пустой или равняться нулю.</exception>
        public IEnumerable<KeyToSend> KeysToSendEnumeration
        {
            get { return KeysToSend; }
            set
            {
                if (value == null || !value.Any())
                    throw new ArgumentException("Перечисление не может быть пустой или равняться нулю.")
                        {Source = GetType().AssemblyQualifiedName};

                _keysToSendF = value.ToArray();
            }
        }

        public bool StartOnce { get; set; }


        /// <exception cref="InvalidOperationException">Предыдущий процесс отправки клавиш еще не завершен. -or- 
        /// Последовательность клавиш для отправки не задана или является пустой.</exception>
        public void Start()
        {
            if (IsStarted)
                throw new InvalidOperationException("Предыдущий процесс отправки клавиш еще не завершен.")
                    {Source = GetType().AssemblyQualifiedName};
            if (_keysToSendF == null || _keysToSendF.Length == 0)
            {
                throw new InvalidOperationException(
                        "Последовательность клавиш для отправки не задана или является пустой.")
                    {Source = GetType().AssemblyQualifiedName};
            }

            _tokenSourceF = new CancellationTokenSource();
            StartImpl(_keysToSendF, _tokenSourceF.Token);
            IsStarted = true;

            InvokeEvents(_keysToSendF, true);

        }

        /// <exception cref="InvalidOperationException">Предыдущий процесс отправки клавиш еще не завершен.</exception>
        /// <exception cref="ArgumentException">Коллекция клавиш для отправки равна null или является пустой.</exception>
        public void Start(ICollection<KeyToSend> keysToSend)
        {
            if (IsStarted)
                throw new InvalidOperationException("Предыдущий процесс отправки клавиш еще не завершен.")
                    {Source = GetType().AssemblyQualifiedName};
            if (keysToSend == null || keysToSend.Count == 0)
                throw new ArgumentException("Коллекция клавиш для отправки равна null или является пустой.")
                    {Source = GetType().AssemblyQualifiedName};

            _keysToSendF = keysToSend.ToArray();
            _tokenSourceF = new CancellationTokenSource();
            StartImpl(keysToSend, _tokenSourceF.Token);
            IsStarted = true;

            InvokeEvents(keysToSend, true);
        }

        public void Stop()
        {
            var tokenSource = _tokenSourceF;
            if (tokenSource != null)
            {
                tokenSource.Cancel();
                _tokenSourceF = null;
                WorckingTaskF = Task.CompletedTask;
                IsStarted = false;
            }

            InvokeEvents(_keysToSendF, false);
        }

        /// <exception cref="Exception">Во время прослушивания входящего межпроцессорного подключения возникла непредвиденная ошибка.</exception>
        public virtual async Task StartListen()
        {
            try
            {
                PipeServerF = new NamedPipeServerStream(NameOfPipeLineF);
                var waitTask = PipeServerF.WaitForConnectionAsync();
                PipeClientF = new NamedPipeClientStream(NameOfPipeLineF);
                await PipeClientF.ConnectAsync().ConfigureAwait(false);
                try
                {
                    while (true)
                    {
                        //await PipeServerF.WaitForConnectionAsync().ConfigureAwait(false);
                        while (PipeServerF.IsConnected)
                        {
                            var buf = new byte[16];
                            await PipeServerF.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                            if (buf.All(b => b == 1))
                            {
                                if (IsStarted)
                                    Stop();
                                else
                                    Start();
                            }
                        }
                    }
                }
                catch (IOException)
                {

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
                        str.AppendLine($"{nameof(_hookF)} != null: {_hookF != IntPtr.Zero}.");
                        str.Append(
                            $"{nameof(WorckingTaskF.Status)}: {Enum.GetName(typeof(TaskStatus), WorckingTaskF.Status)}.");
                        throw new Exception(str.ToString(), ex) { Source = GetType().AssemblyQualifiedName };
                    }
                }
                throw;
            }

        }
        /// <exception cref="InvalidOperationException">Не удалось установить хук.</exception>
        public void SetHook()
        {
            if (_hookF != IntPtr.Zero)
                return;

            LowLevelKeyboardProc procedure = (code, param, lParam) =>
            {
                if (code >= 0 && param == (IntPtr)WmKeyDownConst)
                {
                    var keyCode = Marshal.ReadInt32(lParam);
                    if (keyCode == _turnKeyF)
                    {
                        var buf = new byte[16];
                        for (int i = 0; i < buf.Length; i++)
                            buf[i] = 1;
                        PipeClientF.Write(buf, 0, buf.Length);
                        //using (var client = new NamedPipeClientStream(NameOfPipeLineF))
                        //{
                        //    client.Connect();
                        //    var buf = new byte[16];
                        //    for (int i = 0; i < buf.Length; i++)
                        //        buf[i] = 1;
                        //    client.Write(buf, 0, buf.Length);
                        //}
                    }
                }
                return CallNextHookEx(_hookF, code, param, lParam);
            };

            _hookF = SetWindowsHookEx(13, procedure, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName),
                0);

            if (_hookF == IntPtr.Zero)
            {
                var str = new StringBuilder();
                str.AppendLine("Не удалось установить хук.");
                str.Append($"Код ошибки win32: {Marshal.GetLastWin32Error()}.");
                throw new InvalidOperationException(str.ToString()) { Source = GetType().AssemblyQualifiedName };
            }
        }
        public void Unhook()
        {
            if (_hookF == IntPtr.Zero)
                return;

            UnhookWindowsHookEx(_hookF);
            _hookF = IntPtr.Zero;
        }
        public void Dispose()
        {
            Unhook();
        }
        public static uint VirtualCodeToScan(uint uCode)
        {
            return MapVirtualKey(uCode, 0);
        }
        public static uint ScanCodeToVirtual(uint uCode)
        {
            return MapVirtualKey(uCode, 1);
        }
        public static IDictionary<string, byte> DirectXKeyCodes()
        {
            var dic = new Dictionary<string, byte>(160)
            {
                {"Sleep", 223},
                {"Next", 209},
                {"Stop", 149},
                {"Convert", 121},
                {"Decimal", 83},
                {"X", 45},
                {"Y", 21},
                {"Escape", 1},
                {"Circumflex", 144},
                {"PageDown", 209},
                {"DownArrow", 208},
                {"RightArrow", 205},
                {"LeftArrow", 203},
                {"PageUp", 201},
                {"UpArrow", 200},
                {"RightAlt", 184},
                {"NumPadSlash", 181},
                {"NumPadPeriod", 83},
                {"NumPadPlus", 78},
                {"NumPadMinus", 74},
                {"CapsLock", 58},
                {"LeftAlt", 56},
                {"NumPadStar", 55},
                {"BackSpace", 14},
                {"MediaSelect", 237},
                {"Mail", 236},
                {"MyComputer", 235},
                {"WebBack", 234},
                {"WebForward", 233},
                {"WebStop", 232},
                {"WebRefresh", 231},
                {"WebFavorites", 230},
                {"WebSearch", 229},
                {"Wake", 227},
                {"Power", 222},
                {"Apps", 221},
                {"RightWindows", 220},
                {"LeftWindows", 219},
                {"Down", 208},
                {"End", 207},
                {"Prior", 201},
                {"Up", 200},
                {"Home", 199},
                {"RightMenu", 184},
                {"SysRq", 183},
                {"Divide", 181},
                {"NumPadComma", 179},
                {"WebHome", 178},
                {"VolumeUp", 176},
                {"VolumeDown", 174},
                {"MediaStop", 164},
                {"PlayPause", 162},
                {"Calculator", 161},
                {"Mute", 160},
                {"RightControl", 157},
                {"NumPadEnter", 156},
                {"NextTrack", 153},
                {"Unlabeled", 151},
                {"AX", 150},
                {"Kanji", 148},
                {"Underline", 147},
                {"Colon", 146},
                {"At", 145},
                {"PrevTrack", 144},
                {"NumPadEquals", 141},
                {"AbntC2", 126},
                {"Yen", 125},
                {"NoConvert", 123},
                {"AbntC1", 115},
                {"Kana", 112},
                {"F15", 102},
                {"F14", 101},
                {"F13", 100},
                {"F12", 88},
                {"F11", 87},
                {"OEM102", 86},
                {"NumPad0", 82},
                {"NumPad3", 81},
                {"NumPad2", 80},
                {"NumPad1", 79},
                {"NumPad6", 77},
                {"NumPad5", 76},
                {"NumPad4", 75},
                {"Subtract", 74},
                {"NumPad9", 73},
                {"NumPad8", 72},
                {"NumPad7", 71},
                {"Scroll", 70},
                {"Numlock", 69},
                {"F10", 68},
                {"F9", 67},
                {"F8", 66},
                {"F7", 65},
                {"F6", 64},
                {"F5", 63},
                {"F4", 62},
                {"F3", 61},
                {"F2", 60},
                {"F1", 59},
                {"Capital", 58},
                {"Space", 57},
                {"LeftMenu", 56},
                {"Multiply", 55},
                {"RightShift", 54},
                {"Slash", 53},
                {"Period", 52},
                {"Comma", 51},
                {"M", 50},
                {"N", 49},
                {"B", 48},
                {"V", 47},
                {"C", 46},
                {"Z", 44},
                {"BackSlash", 43},
                {"LeftShift", 42},
                {"Grave", 41},
                {"Apostrophe", 40},
                {"SemiColon", 39},
                {"L", 38},
                {"K", 37},
                {"J", 36},
                {"H", 35},
                {"G", 34},
                {"F", 33},
                {"D", 32},
                {"S", 31},
                {"A", 30},
                {"LeftControl", 29},
                {"Return", 28},
                {"RightBracket", 27},
                {"LeftBracket", 26},
                {"P", 25},
                {"O", 24},
                {"I", 23},
                {"U", 22},
                {"T", 20},
                {"R", 19},
                {"E", 18},
                {"W", 17},
                {"Tab", 15},
                {"Back", 14},
                {"Equals", 13},
                {"Minus", 12},
                {"0", 11},
                {"9", 10},
                {"8", 9},
                {"7", 8},
                {"6", 7},
                {"5", 6},
                {"4", 5},
                {"3", 4},
                {"2", 3},
                {"1", 2},
                {"Insert", 210},
                {"Right", 205},
                {"Left", 203},
                {"Pause", 197},
                {"Add", 78},
                {"Delete", 211},
                {"Q", 16}
            };

            return dic;
        }
        public static IDictionary<string, byte> VirtualKeyCodes()
        {
            var dic = new Dictionary<string, byte>
            {
                {"Left mouse button", 1},
                {"Right mouse button", 2},
                {"Control-break processing", 3},
                {"Middle mouse button (three-button mouse)", 4},
                {"BackSpace ", 8},
                {"Tab ", 9},
                {"CLEAR ", 12},
                {"ENTER ", 13},
                {"SHIFT ", 16},
                {"CTRL ", 17},
                {"ALT ", 18},
                {"PAUSE ", 19},
                {"CAPS LOCK ", 20},
                {"ESC ", 27},
                {"SPACEBAR", 32},
                {"PAGE UP ", 33},
                {"PAGE DOWN ", 34},
                {"END ", 35},
                {"HOME ", 36},
                {"LEFT ARROW ", 37},
                {"UP ARROW ", 38},
                {"RIGHT ARROW ", 39},
                {"DOWN ARROW ", 40},
                {"SELECT ", 41},
                {"PRINT ", 42},
                {"EXECUTE ", 43},
                {"PRINT SCREEN ", 44},
                {"INS ", 45},
                {"DEL ", 46},
                {"HELP ", 47},
                {"0 ", 48},
                {"1 ", 49},
                {"2 ", 50},
                {"3 ", 51},
                {"4 ", 52},
                {"5 ", 53},
                {"6 ", 54},
                {"7 ", 55},
                {"8 ", 56},
                {"9 ", 57},
                {"A ", 65},
                {"B ", 66},
                {"C ", 67},
                {"D ", 68},
                {"E ", 69},
                {"F ", 70},
                {"G ", 71},
                {"H ", 72},
                {"I ", 73},
                {"J ", 74},
                {"K ", 75},
                {"L ", 76},
                {"M ", 77},
                {"N ", 78},
                {"O ", 79},
                {"P ", 80},
                {"Q ", 81},
                {"R ", 82},
                {"S ", 83},
                {"T ", 84},
                {"U ", 85},
                {"V ", 86},
                {"W ", 87},
                {"X ", 88},
                {"Y ", 89},
                {"Z ", 90},
                {"NumPad0", 96},
                {"NumPad1", 97},
                {"NumPad2", 98},
                {"NumPad3", 99},
                {"NumPad4", 100},
                {"NumPad5", 101},
                {"NumPad6", 102},
                {"NumPad7", 103},
                {"NumPad8", 104},
                {"NumPad9", 105},
                {"Separator ", 108},
                {"Subtract ", 109},
                {"Decimal ", 110},
                {"Divide ", 111},
                {"F1 ", 112},
                {"F2 ", 113},
                {"F3 ", 114},
                {"F4 ", 115},
                {"F5 ", 116},
                {"F6 ", 117},
                {"F7 ", 118},
                {"F8 ", 119},
                {"F9 ", 120},
                {"F10 ", 121},
                {"F11 ", 122},
                {"F12 ", 123},
                {"F13 ", 124},
                {"F14 ", 125},
                {"F15 ", 126},
                {"F16 ", 127},
                {"NUM LOCK ", 144},
                {"SCROLL LOCK ", 145},
                {"Left SHIFT ", 160},
                {"Right SHIFT ", 161},
                {"Left CONTROL ", 162},
                {"Right CONTROL ", 163},
                {"Left MENU ", 164},
                {"Right MENU ", 165},
                {"Play ", 250},
                {"Zoom ", 251},
            };
            return dic;
        }


        protected virtual void StartImpl(IEnumerable<KeyToSend> keysToSend, CancellationToken token)
        {
            WorckingTaskF = Task.Run(async () =>
            {
                if (StartOnce)
                {
                    foreach (KeyToSend keyToSend in keysToSend)
                    {
                        if (token.IsCancellationRequested)
                            return;

                        await Task.Delay(keyToSend.DelayBeforeAsMSeconds, token).ConfigureAwait(false);
                        keybd_event(0, keyToSend.KeyCode, keyToSend.IsKeyUp ? (uint)2 : 0, (UIntPtr)0);
                        await Task.Delay(keyToSend.DelayAfterAsMSeconds, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    while (true)
                    {
                        foreach (KeyToSend keyToSend in keysToSend)
                        {
                            if (token.IsCancellationRequested)
                                return;

                            await Task.Delay(keyToSend.DelayBeforeAsMSeconds, token).ConfigureAwait(false);
                            keybd_event(0, keyToSend.KeyCode, keyToSend.IsKeyUp ? (uint)2 : 0, (UIntPtr)0);
                            await Task.Delay(keyToSend.DelayAfterAsMSeconds, token).ConfigureAwait(false);
                        }
                    }
                }
            }, token);
        }
        private void InvokeEvents(ICollection<KeyToSend> keys, bool isStarted)
        {
            var eventt = isStarted ? SendingStarted : SendingStoped;
            if (eventt == null)
                return;

            var delegates = eventt.GetInvocationList().Cast<EventHandler<SendingKeysEventArgs>>().ToArray();
            if (delegates.Length == 0)
                return;

            Func<Task> func = () =>
            {
                try
                {
                    var eventArgs = new SendingKeysEventArgs(keys);
                    foreach (EventHandler<SendingKeysEventArgs> eventHandler in delegates)
                        eventHandler(this, eventArgs);
                }
                catch (Exception ex)
                {
                    // ignored
                }
                return Task.CompletedTask;
            };
            _actionsForOtherThreadF.Add(func);
        }


        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

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
