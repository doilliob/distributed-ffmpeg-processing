using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.MessagePatterns;

namespace worker
{
    public class VideoProcessor
    {
        
        public static void run()
        {

            // Connect ot RabbitMQ
            var factory = new ConnectionFactory();

            Configuration configuration = Configuration.GetConfiguration();
            int ThreadNumber = configuration.GetCoreNumerator();
            String TempVideoFile = "Temp_" + ThreadNumber.ToString() + ".mp4";

            factory.UserName = configuration.RabbitMQUser;
            factory.Password = configuration.RabbitMQPassword;
            factory.HostName = configuration.RabbitMQServer;
            string queue = configuration.RabbitMQQueue;

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: queue,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                var subscription = new Subscription(channel, queue, false);
                while (true)
                {
                    BasicDeliverEventArgs basicDeliveryEventArgs = subscription.Next();
                    string message = Encoding.UTF8.GetString(basicDeliveryEventArgs.Body);
                    try
                    {
                        JObject json_text = JObject.Parse(message);
                        // FTP in
                        String ftp_in_host = (String)json_text.GetValue("ftp_in_host");
                        String ftp_in_port = (String)json_text.GetValue("ftp_in_port");
                        String ftp_in_username = (String)json_text.GetValue("ftp_in_username");
                        String ftp_in_password = (String)json_text.GetValue("ftp_in_password");
                        String ftp_in_dir = (String)json_text.GetValue("ftp_in_dir");
                        String ftp_in_filename = (String)json_text.GetValue("ftp_in_filename");
                        // FTP out
                        String ftp_out_host = (String)json_text.GetValue("ftp_out_host");
                        String ftp_out_port = (String)json_text.GetValue("ftp_out_port");
                        String ftp_out_username = (String)json_text.GetValue("ftp_out_username");
                        String ftp_out_password = (String)json_text.GetValue("ftp_out_password");
                        String ftp_out_dir = (String)json_text.GetValue("ftp_out_dir");
                        String ftp_out_filename = (String)json_text.GetValue("ftp_out_filename");
                        // FFmpeg
                        String ffmpeg_video_codec = (String)json_text.GetValue("ffmpeg_video_codec");
                        if (ffmpeg_video_codec == "h264")
                            ffmpeg_video_codec = Configuration.GetConfiguration().H264;
                
                        String ffmpeg_video_bitrate = (String)json_text.GetValue("ffmpeg_video_bitrate");
                        String ffmpeg_audio_codec = (String)json_text.GetValue("ffmpeg_audio_codec");
                        String ffmpeg_audio_bitrate = (String)json_text.GetValue("ffmpeg_audio_bitrate");
                        String ffmpeg_vf = (String)json_text.GetValue("ffmpeg_vf");
                        String ffmpeg_from = (String)json_text.GetValue("ffmpeg_from");
                        String ffmpeg_to = (String)json_text.GetValue("ffmpeg_to");
                        String ffmpeg_additional = Configuration.GetConfiguration().FFmpegAdditionalSettings;
                        if (ffmpeg_from != "")
                            ffmpeg_from = " -ss " + ffmpeg_from + " ";

                        if (ffmpeg_to != "")
                            ffmpeg_to = " -to " + ffmpeg_to + " ";

                        // Если копируем кусок во временный файл
                        String ffmpeg_pre_params =
                            string.Format(" -y -i \"ftp://{0}:{1}@{2}:{3}/{4}/{5}\" -c:v copy {6} {7} {8} ",
                            ftp_in_username,
                            ftp_in_password,
                            ftp_in_host,
                            ftp_in_port,
                            ftp_in_dir,
                            ftp_in_filename,
                            ffmpeg_from,
                            ffmpeg_to,
                            TempVideoFile);

                        // Обработка временного файла и выгрузка на сервер
                        String ffmpeg_params =
                            string.Format(" -y -i {0} -c:v {1} -b:v {2} -c:a {3} -b:a {4} -vf \"{5}\" {6} \"ftp://{7}:{8}@{9}:{10}/{11}/{12}\"",
                            TempVideoFile,
                            ffmpeg_video_codec,
                            ffmpeg_video_bitrate,
                            ffmpeg_audio_codec,
                            ffmpeg_audio_bitrate,
                            ffmpeg_vf,
                            ffmpeg_additional,
                            ftp_out_username,
                            ftp_out_password,
                            ftp_out_host,
                            ftp_out_port,
                            ftp_out_dir,
                            ftp_out_filename);

                        

                        if (VideoProcessor.start_processing(ffmpeg_pre_params) && VideoProcessor.start_processing(ffmpeg_params))
                            subscription.Ack(basicDeliveryEventArgs);
                        else
                            subscription.Nack(true);
                    }
                    catch (Exception e)
                    {
                        subscription.Nack(true);
                    }

                }
                

            }
        } // --//-- public static void run()

        public static bool start_processing(String ffmpeg_params)
        {
            try
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = "ffmpeg.exe";
                proc.StartInfo.Arguments = ffmpeg_params;
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.RedirectStandardOutput = false;
                proc.Start();
                proc.WaitForExit();
                int exitCode = proc.ExitCode;
                if (exitCode == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }catch(Exception e)
            {
                return false;
            }
        }
    }
}
