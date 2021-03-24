using IpProxy.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Lending.KZKZ.EventBus.Cap;
using Microsoft.Extensions.DependencyInjection;

namespace IpProxy
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
           
            IocManager.Services.ConfigCap(host: "47.98.235.189",
                virtualHost: "ip",
                port: 5672,
                userName: "qulirqadmin",
                password: "qulirq@admin@!*(",
                capSchema: "ip",
                qouteName:"cap.ip",
                connectString: "Data Source=115.29.202.63;Initial Catalog=KZKZ;User ID=sa;Password=sa@^#");
            IocManager.ServiceProvider = IocManager.Services.BuildServiceProvider();
            IocManager.TokenSource= new System.Threading.CancellationTokenSource();
         
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
           
        }
    }
}
