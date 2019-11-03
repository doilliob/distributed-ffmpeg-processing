using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace worker
{
    public class CodecConfigurator
    {
        public string check_h264_modifications()
        {
            // try if H264_nvenc
            try
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "ffmpeg.exe";
                proc.StartInfo.Arguments = "-i video.mp4 -c:v h264_nvenc -y deleteme.mp4";
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.RedirectStandardOutput = false;
                proc.Start();
                proc.WaitForExit();
                if (proc.ExitCode == 0)
                {
                    return "h264_nvenc";
                }
            }
            catch (Exception e) { }
            finally
            {
                if (File.Exists("deleteme.mp4"))
                {
                    File.Delete("deleteme.mp4");
                }
            }

            // try if H264_qsv
            try
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "ffmpeg.exe";
                proc.StartInfo.Arguments = "-i video.mp4 -c:v h264_qsv -y deleteme.mp4";
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.RedirectStandardOutput = false;
                proc.Start();
                proc.WaitForExit();
                if (proc.ExitCode == 0)
                {
                    return "h264_qsv";
                }
            }
            catch (Exception e) { }
            finally
            {
                if (File.Exists("deleteme.mp4"))
                {
                    File.Delete("deleteme.mp4");
                }
            }
           
            return "h264";
        }
    }
}
