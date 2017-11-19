using System;

namespace JsonConfig.Core.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("List all email property: ");
            foreach (dynamic o in JsonConfig.Global.CiMail)
            {
                Console.WriteLine(o);
            }

            // User define config, anything but JsonConfig.json is User define config, but can also be called by Global
            Console.WriteLine();
            Console.WriteLine("Demo for get single property:");
            Console.WriteLine("Email Account: {0}", JsonConfig.User.CiMail.Account);
            Console.WriteLine("Email Password: {0}", JsonConfig.User.CiMail.Password);

            Console.WriteLine();
            Console.WriteLine("Multiple layer property:");
            Console.WriteLine("Frameworks→Dnxcore50→bb: {0}", JsonConfig.Global.Frameworks.Dnxcore50.bb);
            Console.WriteLine("Frameworks→Dnxcore50→bb→ff: {0}", JsonConfig.Global.Frameworks.Dnxcore50.bb.ff);

            Console.WriteLine();
            //Console.WriteLine("Demo for multiple config file merge: ");
        }
    }
}