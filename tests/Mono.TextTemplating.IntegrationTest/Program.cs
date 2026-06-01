using System;
using IntegrationTest.Generated;

namespace Mono.TextTemplating.IntegrationTest
{
    class Program
    {
        static void Main (string[] args)
        {
            Console.WriteLine (GeneratedConstants.Message);
            Console.WriteLine ("Answer: " + GeneratedConstants.Answer);
            foreach (var item in GeneratedConstants.Items) {
                Console.WriteLine (item);
            }
        }
    }
}
