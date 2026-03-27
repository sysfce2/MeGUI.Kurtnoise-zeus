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
    public static class AvsProbe
    {
        public sealed class StreamInfo
        {
            // Video
            public int    Width            { get; }
            public int    Height           { get; }
            public uint   FpsNum           { get; }
            public uint   FpsDen           { get; }
            public int    NumFrames        { get; }
            public int    BitDepth         { get; }
            public bool   FullRange        { get; }
            public string ColorSpace       { get; }  // decoded from pixel_type, e.g. "YUV420", "RGBP", "BGR24"
            public int    ImageType        { get; }
            // Derived from image_type
            public bool   IsFieldBased     { get; }  // AVS_IT_FIELDBASED
            public bool   IsBff            { get; }  // AVS_IT_BFF  (Bottom Field First)
            public bool   IsTff            { get; }  // AVS_IT_TFF  (Top Field First)
            public int    VideoPitch       { get; }  // bytes per row, luma plane, from avs_get_pitch_p
            // Audio
            public int    AudioSampleRate  { get; }
            public int    AudioChannels    { get; }
            public long   NumAudioSamples  { get; }
            public int    ChannelMask      { get; }
            public int    SampleType       { get; }  // AVS_SAMPLE_* bitmask
            // CPU
            public int    CpuFlags         { get; }  // 32-bit avs_get_cpu_flags
            public long   CpuFlagsEx       { get; }  // 64-bit avs_get_cpu_flags_ex (0 if unavailable)

            public StreamInfo(
                int width, int height, uint fpsNum, uint fpsDen, int numFrames,
                int bitDepth, bool fullRange, string colorSpace, int imageType,
                bool isFieldBased, bool isBff, bool isTff, int videoPitch,
                int audioSampleRate, int audioChannels, long numAudioSamples,
                int channelMask, int sampleType,
                int cpuFlags, long cpuFlagsEx)
            {
                Width           = width;
                Height          = height;
                FpsNum          = fpsNum;
                FpsDen          = fpsDen;
                NumFrames       = numFrames;
                BitDepth        = bitDepth;
                FullRange       = fullRange;
                ColorSpace      = colorSpace;
                ImageType       = imageType;
                IsFieldBased    = isFieldBased;
                IsBff           = isBff;
                IsTff           = isTff;
                VideoPitch      = videoPitch;
                AudioSampleRate = audioSampleRate;
                AudioChannels   = audioChannels;
                NumAudioSamples = numAudioSamples;
                ChannelMask     = channelMask;
                SampleType      = sampleType;
                CpuFlags        = cpuFlags;
                CpuFlagsEx      = cpuFlagsEx;
            }
        }

        public static StreamInfo Probe(AvsLibrary lib, AvsSession session, IntPtr clip)
        {
            IntPtr viPtr = lib.GetVideoInfo(clip);
            if (viPtr == IntPtr.Zero) throw new AviSynthException("avs_get_video_info returned NULL.");

            // AVS_VideoInfo layout (from avisynth_c.h):
            //   int  width;                 offset  0
            //   int  height;                offset  4
            //   unsigned fps_numerator;     offset  8
            //   unsigned fps_denominator;   offset 12
            //   int  num_frames;            offset 16
            //   int  pixel_type;            offset 20
            //   int  audio_samples_per_second; offset 24
            //   int  sample_type;           offset 28
            //   int64_t num_audio_samples;  offset 32
            //   int  nchannels;             offset 40
            //   int  image_type;            offset 44
            int  width      = Marshal.ReadInt32(viPtr,  0);
            int  height     = Marshal.ReadInt32(viPtr,  4);
            uint fpsNum     = unchecked((uint)Marshal.ReadInt32(viPtr,  8));
            uint fpsDen     = unchecked((uint)Marshal.ReadInt32(viPtr, 12));
            int  numFrames  = Marshal.ReadInt32(viPtr, 16);
            int  pixelType  = Marshal.ReadInt32(viPtr, 20);
            int  audioRate  = Marshal.ReadInt32(viPtr, 24);
            int  sampleType = Marshal.ReadInt32(viPtr, 28);
            long numAudio   = Marshal.ReadInt64(viPtr, 32);
            int  channels   = Marshal.ReadInt32(viPtr, 40);
            int  imageType  = Marshal.ReadInt32(viPtr, 44);

            // Parity / field flags
            bool isFieldBased = (imageType & AvsApi.IT_FIELDBASED) != 0;
            bool isBff        = (imageType & AvsApi.IT_BFF) != 0;
            bool isTff        = (imageType & AvsApi.IT_TFF) != 0;

            // Channel mask (unshifted AVS_MASK_SPEAKER_* values)
            int chMask = 0;
            if ((imageType & AvsApi.IT_HAS_CHANNELMASK) != 0)
                chMask = (imageType & AvsApi.IT_SPEAKER_BITS_MASK) >> 4;

            // Bit depth — refined later via frame props if available
            int  bitDepth  = GetBitDepth(pixelType);
            bool fullRange = false;
            string colorSpace = GetColorSpace(pixelType);

            // Video pitch — requires a decoded frame; use frame 0
            int videoPitch = 0;
            IntPtr frame0 = lib.GetFrame(clip, 0);
            if (frame0 != IntPtr.Zero)
            {
                try
                {
                    videoPitch = lib.GetPitchP(frame0, AvsApi.DEFAULT_PLANE);

                    // Frame properties: bit depth + colour range
                    if (lib.HasFrameProps && lib.GetFramePropsRO != null && lib.PropGetInt != null && lib.PropGetType != null)
                    {
                        IntPtr map = lib.GetFramePropsRO(session.Env, frame0);
                        if (map != IntPtr.Zero)
                        {
                            long range = ReadIntProp(lib, session.Env, map, "_ColorRange", 1);
                            fullRange  = (range == 0);
                            long cd    = ReadIntProp(lib, session.Env, map, "_ColorDepth", 0);
                            if (cd > 0) bitDepth = (int)cd;
                        }
                    }
                }
                finally { lib.ReleaseVideoFrame(frame0); }
            }

            // CPU flags
            int  cpuFlags   = lib.GetCpuFlags(session.Env);
            long cpuFlagsEx = lib.HasCpuFlagsEx ? lib.GetCpuFlagsEx(session.Env) : 0L;

            return new StreamInfo(
                width, height, fpsNum, fpsDen, numFrames,
                bitDepth, fullRange, colorSpace, imageType,
                isFieldBased, isBff, isTff, videoPitch,
                audioRate, channels, numAudio, chMask, sampleType,
                cpuFlags, cpuFlagsEx
            );
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static int GetBitDepth(int pixelType)
        {
            int field = (pixelType & AvsApi.CS_Sample_Bits_Mask) >> AvsApi.CS_Shift_Sample_Bits;
            switch (field)
            {
                case 0: return 8;
                case 1: return 16;
                case 2: return 16;  // float — report as 16 for compatibility
                case 5: return 10;
                case 6: return 12;
                case 7: return 14;
                default: return 8;
            }
        }

        private static string GetColorSpace(int pt)
        {
            bool isPlanar      = (pt & AvsApi.CS_PLANAR)      != 0;
            bool isInterleaved = (pt & AvsApi.CS_INTERLEAVED) != 0;
            bool isYUV         = (pt & AvsApi.CS_YUV)         != 0;
            bool isBGR         = (pt & AvsApi.CS_BGR)         != 0;
            bool isYUVA        = (pt & AvsApi.CS_YUVA)        != 0;
            bool isRGBType     = (pt & AvsApi.CS_RGB_TYPE)    != 0;  // no-alpha flag for packed; RGB for planar
            bool isRGBAType    = (pt & AvsApi.CS_RGBA_TYPE)   != 0;  // alpha flag for packed;  RGBA for planar

            // ── Y-only (luma only, 4:0:0) ─────────────────────────────────────
            // CS_GENERIC_Y = CS_PLANAR | CS_INTERLEAVED | CS_YUV — both layout bits set
            if (isPlanar && isInterleaved && isYUV && !isYUVA)
                return "Y";

            // ── Packed / interleaved (non-planar) ────────────────────────────
            if (isInterleaved && !isPlanar)
            {
                // YUY2 (4:2:2 packed)
                if (isYUV) return "YUY2";

                // Packed BGR family — distinguish 8-bit vs 16-bit by sample-bits field
                if (isBGR)
                {
                    bool is16bit = (pt & AvsApi.CS_Sample_Bits_Mask) == AvsApi.CS_Sample_Bits_16;
                    if (isRGBType  && is16bit) return "BGR48";  // CS_BGR48
                    if (isRGBAType && is16bit) return "BGR64";  // CS_BGR64
                    if (isRGBType)             return "BGR24";  // CS_BGR24
                    if (isRGBAType)            return "BGR32";  // CS_BGR32
                }

                return "Unknown";
            }

            // ── Planar YUV / YUVA ────────────────────────────────────────────
            if (isPlanar && (isYUV || isYUVA))
            {
                // Horizontal subsampling value (3 bits, shift 0)
                int subW = pt & AvsApi.CS_Sub_Width_Mask;
                // Vertical subsampling value (3 bits, shift 8)
                int subH = (pt & AvsApi.CS_Sub_Height_Mask) >> AvsApi.CS_Shift_Sub_Height;

                // Decode ratio from the sentinel values defined in avisynth.h:
                //   Width:  3 → 1x (no h-sub), 0 → 2x, 1 → 4x
                //   Height: 3 → 1x (no v-sub), 0 → 2x, 1 → 4x
                string sub;
                if      (subW == 3 && subH == 3) sub = "444";  // YV24 / YUV444Pxx / YUVA444
                else if (subW == 0 && subH == 3) sub = "422";  // YV16 / YUV422Pxx / YUVA422
                else if (subW == 0 && subH == 0) sub = "420";  // YV12  / YUV420Pxx / YUVA420
                else if (subW == 1 && subH == 3) sub = "411";  // YV411
                else if (subW == 1 && subH == 1) sub = "410";  // YUV9
                else sub = string.Format("?({0},{1})", subW, subH);

                return isYUVA ? string.Format("YUVA{0}", sub) : string.Format("YUV{0}", sub);
            }

            // ── Planar RGB / RGBA ─────────────────────────────────────────────
            // CS_GENERIC_RGBP  = CS_PLANAR | CS_BGR | CS_RGB_TYPE
            // CS_GENERIC_RGBAP = CS_PLANAR | CS_BGR | CS_RGBA_TYPE
            if (isPlanar && isBGR)
                return isRGBAType ? "RGBAP" : "RGBP";

            return "Unknown";
        }

        private static long ReadIntProp(AvsLibrary lib, IntPtr env, IntPtr map, string key, long defaultVal)
        {
            if (lib.PropGetType(env, map, key) != AvsApi.PROPTYPE_INT) return defaultVal;
            int error = 0;
            long val  = lib.PropGetInt(env, map, key, 0, ref error);
            return error == 0 ? val : defaultVal;
        }
    }
}
