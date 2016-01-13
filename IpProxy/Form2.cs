using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IpProxy
{
    public partial class Form2 : Form
    {
        Form1 main;

        
        public Form2(Form1 _parent)
        {

            main = _parent;
            InitializeComponent();
            var path = AppDomain.CurrentDomain.BaseDirectory + "config.db";
            if (System.IO.File.Exists(path))
            {
                using (System.IO.StreamReader sr = new System.IO.StreamReader(path))
                {
                    this.textBox1.Text = sr.ReadLine();
                    this.textBox2.Text = sr.ReadLine();
                    this.textBox3.Text = sr.ReadLine();
                    sr.Close();
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            main.setValue(this.textBox1.Text, this.textBox2.Text,this.textBox3.Text);
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter("config.db", false))
            {
                sw.WriteLine(this.textBox1.Text);
                sw.WriteLine(this.textBox2.Text);
                sw.WriteLine(this.textBox3.Text);
                sw.Close();
            }
            this.Close();
        }
    }
}
