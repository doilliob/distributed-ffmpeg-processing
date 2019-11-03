using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace worker
{
    public class Configuration
    {
        // Singleton
        private static Configuration instance = null;
        public static Configuration GetConfiguration()
        {
            if (Configuration.instance == null)
                Configuration.instance = new Configuration();
            return Configuration.instance;
        }

        // Properties
        public string H264;
        public int CoresCount;

        public string RabbitMQServer;
        public string RabbitMQUser;
        public string RabbitMQPassword;
        public string RabbitMQQueue;

        public string FFmpegAdditionalSettings;

        // Core Enumerator (for threads)
        private int CoreNumerator;
        private Mutex ThreadLocker;
        public int GetCoreNumerator()
        {
            this.ThreadLocker.WaitOne();
            this.CoreNumerator++;
            this.ThreadLocker.ReleaseMutex();
            return this.CoreNumerator;
        }

        public Configuration()
        {
            this.ThreadLocker = new Mutex();
            this.CoreNumerator = 0;

            this.H264 = new CodecConfigurator().check_h264_modifications();
            this.CoresCount = Environment.ProcessorCount;
            // Загружаем файл настроек
            INIManager manager = new INIManager("./SETTINGS.ini");
            this.RabbitMQServer = manager.GetPrivateString("rabbitmq", "server");
            this.RabbitMQUser = manager.GetPrivateString("rabbitmq", "user"); ;
            this.RabbitMQPassword = manager.GetPrivateString("rabbitmq", "password"); 
            this.RabbitMQQueue = manager.GetPrivateString("rabbitmq", "queue"); 
            this.FFmpegAdditionalSettings = manager.GetPrivateString("ffmpeg", "ffmpeg_additional_settings");
        }
    }
}
