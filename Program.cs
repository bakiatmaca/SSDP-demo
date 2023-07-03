/* 30-06-2013 Baki Turan Atmaca
 * http://bakiatmaca.com/
 * bakituranatmaca@gmail.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.lxport.net.ssdp
{
    class Program
    {
        static void Main(string[] args)
        {
            SSDPService service = new SSDPService("http:\\testapp.abc:8765");
            service.IsDebugMode = true;
            service.Start();
            Console.WriteLine("SSDP service is started");

            while (true)
            {
                ConsoleKey key = Console.ReadKey().Key;

                if (key == ConsoleKey.Q)
                {
                    service.Stop();
                    Console.WriteLine("SSDP service is stopped");
                    break;
                }
            }
        }

    }
}