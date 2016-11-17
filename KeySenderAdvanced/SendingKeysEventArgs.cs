using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace KeySenderLib.KeySenderAdvanced
{
    public class SendingKeysEventArgs : EventArgs
    {
        public SendingKeysEventArgs(ICollection<KeyToSend> keys)
        {
            if (keys == null || keys.Count == 0)
                throw new ArgumentException("Коллекция клавиш равна null или имеет нулевую длину.", nameof(keys))
                {Source = GetType().AssemblyQualifiedName};

            
            //var virtDic = KeySenderLib.KeySenderAdvanced.KeySenderAdvanced.VirtualKeyCodes();
            //var dxDic = KeySenderLib.KeySenderAdvanced.KeySenderAdvanced.DirectXKeyCodes();
            //var dic =
            //    keys.Select(
            //            keyToSend =>
            //                keyToSend.IsVirtualKeyCode
            //                    ? virtDic.First(pair => pair.Value == keyToSend.KeyCode)
            //                    : dxDic.First(pair => pair.Value == keyToSend.KeyCode))
            //        .ToDictionary(keyss2 => keyss2.Key, keyss2 => keyss2.Value);
            //var keyss = isVirtualKeyCodes
            //    ? KeySenderAdvanced.VirtualKeyCodes()
            //        .Join(keys, pair => pair.Value, send => send.KeyCode,
            //            (pair, send) => pair)
            //        .ToDictionary(pair => pair.Key, pair => pair.Value)
            //    : KeySenderAdvanced.DirectXKeyCodes().Join(keys, pair => pair.Value, send => send.KeyCode,
            //            (pair, send) => pair)
            //        .ToDictionary(pair => pair.Key, pair => pair.Value);
            Keys = new ReadOnlyCollection<KeyToSend>(keys.ToArray());
        }


        public IReadOnlyCollection<KeyToSend> Keys { get; }
    }
}
