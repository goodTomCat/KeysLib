using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using JabyLib.Other;

namespace KeySenderLib
{
    public enum SendKeyType : byte
    {
        Virtual,
        DirectX
    }

    public class SendingOptions : INotifyPropertyChanged
    {
        protected Tuple<int, int> DelayKeyUpDownF = new Tuple<int, int>(150, 180);
        protected Tuple<int, int> DelayKeyPressF = new Tuple<int, int>(190, 220);
        protected Dictionary<string, int> DirectxKeyCodesF;
        protected Tuple<int, int, int> LastSelectedKeysIndexesF = new Tuple<int, int, int>(-1, -1, -1);
        protected int LastProcessIdF;
        protected int KeyCodeToStartOrStopSendingF;
        public event PropertyChangedEventHandler PropertyChanged;

        public SendingOptions()
        {
            DirectxKeyCodesF = CreateDirectxKeyCodes();
        }
        /// <exception cref="ArgumentNullException">options == null.</exception>
        public SendingOptions(SendingOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            Clone(options);
        }


        public virtual Tuple<int, int> DelayKeyUpDown
        {
            get { return DelayKeyUpDownF; }
            set
            {
                if (value.Item1 <= 10)
                {
                    var exc = new ArgumentOutOfRangeException("value",
                        "Значение нижней границы должно быть положительным и больше 10.");
                    exc.Data.Add("DelayKeyUpDown.Item1", value.Item1);
                    throw exc;
                }
                if (value.Item2 < value.Item1)
                {
                    var exc = new ArgumentOutOfRangeException("DelayKeyUpDown.Item2",
                        "Значение верхней границы должно быть больше либо равно нижней.");
                    exc.Data.Add("DelayKeyUpDown.Item2", value.Item2);
                    throw exc;
                }

                DelayKeyUpDownF = value;
                NotifyPropertyChanged("DelayKeyUpDown");
            }
        }
        public bool ChangeFocusToWindow { get; set; } = true;
        public virtual Tuple<int, int> DelayKeyPress
        {
            get { return DelayKeyPressF; }
            set
            {
                if (value.Item1 <= DelayKeyUpDown.Item1)
                {
                    var exc = new ArgumentOutOfRangeException("value",
                        "Значение нижней границы должно быть больше DelayKeyUpDown.Item1.");
                    exc.Data.Add("DelayKeyPress.Item1", value.Item1);
                    throw exc;
                }
                if (value.Item2 < value.Item1 || value.Item2 < DelayKeyUpDown.Item2)
                {
                    var exc = new ArgumentOutOfRangeException("DelayKeyUpDown.Item2",
                        "Значение верхней границы должно быть больше либо равно нижней и больше DelayKeyUpDown.Item2.");
                    exc.Data.Add("DelayKeyPress.Item2", value.Item2);
                    throw exc;
                }

                DelayKeyPressF = value;
                NotifyPropertyChanged("DelayKeyPress");
            }
        }
        public IReadOnlyDictionary<string, int> DirectxKeyCodes =>
            new ReadOnlyDictionary<string, int>(DirectxKeyCodesF);
        public SendKeyType KeyType { get; set; } = SendKeyType.DirectX;
        public virtual Tuple<int, int, int> LastSelectedKeysIndexes
        {
            get { return LastSelectedKeysIndexesF; }
            set
            {
                if (value.Item1 < -1 || value.Item2 < -1 || value.Item3 < -1)
                {
                    var exc = new ArgumentOutOfRangeException("LastSelectedKeyIndex",
                        "Значения индексов должны быть больше, либо равно -1.");
                    exc.Data.Add("LastSelectedKeyIndex", value);
                    throw exc;
                }
                else
                {
                    if (value.Item1 != -1 && value.Item2 != -1 && value.Item2 != -1)
                    {
                        if (!(value.Item1 != value.Item2 && value.Item2 != value.Item3))
                        {
                            var exc = new ArgumentOutOfRangeException("LastSelectedKeyIndex",
                                "Значения индексов должны различаться.");
                            exc.Data.Add("LastSelectedKeyIndex", value);
                            throw exc;
                        }
                    }
                }

                LastSelectedKeysIndexesF = value;
                NotifyPropertyChanged("LastSelectedKeysIndexes");
            }
        }
        public virtual int LastProcessId
        {
            get { return LastProcessIdF; }
            set
            {
                if (value < 0)
                {
                    var exc = new ArgumentOutOfRangeException(nameof(LastProcessId),
                        "Значение id последнего процесса должно быть больше, либо равно нулю.");
                    exc.Data.Add(nameof(LastProcessId), value);
                    throw exc;
                }

                LastProcessIdF = value;
                NotifyPropertyChanged("LastProcessId");
            }
        }
        public virtual int KeyCodeToStartOrStopSending
        {
            get { return KeyCodeToStartOrStopSendingF; }
            set
            {
                if (value < 0)
                {
                    var exc = new ArgumentOutOfRangeException(nameof(KeyCodeToStartOrStopSending),
                        "Значение кода клавиши должно быть больше нуля.");
                    exc.Data.Add(nameof(KeyCodeToStartOrStopSending), value);
                }

                KeyCodeToStartOrStopSendingF = value;
                NotifyPropertyChanged(nameof(KeyCodeToStartOrStopSending));
            }
        }


