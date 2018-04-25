using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            var a = GenerateMembershipToken();
            Console.WriteLine(a);

            Console.ReadLine();
        }
        public static string GenerateMembershipToken()
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

            return new string(Enumerable.Repeat(chars, 6)
                .Select(x => x[random.Next(x.Length)]).ToArray());
        }
    }
}
