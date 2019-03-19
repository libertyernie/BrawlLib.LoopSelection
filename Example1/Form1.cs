using BrawlLib.LoopSelection;
using MP3Sharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Example1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button2.Enabled = false;
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                using (var stream = new MP3Stream(openFileDialog1.FileName))
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms);
                    var wrapper = new AudioWrapper(ms.ToArray(), stream.ChannelCount, stream.Frequency);
                    using (var dialog = new BrstmConverterDialog(wrapper))
                    {
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            propertyGrid1.SelectedObject = wrapper;
                        }
                    }
                }
            }
            button1.Enabled = true;
            button2.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (propertyGrid1.SelectedObject is AudioWrapper wrapper)
            {
                using (var dialog = new BrstmConverterDialog(wrapper))
                {
                    dialog.ShowDialog(this);
                    propertyGrid1.SelectedObject = wrapper;
                }
            }
        }
    }
}
