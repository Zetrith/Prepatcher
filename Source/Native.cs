using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Prepatcher
{
    static partial class Native
    {
        public static Action<IntPtr, IntPtr> mono_install_assembly_search_hook { get; private set; }
        public static Func<IntPtr, IntPtr> mono_assembly_name_get_name { get; private set; }
        public static Func<IntPtr, IntPtr> mono_assembly_get_name { get; private set; }
        public static Func<IntPtr> mono_domain_get { get; private set; }

        public static IntPtr DomainPtr { get; private set; }

        public static bool Linux => Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer;
        public static bool Windows => Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
        public static bool OSX => Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;

        public static void EarlyInit()
        {
            if (Linux)
                BindMethods("Linux");
            else if (Windows)
                BindMethods("Windows");
            else if (OSX)
                BindMethods("OSX");

            DomainPtr = mono_domain_get();
        }

        public static string ManagedFolder()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "Resources/Data/Managed";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Managed";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Managed";
            return null;
        }

        private static void BindMethods(string postfix)
        {
            foreach (var method in Type.GetType(typeof(Native).FullName + postfix).GetMethods())
            {
                if (!method.MethodImplementationFlags.HasFlag(MethodImplAttributes.PreserveSig)) continue;

                var setter = AccessTools.PropertySetter(typeof(Native), method.Name);
                if (setter != null)
                    setter.Invoke(null, new[] { Delegate.CreateDelegate(setter.GetParameters()[0].ParameterType, method) });
            }
        }
    }

    static class NativeLinux
    {
        const string MonoLib = "libmonobdwgc-2.0.so";

        [DllImport(MonoLib)]
        public static extern void mono_install_assembly_search_hook(IntPtr func, IntPtr data);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_assembly_name_get_name(IntPtr aname);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_assembly_get_name(IntPtr aname);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_domain_get();
    }

    static partial class NativeWindows
    {
        const string MonoLib = "mono-2.0-bdwgc.dll";

        [DllImport(MonoLib)]
        public static extern void mono_install_assembly_search_hook(IntPtr func, IntPtr data);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_assembly_name_get_name(IntPtr aname);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_assembly_get_name(IntPtr aname);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_domain_get();
    }

    static partial class NativeOSX
    {
        const string MonoLib = "libmonobdwgc-2.0.dylib";

        [DllImport(MonoLib)]
        public static extern void mono_install_assembly_search_hook(IntPtr func, IntPtr data);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_assembly_name_get_name(IntPtr aname);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_assembly_get_name(IntPtr aname);

        [DllImport(MonoLib)]
        public static extern IntPtr mono_domain_get();
    }
}
