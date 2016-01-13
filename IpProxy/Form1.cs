using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace IpProxy
{
    public partial class Form1 : Form
    {
        System.Collections.Concurrent.ConcurrentQueue<IpInfo> queue;
        System.Timers.Timer timer;
        bool closeTag = false;
        List<IpInfo> list = new List<IpInfo>();
        List<Dict> dictList = new List<Dict>();
        string url = "";
        string regex = "";
        string find = "";
        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
            queue = new System.Collections.Concurrent.ConcurrentQueue<IpInfo>();
            timer = new System.Timers.Timer(1000);
            timer.Elapsed += timer_Elapsed;
            timer.Start();
            url = "httsp://www.google.com";
            regex = @"plus\.google\.com";
            find = @"<td>(?<ip>\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})</td>\s+<td>(?<port>\d+)</td>";
            var path = AppDomain.CurrentDomain.BaseDirectory + "config.db";
            if (System.IO.File.Exists(path))
            {
                using (System.IO.StreamReader sr = new System.IO.StreamReader(path))
                {
                    this.setValue(sr.ReadLine(), sr.ReadLine(), sr.ReadLine());
                    sr.Close();
                }
            }

            var list = AppDomain.CurrentDomain.BaseDirectory + "list.db";
            if (System.IO.File.Exists(list))
            {
                button7_Click(null, null);
            }

            var dictPat = AppDomain.CurrentDomain.BaseDirectory + "Url.db";
            if (System.IO.File.Exists(dictPat))
            {
                using (System.IO.StreamReader sr = new System.IO.StreamReader(dictPat))
                {
                    while (!sr.EndOfStream)
                    {
                        dictList.Add(new Dict()
                        {
                            url = sr.ReadLine(),
                            regex = sr.ReadLine()
                        });
                    }
                }
            }
            this.FormClosed += Form1_FormClosed;
            closeTag = true;

            for (var i = 0; i < 10; i++)
            {
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    Check();
                });
            }
        }
        public void setValue(string _url, string _regex, string _find)
        {
            url = _url;
            regex = _regex;
            find = _find;

        }
        void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closeTag = false;
            timer.Stop();
            timer.Dispose();
            CloseProxy();
        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.label2.Text = queue.Count.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            button5_Click(null, null);



            var txt = this.textBox1.Text.Trim();
            if (!string.IsNullOrEmpty(txt))
            {
                #region Input
                var arr = txt.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    var client = new System.Net.WebClient();
                    foreach (var item in arr)
                    {
                        try
                        {

                            var str = client.DownloadString(item);
                            var ms = System.Text.RegularExpressions.Regex.Matches(str, find);
                            foreach (System.Text.RegularExpressions.Match mth in ms)
                            {
                                if (mth.Success)
                                {
                                    queue.Enqueue(new IpInfo()
                                    {
                                        IP = mth.Groups["ip"].Value,
                                        Port = int.Parse(mth.Groups["port"].Value),
                                        url = item
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.listBox2.Items.Add(ex.Message);
                        }

                    }
                });
                #endregion
            }
            for (var i = 0; i < dictList.Count; i++)
            {
                var item = dictList[i];
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    var client = new System.Net.WebClient();

                    try
                    {
                        client.Headers["Accept"] = "text/html, application/xhtml+xml, image/jxr, */*";
                        client.Headers["DNT"] = "1";
                        client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                        var str = client.DownloadString(item.url);
                        var ms = System.Text.RegularExpressions.Regex.Matches(str, item.regex);
                        foreach (System.Text.RegularExpressions.Match mth in ms)
                        {
                            if (mth.Success)
                            {
                                queue.Enqueue(new IpInfo()
                                {
                                    IP = mth.Groups["ip"].Value,
                                    Port = int.Parse(mth.Groups["port"].Value),
                                    url = item.url
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.listBox2.Items.Add(ex.Message);
                    }
                });
            }


        }

        protected void SetProxy()
        {
            try
            {
                var inx = this.listBox1.SelectedIndex;
                var info = this.list[inx];
                Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);

                //设置代理可用 
                rk.SetValue("ProxyEnable", 1);
                //设置代理IP和端口 
                rk.SetValue("ProxyServer", info.IP + ":" + info.Port);
                rk.Close();
                Reflush();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        protected void CloseProxy()
        {
            Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);

            rk.SetValue("ProxyEnable", 0);
            rk.Close();
            Reflush();
        }
        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lPBuffer, int lpdwBufferLength);

        private const int INTERNET_OPTION_REFRESH = 0x000025;
        private const int INTERNET_OPTION_SETTINGS_CHANGED = 0x000027;
        private void Reflush()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }

        private void Check()
        {
            IpInfo info = null;
            var id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            while (closeTag)
            {
                if (!queue.TryDequeue(out info))
                {
                    System.Threading.Thread.Sleep(50);
                    continue;
                }

                try
                {
                    this.listBox2.Items.Add(string.Format("threed:{0} check {1}:{2}", id, info.IP, info.Port));
                    listBox2.TopIndex = listBox2.Items.Count - (int)(listBox2.Height / listBox2.ItemHeight);
                    BClient client = new BClient();

                    client.Headers["Accept"] = "text/html, application/xhtml+xml, image/jxr, */*";
                    client.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                    client.Proxy = new WebProxy(info.IP, info.Port);
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    var str = client.DownloadString(url);
                    sw.Stop();
                    if (System.Text.RegularExpressions.Regex.Match(str, regex).Success)
                    {
                        this.listBox1.Items.Add(string.Format("threed:{0}  {1}:{2},times:{3},url:{4}", id, info.IP, info.Port, sw.ElapsedMilliseconds, info.url));
                        list.Add(info);
                        listBox1.TopIndex = listBox1.Items.Count - (int)(listBox1.Height / listBox1.ItemHeight);
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            CloseProxy();

        }

        private void button2_Click(object sender, EventArgs e)
        {
            SetProxy();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            queue = new System.Collections.Concurrent.ConcurrentQueue<IpInfo>();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                this.listBox1.Items.Clear();
            }
            catch (Exception)
            {
            }
            try
            {
                this.listBox2.Items.Clear();
            }
            catch (Exception)
            {
            }
            list.Clear();
            queue = new System.Collections.Concurrent.ConcurrentQueue<IpInfo>();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            var item = this.listBox1.SelectedItem;
            if (item == null)
                return;
            var inx = this.listBox1.SelectedIndex;
            var info = list[inx];

            using (System.IO.StreamWriter sw = new System.IO.StreamWriter("list.db", true))
            {
                sw.WriteLine(item.ToString());
                sw.WriteLine(string.Format("{0}:{1}", info.IP, info.Port));
                sw.Close();
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {

            var filename = AppDomain.CurrentDomain.BaseDirectory + "list.db";
            using (System.IO.StreamReader sr = new System.IO.StreamReader(filename))
            {
                while (!sr.EndOfStream)
                {
                    var title = sr.ReadLine();
                    var ipStr = sr.ReadLine();
                    listBox1.Items.Add(title);
                    var arr = ipStr.Split(':');
                    list.Add(new IpInfo()
                    {
                        Port = int.Parse(arr[1]),
                        IP = arr[0]
                    });
                }
                sr.Close();
            }

        }

        private void button8_Click(object sender, EventArgs e)
        {
            Form2 f = new Form2(this);
            f.ShowDialog();
        }
    }

    class IpInfo
    {
        public string IP { get; set; }

        public int Port { get; set; }

        public string url { get; set; }
    }

    class Dict
    {
        public string url { get; set; }

        public string regex { get; set; }
    }


    class BClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            var req = (HttpWebRequest)base.GetWebRequest(address);
            req.Timeout = 2000;
            req.ReadWriteTimeout = 2000;
            return req;
        }
    }
}
