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

namespace MeGUI
{
    public sealed class AvsSession : IDisposable
    {
        private readonly AvsLibrary _lib;
        private readonly IntPtr _env;
        private bool _disposed;

        public IntPtr Env { get { return _env; } }

        public AvsSession(AvsLibrary lib)
        {
            _lib = lib;
            _env = lib.CreateScriptEnvironment(9);
            if (_env == IntPtr.Zero)
                throw new AviSynthException("avs_create_script_environment(9) returned NULL.");
            CheckError();
        }

        public void CheckError()
        {
            IntPtr errPtr = _lib.GetError(_env);
            if (errPtr == IntPtr.Zero) return;
            string msg = Marshal.PtrToStringAnsi(errPtr) ?? "Unknown AviSynth+ error";
            throw new AviSynthException(msg);
        }

        public string EvalString(string expression)
        {
            const int AVS_VALUE_SIZE = 16;
            IntPtr argMem = IntPtr.Zero;
            IntPtr strPtr = IntPtr.Zero;

            try
            {
                argMem = Marshal.AllocHGlobal(AVS_VALUE_SIZE);
                for (int i = 0; i < AVS_VALUE_SIZE; i++) Marshal.WriteByte(argMem, i, 0);

                strPtr = Marshal.StringToHGlobalAnsi(expression);
                Marshal.WriteInt16(argMem, 0, (short)'s');
                Marshal.WriteIntPtr(argMem, 8, strPtr);
                var argVal = Marshal.PtrToStructure<AvsApi.AvsValue>(argMem);

                var result = _lib.Invoke(_env, "Eval", argVal, IntPtr.Zero);
                CheckError();

                try
                {
                    if ((result.type == (short)'s' || result.type == (short)'d') && result.p != IntPtr.Zero)
                        return Marshal.PtrToStringAnsi(result.p) ?? string.Empty;
                    return string.Empty;
                }
                finally
                {
                    _lib.ReleaseValue(result);
                }
            }
            finally
            {
                if (strPtr != IntPtr.Zero) Marshal.FreeHGlobal(strPtr);
                if (argMem != IntPtr.Zero) Marshal.FreeHGlobal(argMem);
            }
        }

        public IntPtr EvalScript(string script)
        {
            const int AVS_VALUE_SIZE = 16;
            IntPtr argMem = IntPtr.Zero;
            IntPtr strPtr = IntPtr.Zero;

            try
            {
                argMem = Marshal.AllocHGlobal(AVS_VALUE_SIZE);
                for (int i = 0; i < AVS_VALUE_SIZE; i++) Marshal.WriteByte(argMem, i, 0);

                strPtr = Marshal.StringToHGlobalAnsi(script);
                Marshal.WriteInt16(argMem, 0, (short)'s');
                Marshal.WriteIntPtr(argMem, 8, strPtr);
                var argVal = Marshal.PtrToStructure<AvsApi.AvsValue>(argMem);

                var result = _lib.Invoke(_env, "Eval", argVal, IntPtr.Zero);
                CheckError();

                if (result.type != (short)'c')
                {
                    _lib.ReleaseValue(result);
                    throw new AviSynthException(string.Format("AVS script did not return a clip (type='{0}').", (char)result.type));
                }

                IntPtr clip = _lib.ValAsClip(result, _env);
                _lib.ReleaseValue(result);
                if (clip == IntPtr.Zero)
                    throw new AviSynthException("avs_take_clip returned NULL.");

                return clip;
            }
            finally
            {
                if (strPtr != IntPtr.Zero) Marshal.FreeHGlobal(strPtr);
                if (argMem != IntPtr.Zero) Marshal.FreeHGlobal(argMem);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lib.DeleteScriptEnvironment(_env);
        }
    }
}
