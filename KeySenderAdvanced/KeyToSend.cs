using System;

namespace KeySenderLib.KeySenderAdvanced
{
    public class KeyToSend
    {
        protected byte KeyCodeF;
        protected int DelayBeforeF;
        protected int DelayAfterF;


        /// <exception cref="ArgumentOutOfRangeException">Код клавиши должен быть больше нуля.</exception>        
        public KeyToSend(byte keyCode, bool isVirtualKeyCode, bool isKeyUp)
        {
            if (keyCode == 0)
                throw new ArgumentOutOfRangeException(nameof(keyCode), keyCode, "Код клавиши должен быть больше нуля.")
                {Source = GetType().AssemblyQualifiedName};

            KeyCodeF = keyCode;
            IsVirtualKeyCode = isVirtualKeyCode;
            IsKeyUp = isKeyUp;
        }
        /// <param name="delay">Задержка в миллисекундах.</param>
        /// <exception cref="ArgumentOutOfRangeException">Значение задержки должно варьироваться от 0 до 60000 миллисекунд.</exception>
        public KeyToSend(byte keyCode, int delayB, int delayA, bool isVirtualKeyCode, bool isKeyUp) : this(keyCode, isVirtualKeyCode, isKeyUp)
        {
            if (delayB < 0 || delayB > 60000)
                throw new ArgumentOutOfRangeException(nameof(delayB), delayB,
                    "Значение задержки должно варьироваться от 0 до 60000 миллисекунд.")
                { Source = GetType().AssemblyQualifiedName };
            if (delayA < 0 || delayA > 60000)
                throw new ArgumentOutOfRangeException(nameof(delayA), delayA,
                    "Значение задержки должно варьироваться от 0 до 60000 миллисекунд.")
                { Source = GetType().AssemblyQualifiedName };

            DelayBeforeF = delayB;
            DelayAfterF = delayA;
        }
        public KeyToSend(int keyCode, int delayB, int delayA, bool isVirtualKeyCode, bool isKeyUp) : 
            this((byte)keyCode, delayB, delayA, isVirtualKeyCode, isKeyUp)
        {
            
        }
        public KeyToSend(int keyCode, bool isVirtualKeyCode, bool isKeyUp) : this((byte)keyCode, isVirtualKeyCode, isKeyUp)
        { }


        public byte KeyCode => KeyCodeF;
        /// <summary>
        /// Задержка до нажатия в секундах.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Значение задержки должно варьироваться от 0 до 60 секунд.</exception>
        public float DelayBeforeAsSeconds
        {
            get
            {
                var msec = (float)DelayBeforeF;
                return msec/1000;
            }
            set
            {
                if (value < 0 || value > 60)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Значение задержки должно варьироваться от 0 до 60 секунд.")
                    { Source = GetType().AssemblyQualifiedName };

                DelayBeforeF = Convert.ToInt32(value * 1000);
            }
        }
        /// <summary>
        /// Задержка до нажатия в миллисекунд.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Значение задержки должно варьироваться от 0 до 60000 миллисекунд.</exception>
        public int DelayBeforeAsMSeconds
        {
            get
            {
                return DelayBeforeF;
            }
            set
            {
                if (value < 0 || value > 60000)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Значение задержки должно варьироваться от 0 до 60000 миллисекунд.")
                    { Source = GetType().AssemblyQualifiedName };

                DelayBeforeF = value;
            }
        }
        /// <summary>
        /// Задержка после нажатия в секундах.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Значение задержки должно варьироваться от 0 до 60 секунд.</exception>
        public float DelayAterAsSeconds
        {
            get
            {
                var msec = (float)DelayAfterF;
                return msec / 1000;
            }
            set
            {
                if (value < 0 || value > 60)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Значение задержки должно варьироваться от 0 до 60 секунд.")
                    { Source = GetType().AssemblyQualifiedName };

                DelayAfterF = Convert.ToInt32(value * 1000);

            }
        }
        /// <summary>
        /// Задержка после нажатия в миллисекунд.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Значение задержки должно варьироваться от 0 до 60000 миллисекунд.</exception>
        public int DelayAfterAsMSeconds
        {
            get { return DelayAfterF; }
            set
            {
                if (value < 0 || value > 60000)
                    throw new ArgumentOutOfRangeException(nameof(value), value,
                        "Значение задержки должно варьироваться от 0 до 60000 миллисекунд.")
                    { Source = GetType().AssemblyQualifiedName };

                DelayAfterF = value;
            }
        }
        public bool IsVirtualKeyCode { get; }
        public bool IsKeyUp { get; }
        public static int DelayKeyUpDown => 160;
        public static int DelayKeyPress => 200;
    }
}
