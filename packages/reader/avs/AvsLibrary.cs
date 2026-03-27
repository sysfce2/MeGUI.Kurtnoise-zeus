// ****************************************************************************
// 
// Copyright (C) 2005-2026 Doom9 & al
// 
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
// 
// ****************************************************************************

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MeGUI
{
    public sealed class AvsLibrary : IDisposable
    {
        private IntPtr _handle;
        private string _loadedPath = string.Empty;

        public string LoadedPath { get { return _loadedPath; } }

        public AvsApi.CreateScriptEnvironmentDelegate  CreateScriptEnvironment  { get; private set; }
        public AvsApi.DeleteScriptEnvironmentDelegate  DeleteScriptEnvironment  { get; private set; }
        public AvsApi.InvokeDelegate                   Invoke                   { get; private set; }
        public AvsApi.GetErrorDelegate                 GetError                 { get; private set; }
        public AvsApi.ValAsClipDelegate                ValAsClip                { get; private set; }
        public AvsApi.ReleaseClipDelegate              ReleaseClip              { get; private set; }
        public AvsApi.GetVideoInfoDelegate             GetVideoInfo             { get; private set; }
        public AvsApi.GetFrameDelegate                 GetFrame                 { get; private set; }
        public AvsApi.ReleaseVideoFrameDelegate        ReleaseVideoFrame        { get; private set; }
        public AvsApi.GetAudioDelegate                 GetAudio                 { get; private set; }
        public AvsApi.ReleaseValueDelegate             ReleaseValue             { get; private set; }

        public AvsApi.GetFramePropsRODelegate          GetFramePropsRO          { get; private set; }
        public AvsApi.PropGetIntDelegate               PropGetInt               { get; private set; }
        public AvsApi.PropGetTypeDelegate              PropGetType              { get; private set; }
        public bool HasFrameProps { get; private set; }

        public AvsApi.GetPitchPDelegate                GetPitchP                { get; private set; }
        public AvsApi.GetReadPtrPDelegate              GetReadPtrP              { get; private set; }
        public AvsApi.GetCpuFlagsDelegate              GetCpuFlags              { get; private set; }
        public AvsApi.GetCpuFlagsExDelegate            GetCpuFlagsEx            { get; private set; }
        public bool HasCpuFlagsEx { get; private set; }

        public static AvsLibrary Load(string explicitPath = null)
        {
            var lib = new AvsLibrary();
            lib.Init(explicitPath);
            return lib;
        }

        private void Init(string explicitPath)
        {
            _handle = string.IsNullOrWhiteSpace(explicitPath)
                ? LoadLibraryW(AvsApi.LibName + ".dll")
                : LoadLibraryW(explicitPath);

            if (_handle == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                throw new DllNotFoundException(
                    string.Format("Could not load '{0}' (Win32 error {1}).",
                        string.IsNullOrWhiteSpace(explicitPath) ? AvsApi.LibName + ".dll" : explicitPath,
                        err));
            }

            _loadedPath = TryGetModulePath(_handle) ?? (explicitPath ?? (AvsApi.LibName + ".dll"));

            // Core — required
            CreateScriptEnvironment = Resolve<AvsApi.CreateScriptEnvironmentDelegate>("avs_create_script_environment");
            DeleteScriptEnvironment = Resolve<AvsApi.DeleteScriptEnvironmentDelegate>("avs_delete_script_environment");
            Invoke                  = Resolve<AvsApi.InvokeDelegate>                 ("avs_invoke");
            GetError                = Resolve<AvsApi.GetErrorDelegate>               ("avs_get_error");
            ValAsClip               = Resolve<AvsApi.ValAsClipDelegate>              ("avs_take_clip");
            ReleaseClip             = Resolve<AvsApi.ReleaseClipDelegate>            ("avs_release_clip");
            GetVideoInfo            = Resolve<AvsApi.GetVideoInfoDelegate>           ("avs_get_video_info");
            GetFrame                = Resolve<AvsApi.GetFrameDelegate>               ("avs_get_frame");
            ReleaseVideoFrame       = Resolve<AvsApi.ReleaseVideoFrameDelegate>      ("avs_release_video_frame");
            ReleaseValue            = Resolve<AvsApi.ReleaseValueDelegate>           ("avs_release_value");
            GetPitchP               = Resolve<AvsApi.GetPitchPDelegate>              ("avs_get_pitch_p");
            GetReadPtrP             = Resolve<AvsApi.GetReadPtrPDelegate>             ("avs_get_read_ptr_p");
            GetCpuFlags             = Resolve<AvsApi.GetCpuFlagsDelegate>            ("avs_get_cpu_flags");
            GetAudio                = Resolve<AvsApi.GetAudioDelegate>               ("avs_get_audio");

            // Optional — frame properties (AviSynth+ extended API)
            var getPropRO   = TryResolve<AvsApi.GetFramePropsRODelegate>("avs_get_frame_props_ro");
            var propGetInt  = TryResolve<AvsApi.PropGetIntDelegate>     ("avs_prop_get_int");
            var propGetType = TryResolve<AvsApi.PropGetTypeDelegate>    ("avs_prop_get_type");
            if (getPropRO != null && propGetInt != null && propGetType != null)
            {
                GetFramePropsRO = getPropRO;
                PropGetInt      = propGetInt;
                PropGetType     = propGetType;
                HasFrameProps   = true;
            }

            // Optional — 64-bit CPU flags (AviSynth+ v12+)
            var cpuFlagsEx = TryResolve<AvsApi.GetCpuFlagsExDelegate>("avs_get_cpu_flags_ex");
            if (cpuFlagsEx != null)
            {
                GetCpuFlagsEx = cpuFlagsEx;
                HasCpuFlagsEx = true;
            }
        }

        private T Resolve<T>(string name) where T : class
        {
            IntPtr ptr = GetProcAddress(_handle, name);
            if (ptr == IntPtr.Zero)
                throw new EntryPointNotFoundException(
                    string.Format("Required AviSynth+ export '{0}' not found.", name));
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }

        private T TryResolve<T>(string name) where T : class
        {
            IntPtr ptr = GetProcAddress(_handle, name);
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                FreeLibrary(_handle);
                _handle = IntPtr.Zero;
            }
        }

        private static string TryGetModulePath(IntPtr hModule)
        {
            try
            {
                var sb = new StringBuilder(2048);
                uint len = GetModuleFileName(hModule, sb, sb.Capacity);
                if (len == 0) return null;
                return sb.ToString();
            }
            catch { return null; }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpLibFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, BestFitMapping = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);
    }
}
