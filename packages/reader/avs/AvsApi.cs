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
using System.Runtime.Serialization;

namespace MeGUI
{
    [Serializable]
    public class AviSynthException : ApplicationException
    {
        public AviSynthException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        public AviSynthException(string message) : base(message) { }
        public AviSynthException() : base() { }
        public AviSynthException(string message, Exception innerException) : base(message, innerException) { }
    }

    public enum AudioSampleType : int
    {
        Unknown = 0,
        INT8    = 1,
        INT16   = 2,
        INT24   = 4,
        INT32   = 8,
        FLOAT   = 16
    }

    public static class AvsApi
    {
        public const string LibName = "avisynth";

        // AVS_CS pixel-type bit fields — matches VideoInfo::AvsColorFormat in avisynth.h
        // Color-family flags
        public const int CS_YUVA        = 1 << 27;
        public const int CS_BGR         = 1 << 28;
        public const int CS_YUV         = 1 << 29;
        public const int CS_INTERLEAVED = 1 << 30;
        public const int CS_PLANAR      = unchecked((int)(1u << 31));  // signed: 0x80000000

        // Packed-format subtype bits (bits 0-1)
        public const int CS_RGB_TYPE    = 1 << 0;  // packed BGR24 / planar RGB  (no alpha)
        public const int CS_RGBA_TYPE   = 1 << 1;  // packed BGR32 / planar RGBA (with alpha)

        // Chroma horizontal subsampling (bits 2:0, CS_Shift_Sub_Width = 0)
        public const int CS_Sub_Width_Mask = 7;          // 7 << 0
        public const int CS_Sub_Width_1    = 3;          // YV24 / 4:4:4  — no h-subsampling
        public const int CS_Sub_Width_2    = 0;          // YV12, YV16    — 2× h-subsampling
        public const int CS_Sub_Width_4    = 1;          // YV411, YUV9   — 4× h-subsampling

        // Chroma vertical subsampling (bits 10:8, CS_Shift_Sub_Height = 8)
        public const int CS_Shift_Sub_Height = 8;
        public const int CS_Sub_Height_Mask  = 7 << 8;   // 0x700
        public const int CS_Sub_Height_1     = 3 << 8;   // YV16, YV24  — no v-subsampling
        public const int CS_Sub_Height_2     = 0 << 8;   // YV12, I420  — 2× v-subsampling
        public const int CS_Sub_Height_4     = 1 << 8;   // YUV9        — 4× v-subsampling

        // Sample-depth bits (bits 18:16)
        public const int CS_Shift_Sample_Bits = 16;
        public const int CS_Sample_Bits_Mask  = 7 << CS_Shift_Sample_Bits;
        public const int CS_Sample_Bits_8     = 0 << CS_Shift_Sample_Bits;
        public const int CS_Sample_Bits_10    = 5 << CS_Shift_Sample_Bits;
        public const int CS_Sample_Bits_12    = 6 << CS_Shift_Sample_Bits;
        public const int CS_Sample_Bits_14    = 7 << CS_Shift_Sample_Bits;
        public const int CS_Sample_Bits_16    = 1 << CS_Shift_Sample_Bits;
        public const int CS_Sample_Bits_32    = 2 << CS_Shift_Sample_Bits;  // float

        // AVS_IT_* image_type flags (from avisynth_c.h)
        public const int IT_BFF              = 1 << 0;
        public const int IT_TFF              = 1 << 1;
        public const int IT_FIELDBASED       = 1 << 2;
        public const int IT_HAS_CHANNELMASK  = 1 << 3;
        public const int IT_SPEAKER_BITS_MASK = 0x7FFFF0;   // (AVS_MASK_SPEAKER_DEFINED << 4) | AVS_IT_SPEAKER_ALL

        // AVS_SAMPLE_* audio sample type flags (from avisynth_c.h)
        public const int SAMPLE_INT8  = 1 << 0;
        public const int SAMPLE_INT16 = 1 << 1;
        public const int SAMPLE_INT24 = 1 << 2;
        public const int SAMPLE_INT32 = 1 << 3;
        public const int SAMPLE_FLOAT = 1 << 4;

        public const int PROPTYPE_INT = 'i';

        // AVS_DEFAULT_PLANE = 0
        public const int DEFAULT_PLANE = 0;

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct AvsValue
        {
            [FieldOffset(0)] public short type;
            [FieldOffset(2)] public short array_size;
            [FieldOffset(8)] public int    i;
            [FieldOffset(8)] public float  f;
            [FieldOffset(8)] public IntPtr p;
            [FieldOffset(8)] public long   l;
            [FieldOffset(8)] public double d;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr CreateScriptEnvironmentDelegate(int version);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DeleteScriptEnvironmentDelegate(IntPtr env);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate AvsValue InvokeDelegate(IntPtr env, [MarshalAs(UnmanagedType.LPStr)] string name, AvsValue args, IntPtr arg_names);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GetErrorDelegate(IntPtr env);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr ValAsClipDelegate(AvsValue val, IntPtr env);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ReleaseClipDelegate(IntPtr clip);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GetVideoInfoDelegate(IntPtr clip);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GetFrameDelegate(IntPtr clip, int n);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ReleaseVideoFrameDelegate(IntPtr frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetAudioDelegate(IntPtr clip, IntPtr buf, long start, long count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GetFramePropsRODelegate(IntPtr env, IntPtr frame);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate long PropGetIntDelegate(IntPtr env, IntPtr map, [MarshalAs(UnmanagedType.LPStr)] string key, int index, ref int error);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int PropGetTypeDelegate(IntPtr env, IntPtr map, [MarshalAs(UnmanagedType.LPStr)] string key);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void ReleaseValueDelegate(AvsValue val);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetPitchPDelegate(IntPtr frame, int plane);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int GetCpuFlagsDelegate(IntPtr env);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate long GetCpuFlagsExDelegate(IntPtr env);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate IntPtr GetReadPtrPDelegate(IntPtr frame, int plane);
    }
}
