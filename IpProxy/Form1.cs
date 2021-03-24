using IpProxy.Core;
using Lending.KZKZ.EventBus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Lending.KZKZ.EventBus.Cap;

namespace IpProxy
{
    public partial class Form1 : Form
    {
        System.Collections.Concurrent.ConcurrentQueue<IpInfo> queue;
        System.Timers.Timer timer;
        bool closeTag = false;
        public List<IpInfo> list = new List<IpInfo>();
        List<Dict> dictList = new List<Dict>();
        string url = "";
        string regex = "";
        string find = "";
        public static Form1 Main { get; private set; }


        IPublish Publish { get; }
        public Form1()
        {
            InitializeComponent();
            Main = this;
            Publish = IocManager.ServiceProvider.GetRequiredService<IPublish>();
            Control.CheckForIllegalCrossThreadCalls = false;
            queue = new System.Collections.Concurrent.ConcurrentQueue<IpInfo>();
            url = "https://www.google.com";
            regex = @"\.google\.com";
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

        }
        public void setValue(string _url, string _regex, string _find)
        {
            url = _url;
            regex = _regex;
            find = _find;

        }
        void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            IocManager.TokenSource.Cancel();
            closeTag = false;

            CloseProxy();
        }



        private void button1_Click(object sender, EventArgs e)
        {
            IocManager.TokenSource = new CancellationTokenSource();
            button5_Click(null, null);
          
            new System.Threading.Thread(() =>
            {
                for (var i = 0; i < dictList.Count; i++)
                {

                    if (IocManager.TokenSource.IsCancellationRequested)
                    {
                        return;
                    }
                    var item = dictList[i];



                    try
                    {
                        var client = (HttpWebRequest)WebRequest.Create(item.url);
                        var uri = new Uri(item.url, UriKind.Absolute);
                        client.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                        client.Headers["Cache-Control"] = "no-cache";
                        client.Headers["Pragma"] = "no-cache";
                        client.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.98 Safari/537.36";
                        client.Host = uri.Host;
                        client.Referer = item.url;
                        client.Headers["Upgrade-Insecure-Requests"] = "1";
                        client.Headers["Cookie"] = "_ydclearance=7430d4d0c5b6e6dafdb45a49-642b-4836-9dc5-56ff38e8514c-1490076867; channelid=0; sid=1490071175622631; Hm_lvt_7ed65b1cc4b810e9fd37959c9bb51b31=1490069668,1490071145,1490071576; Hm_lpvt_7ed65b1cc4b810e9fd37959c9bb51b31=1490071854; _ga=GA1.2.392719364.1490069668";
                        var rep = (HttpWebResponse)client.GetResponse();
                        var stream = rep.GetResponseStream();
                        if (rep.ContentEncoding.ToLower().Contains("gzip"))
                        {
                            stream = new GZipStream(stream, CompressionMode.Decompress);
                        }
                        using (var sm = new StreamReader(stream))
                        {

                            var str = sm.ReadToEnd();
                            var ms = System.Text.RegularExpressions.Regex.Matches(str, item.regex);
                            foreach (System.Text.RegularExpressions.Match mth in ms)
                            {
                                if (IocManager.TokenSource.IsCancellationRequested)
                                {
                                    return;
                                }
                                if (mth.Success)
                                {

                                    Publish.Publish("Ip.Test", new IpInfo()
                                    {
                                        IP = mth.Groups["ip"].Value,
                                        Port = int.Parse(mth.Groups["port"].Value),
                                        url = item.url,
                                        remote = url,
                                        Test = regex
                                    });
                                }
                            }
                        }
                        System.Threading.Thread.Sleep(2000);
                    }
                    catch (Exception ex)
                    {
                        this.listBox2.Items.Add(item.url + " err:" + ex.Message);
                    }

                }
            }).Start();
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

        private async void Check()
        {
            IpInfo info = null;
            var id = System.Threading.Thread.CurrentThread.ManagedThreadId;
            while (closeTag)
            {
                if (!queue.TryDequeue(out info))
                {
                    System.Threading.Thread.Sleep(10);
                    continue;
                }

                try
                {
                    Stopwatch sw = new Stopwatch();
                    this.listBox2.Items.Add(string.Format("threed:{0} check {1}:{2}", id, info.IP, info.Port));
                    listBox2.TopIndex = listBox2.Items.Count - (int)(listBox2.Height / listBox2.ItemHeight);
                    var client = (HttpWebRequest)WebRequest.Create(url);
                    var uri = new Uri(url, UriKind.Absolute);
                    client.Timeout = 4000;
                    client.ReadWriteTimeout = 4000;
                    client.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                    client.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.98 Safari/537.36";
                    client.Host = uri.Host;
                    client.Referer = url;
                    client.Proxy = new WebProxy(info.IP, info.Port);
                    sw.Start();
                    var rep = (HttpWebResponse)await client.GetResponseAsync().ConfigureAwait(false);
                    var stream = rep.GetResponseStream();
                    sw.Stop();
                    if (rep.ContentEncoding.ToLower().Contains("gzip"))
                    {
                        stream = new GZipStream(stream, CompressionMode.Decompress);
                    }
                    using (var sm = new StreamReader(stream))
                    {
                        var str = await sm.ReadToEndAsync().ConfigureAwait(false);

                        if (System.Text.RegularExpressions.Regex.Match(str, regex).Success)
                        {
                            this.listBox1.Items.Add(string.Format("threed:{0}  {1}:{2},times:{3},url:{4}", id, info.IP, info.Port, sw.ElapsedMilliseconds, info.url));
                            list.Add(info);
                            listBox1.TopIndex = listBox1.Items.Count - (int)(listBox1.Height / listBox1.ItemHeight);
                        }

                    }
                }
                catch (TimeoutException)
                {

                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
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
            IocManager.TokenSource.Cancel();
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

        private void button9_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                IocManager.Services.AddSingleton(typeof(CustomerHandler));
                IocManager.ServiceProvider.UseCap(IocManager.TokenSource.Token);
            });
        }

        private void button10_Click(object sender, EventArgs e)
        {
            button5_Click(null, null);
            var txt = this.textBox1.Text.Trim();

            if (!string.IsNullOrEmpty(txt))
            {
                #region Input
                var arr = txt.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {

                    foreach (var item in arr)
                    {
                        var u = item.Split(':');
                        var port = int.Parse(u[1]);
                        if (port > 0)
                        {
                            if (IocManager.TokenSource.IsCancellationRequested)
                            {
                                return;
                            }
                            Publish.Publish("Ip.Test", new IpInfo()
                            {
                                IP = u[0],
                                Port = port,
                                url = "123",
                                remote = url,
                                Test = regex
                            });
                        }


                    }
                });
                #endregion
            }
        }
    }

    public class IpInfo
    {
        public string IP { get; set; }

        public int Port { get; set; }

        public string url { get; set; }


        public string remote { get; set; }


        public string Test { get; set; }
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
