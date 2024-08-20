// ****************************************************************************
// 
// Copyright (C) 2005-2024 Doom9 & al
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
using System.IO;
using System.Globalization;
using System.Text;

using MeGUI.core.util;

namespace MeGUI
{
    class svtav1psyEncoder : CommandlineVideoEncoder
    {
        public static readonly JobProcessorFactory Factory = new JobProcessorFactory(new ProcessorFactory(init), "svtav1psyEncoder");

        private static IJobProcessor init(MainForm mf, Job j)
        {
            if (j is VideoJob && (j as VideoJob).Settings is svtav1psySettings)
            {
                UpdateCacher.CheckPackage("ffmpeg");
                //UpdateCacher.CheckPackage("svtav1psy");
                return new svtav1psyEncoder("cmd.exe");
            }
            return null;
        }

        public svtav1psyEncoder(string encoderPath) : base()
        {
            Executable = encoderPath;
            IMinimumChildProcessCount = 1;
        }

        public override void ProcessLine(string line, StreamType stream, ImageType oType)
        {
            
            if (line.StartsWith("Encoding:")) // status update
            {
                int frameNumberStart = line.IndexOf(":", 4) + 2;
                int frameNumberEnd = line.IndexOf("/");
                if (frameNumberStart > 0 && frameNumberEnd > 0 && frameNumberEnd > frameNumberStart)
                    if (base.setFrameNumber(line.Substring(frameNumberStart, frameNumberEnd - frameNumberStart).Trim()))
                        return;
            } 
            else if (FileUtil.RegExMatch(line, @"^Encoding: \d+/", true))
                base.setFrameNumber(line.Substring(11, line.IndexOf("/") - 11));

            else if (line.StartsWith("frame=")) // status update for ffmpeg
            {
                int frameNumberEnd = line.IndexOf("f", 6);
                if (base.setFrameNumber(line.Substring(6, frameNumberEnd - 6).Trim()))
                    return;
            }

            if (line.ToLowerInvariant().Contains("error") &&
               !line.ToLowerInvariant().StartsWith("input #0, avisynth, from '") &&
               !line.ToLowerInvariant().EndsWith("input/output error"))
                oType = ImageType.Error;
            else if (line.ToLowerInvariant().Contains("warning") &&
                !line.ToLowerInvariant().StartsWith("input #0, avisynth, from '"))
                oType = ImageType.Warning;
            base.ProcessLine(line, stream, oType);
        }

        public static string genCommandline(string input, string output, Dar? d, int hres, int vres, ref ulong numberOfFrames, svtav1psySettings _xs, LogItem log)
        {
            StringBuilder sb = new StringBuilder();
            CultureInfo ci = new CultureInfo("en-us");
            svtav1psySettings xs = (svtav1psySettings)_xs.Clone();
            MeGUI.packages.video.svtav1psy.svtav1psySettingsHandler oSettingsHandler = new packages.video.svtav1psy.svtav1psySettingsHandler(xs, log);

            // log
            if (log != null)
            {
                if (!String.IsNullOrEmpty(xs.CustomEncoderOptions))
                    log.LogEvent("custom command line: " + xs.CustomEncoderOptions);

                sb.Append("/c \"\"" + MainForm.Instance.Settings.FFmpeg.Path + "\" -loglevel level+error -i \"" + input + "\" -strict -1 -f yuv4mpegpipe - | ");
                sb.Append("\"" + MainForm.Instance.Settings.SvtAv1Psy.Path + "\" ");
            }

            #region main tab
            ///<summary>
            /// SVT-AV1-PSY Main Tab Settings
            ///</summary>
            ///

            // Progress & Input
            sb.Append("--progress 3 -i - ");
            sb.Append("--rc 0 --crf 43 ");

            // Presets
            if (!xs.CustomEncoderOptions.Contains("--preset "))
            {
                switch (xs.Preset)
                {
                    case 0:  sb.Append("--preset 0 "); break;
                    case 1:  sb.Append("--preset 1 "); break;
                    case 2:  sb.Append("--preset 2 "); break;
                    case 3:  sb.Append("--preset 3 "); break;
                    case 4:  sb.Append("--preset 4 "); break;
                    case 5:  sb.Append("--preset 5 "); break; 
                    case 6:  sb.Append("--preset 6 "); break;
                    case 7:  sb.Append("--preset 7 "); break;
                    case 8:  sb.Append("--preset 8 "); break;
                    case 9:  sb.Append("--preset 9 "); break;
                    case 10: sb.Append("--preset 10 "); break; // default value
                    case 11: sb.Append("--preset 11 "); break;
                    case 12: sb.Append("--preset 12 "); break;
                    case 13: sb.Append("--preset 13 "); break;
                    default: break;
                }
            }

            // Tunings
            if (!xs.CustomEncoderOptions.Contains("--tune "))
            {
                switch (xs.svtAv1PsyTuning)
                {
                    case svtav1psySettings.svtAv1PsyTuningModes.VQ:             sb.Append("--tune 0 "); break;
                    case svtav1psySettings.svtAv1PsyTuningModes.PSNR:           sb.Append("--tune 1 "); break;
                    case svtav1psySettings.svtAv1PsyTuningModes.SSIM:           sb.Append("--tune 2 "); break;
                    case svtav1psySettings.svtAv1PsyTuningModes.SUBJECTIVESSIM: sb.Append("--tune 3 "); break;
                    default: break;
                }
            }

            /*
            // Threads
            if (!xs.CustomEncoderOptions.Contains("-threads "))
                if (xs.NbThreads > 0)
                    sb.Append("-threads " + xs.NbThreads + " ");

            // bit-depth
            if (xs.FFV110Bits)
                sb.Append("-pix_fmt yuv420p10le ");
            */
            #endregion

            // get number of frames to encode
            oSettingsHandler.getFrames(ref numberOfFrames);
            if (numberOfFrames != null)
                sb.Append("--frames " + numberOfFrames.ToString() + " ");

            xs.CustomEncoderOptions = oSettingsHandler.getCustomCommandLine();
            if (!String.IsNullOrEmpty(xs.CustomEncoderOptions)) // add custom encoder options
                sb.Append(xs.CustomEncoderOptions + " ");

            if (log != null)
            {
                // input/output
                if (xs.FFV1EncodingType == VideoCodecSettings.FFV1EncodingMode.twopass1)
                    sb.Append("-f null NUL ");
                else if (!String.IsNullOrEmpty(output))
                    sb.Append("-b " + "\"" + output + "\""); 
            }

            return sb.ToString();
        }

        protected override string Commandline
        {
            get 
            {
                string strCommandLine = genCommandline(Job.Input, Job.Output, Job.DAR, Hres, Vres, ref NumberOfFrames, Job.Settings as svtav1psySettings, base.log);
                Su.NbFramesTotal = NumberOfFrames;
                return strCommandLine;
            }
        }

        protected override void doExitConfig()
        {
            base.doExitConfig();
        }
    }
}