        /// <exception cref="ArgumentNullException">path == null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Файла не существует, либо его длина равна нулю.</exception>
        /// <exception cref="IOException">При чтении настроек из файла возникла ошибка ввода вывода.</exception>
        /// <exception cref="SerializationException">При чтении настроек из файла возникла ошибка десеарелизации.</exception>
        /// <exception cref="Exception">При чтении настроек из файла возникла непредвиденная ошибка.</exception>
        public virtual async Task ReadFromFile(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path)) {Source = GetType().AssemblyQualifiedName};
            var infoOfFile = new FileInfo(path);
            if (!infoOfFile.Exists || infoOfFile.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(path), "Файла не существует, либо его длина равна нулю.")
                    {Source = GetType().AssemblyQualifiedName};

            int length = -1;
            int numbOfReadedBytes = -1;
            try
            {
                using (var stream = infoOfFile.OpenRead())
                {
                    var bytesOfLength = new byte[5000];
                    numbOfReadedBytes =
                        await stream.ReadAsync(bytesOfLength, 0, bytesOfLength.Length).ConfigureAwait(false);
                    length = BitConverter.ToInt32(bytesOfLength, 0);
                    if (length < 0 || length > 20000)
                        throw new InvalidOperationException("Не тот файл.");

                    MemoryStream streamMem;
                    if (length > numbOfReadedBytes)
                    {
                        streamMem = new MemoryStream(bytesOfLength, 4, numbOfReadedBytes - 4, true);
                        bytesOfLength = new byte[length - numbOfReadedBytes];
                        await stream.ReadAsync(bytesOfLength, 0, bytesOfLength.Length).ConfigureAwait(false);
                        streamMem.Seek(streamMem.Length, SeekOrigin.Begin);
                        streamMem.Write(bytesOfLength, 0, bytesOfLength.Length);
                    }
                    else
                        streamMem = new MemoryStream(bytesOfLength, 4, length);

                    var ser = new ProtoBufSerializer();
                    var options = ser.Deserialize<SendingOptions>(streamMem, true);
                    Clone(options, false);
                }
            }
            catch (IOException ex)
            {
                throw CreateException(0, 0, ex, path, length);
            }
            catch (SerializationException ex)
            {
                throw CreateException(0, 1, ex, path);
            }
            catch (Exception ex)
            {
                throw CreateException(0, 2, ex, path, length, numbOfReadedBytes);
            }


        }
        /// <exception cref="ArgumentNullException">directory == null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Указанной директории не существует.</exception>
        public async Task<string> WriteToFile(DirectoryInfo directory)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory)) { Source = GetType().AssemblyQualifiedName };
            if (!directory.Exists)
                throw new ArgumentOutOfRangeException(nameof(directory.Exists))
                { Source = GetType().AssemblyQualifiedName };

            var path = directory + "//" + "Key Sending Options.opt";
            await WriteToFile(path).ConfigureAwait(false);
            return path;
        }
        /// <exception cref="ArgumentNullException">path == null.</exception>
        /// <exception cref="SerializationException">При записи настроек в файл возникла ошибка сериализации.</exception>
        /// <exception cref="ArgumentException"><see cref="File.Create(string)"/>.</exception>
        /// <exception cref="IOException">При записи настроек в файл возникла ошибка ввода/вывода.</exception>
        /// <exception cref="NotSupportedException"><see cref="File.Create(string)"/>.</exception>
        /// <exception cref="Exception">При записи настроек в файл возникла непредвиденная ошибка.</exception>
        public virtual async Task WriteToFile(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path)) {Source = GetType().AssemblyQualifiedName};

            try
            {
                var ser = new ProtoBufSerializer();
                var bytesToWrite = ser.Serialize(this, false);
                using (var stream = File.Create(path))
                {
                    var lengthAsBytes = BitConverter.GetBytes(bytesToWrite.Length);
                    stream.Write(lengthAsBytes, 0, lengthAsBytes.Length);
                    await stream.WriteAsync(bytesToWrite, 0, bytesToWrite.Length).ConfigureAwait(false);
                }
            }
            catch (SerializationException ex)
            {
                throw CreateException(1, 0, ex);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message, nameof(path), ex) {Source = GetType().AssemblyQualifiedName};
            }
            catch (IOException ex)
            {
                throw CreateException(1, 1, ex, path);
            }
            catch (NotSupportedException ex)
            {
                throw new ArgumentOutOfRangeException(nameof(path), ex.Message)
                    {Source = GetType().AssemblyQualifiedName};
            }
            catch (Exception ex)
            {
                throw CreateException(1, 2, ex, path);
            }
            
        }

        private void Clone(SendingOptions options)
        {
            Clone(options, true);
        }
        private void Clone(SendingOptions options, bool deepClone)
        {
            if (deepClone)
            {
                DelayKeyUpDownF = new Tuple<int, int>(options.DelayKeyUpDown.Item1, options.DelayKeyUpDown.Item2);
                ChangeFocusToWindow = options.ChangeFocusToWindow;
                DelayKeyPressF = new Tuple<int, int>(options.DelayKeyPress.Item1, options.DelayKeyPress.Item2);
                DirectxKeyCodesF = CreateDirectxKeyCodes();
                KeyType = options.KeyType;
                LastSelectedKeysIndexesF = new Tuple<int, int, int>(options.LastSelectedKeysIndexes.Item1,
                    options.LastSelectedKeysIndexes.Item2, options.LastSelectedKeysIndexes.Item3);
                LastProcessIdF = options.LastProcessId;
                KeyCodeToStartOrStopSendingF = options.KeyCodeToStartOrStopSending;
            }
            else
            {
                DelayKeyUpDownF = options.DelayKeyUpDownF;
                ChangeFocusToWindow = options.ChangeFocusToWindow;
                DelayKeyPressF = options.DelayKeyPressF;
                DirectxKeyCodesF = CreateDirectxKeyCodes();
                KeyType = options.KeyType;
                LastSelectedKeysIndexesF = options.LastSelectedKeysIndexesF;
                LastProcessIdF = options.LastProcessId;
                KeyCodeToStartOrStopSendingF = options.KeyCodeToStartOrStopSending;
            }
        }
        protected virtual void NotifyPropertyChanged(string info)
        {
            try
            {
                var ev = PropertyChanged;
                if (ev == null) return;

                var delegates = ev.GetInvocationList();
                foreach (Delegate delegat in delegates)
                    ((PropertyChangedEventHandler)delegat)(this, new PropertyChangedEventArgs(info));
            }
            catch (Exception)
            {
                // ignored
            }
        }
        protected Dictionary<string, int> CreateDirectxKeyCodes()
        {
            var dic = new Dictionary<string, int>(160)
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
                {"D0", 11},
                {"D9", 10},
                {"D8", 9},
                {"D7", 8},
                {"D6", 7},
                {"D5", 6},
                {"D4", 5},
                {"D3", 4},
                {"D2", 3},
                {"D1", 2},
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
        private Exception CreateException(int numb, int innerNumb, params object[] objs)
        {
            Exception result = null;
            StringBuilder str = new StringBuilder();
            switch (numb)
            {
                case 0:
                    #region ReadFromFile(string path)
                    switch (innerNumb)
                    {
                        case 0:
                            //CreateException(0, 0, 0ex, 1path, 2length)
                            str.AppendLine("При чтении настроек из файла возникла ошибка ввода вывода.");
                            str.AppendLine($"Путь: {objs[1]}.");
                            str.Append($"length: {objs[2]}.");
                            result = new IOException(str.ToString(), (IOException)objs[0]);
                            break;
                        case 1:
                            //CreateException(0, 1, 0ex, 1path)
                            str.AppendLine("При чтении настроек из файла возникла ошибка десеарелизации.");
                            str.Append($"Путь: {objs[1]}.");
                            result = new SerializationException(str.ToString(), (SerializationException)objs[0]);
                            break;
                        case 2:
                            //CreateException(0, 2, 0ex, 1path, 2length, 3numbOfReadedBytes)
                            str.AppendLine("При чтении настроек из файла возникла непредвиденная ошибка.");
                            str.AppendLine($"Путь: {objs[1]}.");
                            str.AppendLine($"length: {objs[2]}.");
                            str.Append($"numbOfReadedBytes: {objs[3]}.");
                            result = new Exception(str.ToString(), (Exception)objs[0]);
                            break;
                    }
                    #endregion
                    break;
                case 1:
                    #region WriteToFile(string path)
                    switch (innerNumb)
                    {
                        case 0:
                            str.Append("При записи настроек в файл возникла ошибка сериализации.");
                            result = new SerializationException(str.ToString(), (SerializationException)objs[0]);
                            break;
                        case 1:
                            //CreateException(1, 1, 0ex, 1path)
                            str.AppendLine("При записи настроек в файл возникла ошибка ввода/вывода.");
                            str.Append($"path: {objs[1]}.");
                            result = new IOException(str.ToString(), (IOException)objs[0]);
                            break;
                        case 2:
                            //CreateException(1, 2, ex, path)
                            str.AppendLine("При записи настроек в файл возникла непредвиденная ошибка.");
                            str.Append($"path: {objs[1]}.");
                            result = new Exception(str.ToString(), (Exception)objs[0]);
                            break;
                    }
                    #endregion
                    break;
            }
            if (result == null)
            {
                str.AppendLine("Не удалось найти подходящего описания ошибки.");
                str.AppendLine($"numb: {numb}.");
                str.Append($"innerNumb: {innerNumb}.");
                result = new Exception(str.ToString());
            }
            result.Source = GetType().AssemblyQualifiedName;
            return result;
        }
    }
}
