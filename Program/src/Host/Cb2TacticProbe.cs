using System;

namespace CombatBrain2.Host
{
    // 取证钩子：原生在构造 Tactics 前把某个具体 TacticComponent 类型登记到这里，
    // 使探测战术类与当前原生选择链看到同一批候选权重。构造时按信息最全的那个
    // 类型签名观测，不按声明类型观测。
    internal static class Cb2TacticProbe
    {
        private static Type _nativeType;
        private static int _nativeConstructed;
        private static int _baseConstructed;

        internal static void RegisterNative(Type nativeType)
        {
            _nativeType = nativeType;
        }

        internal static void OnTacticConstructed(Type concreteType, bool isBaseConstructor)
        {
            if (_nativeType == null || concreteType != _nativeType) return;
            if (isBaseConstructor) _baseConstructed++;
            else _nativeConstructed++;
        }

        internal static bool NativeRegistered { get { return _nativeType != null; } }
        internal static int NativeConstructed { get { return _nativeConstructed; } }
        internal static int BaseConstructed { get { return _baseConstructed; } }

        internal static void Reset()
        {
            _nativeType = null;
            _nativeConstructed = 0;
            _baseConstructed = 0;
        }
    }
}
