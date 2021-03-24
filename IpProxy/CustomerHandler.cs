using Lending.KZKZ.EventBus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace IpProxy
{
    public class CustomerHandler : ISubscribe
    {

        [Subscribe("Ip.Test")]
        public async Task Check(IpInfo info)
        {

            try
            {
                Stopwatch sw = new Stopwatch();
                var listBox2 = Form1.Main.listBox2;
                var listBox1 = Form1.Main.listBox1;
                Form1.Main.listBox2.Items.Add(string.Format("threed:{0} check {1}:{2}", 0, info.IP, info.Port));
                listBox2.TopIndex = listBox2.Items.Count - (int)(listBox2.Height / listBox2.ItemHeight);
                var client = (HttpWebRequest)WebRequest.Create(info.remote);
                var uri = new Uri(info.remote, UriKind.Absolute);
                client.Timeout = 4000;
                client.ReadWriteTimeout = 4000;
                client.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                client.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.98 Safari/537.36";
                client.Host = uri.Host;
                client.Referer = info.remote;
                client.Proxy = new WebProxy(info.IP, info.Port);
                sw.Start();
                var rep = (HttpWebResponse)await client.GetResponseAsync();
                var stream = rep.GetResponseStream();
                sw.Stop();
                if (rep.ContentEncoding.ToLower().Contains("gzip"))
                {
                    stream = new GZipStream(stream, CompressionMode.Decompress);
                }
                using (var sm = new StreamReader(stream))
                {
                    var str = await sm.ReadToEndAsync();
                    if (System.Text.RegularExpressions.Regex.Match(str, info.Test).Success)
                    {
                        listBox1.Items.Add(string.Format("threed:{0}  {1}:{2},times:{3},url:{4}", 0, info.IP, info.Port, sw.ElapsedMilliseconds, info.url));
                        Form1.Main.list.Add(info);
                        listBox1.TopIndex = listBox1.Items.Count - (int)(listBox1.Height / listBox1.ItemHeight);
                    }

                }
            }
            catch (TimeoutException e)
            {

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

        }
    }
}
