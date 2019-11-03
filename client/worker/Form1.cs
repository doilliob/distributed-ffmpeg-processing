using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace worker
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            Configuration conf = Configuration.GetConfiguration();
            for(int i=0; i < conf.CoresCount; i++)
                new Thread(VideoProcessor.run).Start();
        }
    }
}
